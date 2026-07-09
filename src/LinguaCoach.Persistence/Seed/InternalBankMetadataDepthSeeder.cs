using System.Text.Json;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Phase E10 — "Internal Bank Metadata Depth Expansion for Focus and Difficulty". An idempotent,
/// deterministic metadata-**repair** step for the lean published bank tables
/// (<see cref="CefrVocabularyEntry"/>/<see cref="CefrGrammarProfileEntry"/>/
/// <see cref="CefrReadingReference"/>). E9 added the columns and a copy-from-candidate backfill, but
/// the internal E6/E7/E8 lean rows were authored with context tags + subskill only — no difficulty
/// band and no focus tags — so D5's difficulty/focus filtering relaxes away on those types. E10
/// fills those two fields **by deriving them from the row's own already-published metadata**:
/// difficulty band from the row's CEFR level, and a focus tag from the row's subskill.
///
/// This is metadata repair, never content insertion, and is deliberately conservative:
/// <list type="bullet">
/// <item><description>Updates only **existing** published rows — never inserts a bank row.</description></item>
/// <item><description>Touches only rows whose <see cref="CefrResourceSource"/> is internal/original
/// (<c>LicenseType == "Internal/Original"</c>) **and** that trace to exactly one published
/// <see cref="ResourceCandidate"/> — i.e. rows this codebase's own internal packs published through
/// the real pipeline. Ambiguous or untraceable rows are skipped, never guessed.</description></item>
/// <item><description>Fills a field **only when it is currently empty** — an existing difficulty
/// band or non-empty focus-tag array is never overwritten. Re-running is a no-op.</description></item>
/// <item><description>Derives difficulty only for a mappable CEFR level (A1-C2); derives a focus tag
/// only from a subskill that is a valid <see cref="CurriculumSubskillConstants"/> value.</description></item>
/// <item><description>Preserves subskill and context tags exactly. Never touches
/// <see cref="CefrReadingPassage"/> (its focus/difficulty were authored in E8).</description></item>
/// </list>
/// No schema change (E9's columns already exist), no external data, no direct final-table content
/// insertion, no Persian/bilingual content — everything is derived from metadata already in the repo.
/// </summary>
public static class InternalBankMetadataDepthSeeder
{
    private const string InternalLicenseType = "Internal/Original";

    public static async Task<int> RunAsync(LinguaCoachDbContext db, ILogger logger, CancellationToken ct = default)
    {
        // Only internal/original sources are eligible — E10 enriches this codebase's own internal
        // content, never any future externally-sourced published rows.
        var internalSourceIds = (await db.CefrResourceSources
            .AsNoTracking()
            .Where(s => s.LicenseType == InternalLicenseType)
            .Select(s => s.Id)
            .ToListAsync(ct))
            .ToHashSet();

        var total = 0;

        total += await EnrichAsync(
            db, db.CefrVocabularyEntries, nameof(CefrVocabularyEntry), internalSourceIds,
            e => new LeanRowView(e.Id, e.SourceId, e.CefrLevel, e.Subskill, e.DifficultyBand, e.ContextTagsJson, e.FocusTagsJson),
            (e, s, d, c, f) => e.SetSelectionMetadata(s, d, c, f), ct);

        total += await EnrichAsync(
            db, db.CefrGrammarProfileEntries, nameof(CefrGrammarProfileEntry), internalSourceIds,
            e => new LeanRowView(e.Id, e.SourceId, e.CefrLevel, e.Subskill, e.DifficultyBand, e.ContextTagsJson, e.FocusTagsJson),
            (e, s, d, c, f) => e.SetSelectionMetadata(s, d, c, f), ct);

        total += await EnrichAsync(
            db, db.CefrReadingReferences, nameof(CefrReadingReference), internalSourceIds,
            e => new LeanRowView(e.Id, e.SourceId, e.CefrLevel, e.Subskill, e.DifficultyBand, e.ContextTagsJson, e.FocusTagsJson),
            (e, s, d, c, f) => e.SetSelectionMetadata(s, d, c, f), ct);

        if (total > 0)
            await db.SaveChangesAsync(ct);

        logger.LogInformation("InternalBankMetadataDepthSeeder: enriched focus/difficulty metadata on {Count} internal published bank row(s).", total);
        return total;
    }

    private readonly record struct LeanRowView(
        Guid Id, Guid SourceId, string CefrLevel, string? Subskill, int? DifficultyBand, string? ContextTagsJson, string? FocusTagsJson);

    private static async Task<int> EnrichAsync<TEntity>(
        LinguaCoachDbContext db,
        DbSet<TEntity> set,
        string publishedEntityType,
        IReadOnlySet<Guid> internalSourceIds,
        Func<TEntity, LeanRowView> viewOf,
        Action<TEntity, string?, int?, string?, string?> apply,
        CancellationToken ct) where TEntity : class
    {
        var rows = await set.ToListAsync(ct);

        // Only rows from an internal source that could still gain metadata are worth examining.
        var candidateRows = rows
            .Where(r =>
            {
                var v = viewOf(r);
                return internalSourceIds.Contains(v.SourceId)
                    && (v.DifficultyBand is null || !IsNonEmptyTagJson(v.FocusTagsJson));
            })
            .ToList();
        if (candidateRows.Count == 0)
            return 0;

        // Traceability: each row must map to exactly one published candidate (same conservative rule
        // as the E9 backfill), proving it came through the real pipeline rather than a bypass insert.
        var publishedIdCounts = (await db.ResourceCandidates
            .AsNoTracking()
            .Where(c => c.PublishedEntityType == publishedEntityType && c.PublishedEntityId != null)
            .Select(c => c.PublishedEntityId!.Value)
            .ToListAsync(ct))
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        var updated = 0;
        foreach (var row in candidateRows)
        {
            var v = viewOf(row);
            if (!publishedIdCounts.TryGetValue(v.Id, out var count) || count != 1)
                continue; // untraceable or ambiguous — never guess

            // Derive only where empty; never overwrite existing values.
            var newDifficulty = v.DifficultyBand ?? DeriveDifficultyBand(v.CefrLevel);
            var newFocusJson = IsNonEmptyTagJson(v.FocusTagsJson) ? v.FocusTagsJson : DeriveFocusTagsJson(v.Subskill);

            var difficultyChanged = v.DifficultyBand is null && newDifficulty is not null;
            var focusChanged = !IsNonEmptyTagJson(v.FocusTagsJson) && IsNonEmptyTagJson(newFocusJson);
            if (!difficultyChanged && !focusChanged)
                continue; // nothing deterministic to add for this row

            // Preserve subskill + context exactly; only difficulty/focus are (potentially) filled.
            apply(row, v.Subskill, newDifficulty, v.ContextTagsJson, newFocusJson);
            updated++;
        }

        return updated;
    }

    /// <summary>Deterministic CEFR → difficulty-band mapping (shared with the Phase D6 selector via
    /// <see cref="CefrDifficultyBand"/>). Returns null for any unrecognized level so nothing
    /// indefensible is written.</summary>
    private static int? DeriveDifficultyBand(string? cefrLevel) => CefrDifficultyBand.FromCefr(cefrLevel);

    /// <summary>Derives a single focus tag from the row's subskill (e.g. "vocabulary.collocation" →
    /// <c>["collocation"]</c>, "reading.inference" → <c>["inference"]</c>). Only derives from a
    /// subskill that is a valid <see cref="CurriculumSubskillConstants"/> value; returns null
    /// otherwise so no free-text or invalid focus tag is invented.</summary>
    private static string? DeriveFocusTagsJson(string? subskill)
    {
        if (string.IsNullOrWhiteSpace(subskill) || !CurriculumSubskillConstants.IsValid(subskill))
            return null;

        var dot = subskill.LastIndexOf('.');
        var tail = dot >= 0 && dot < subskill.Length - 1 ? subskill[(dot + 1)..] : subskill;
        if (string.IsNullOrWhiteSpace(tail))
            return null;

        return JsonSerializer.Serialize(new[] { tail.Trim().ToLowerInvariant() });
    }

    private static bool IsNonEmptyTagJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        var trimmed = json.Trim();
        return trimmed is not ("[]" or "{}" or "null");
    }
}
