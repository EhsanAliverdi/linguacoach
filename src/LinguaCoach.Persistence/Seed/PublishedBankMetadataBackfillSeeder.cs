using LinguaCoach.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Phase E9 — one-time, idempotent metadata repair for the lean published bank tables
/// (<see cref="CefrVocabularyEntry"/>/<see cref="CefrGrammarProfileEntry"/>/
/// <see cref="CefrReadingReference"/>). Before E9 those tables dropped the context/focus/subskill/
/// difficulty metadata at publish time; E9 added the columns and made new publishes carry the
/// metadata, but rows already published (E6/E7/E8, and any production rows) still lack it. This
/// seeder backfills those rows from the metadata on the <see cref="ResourceCandidate"/> that
/// published them.
///
/// Safety rules (deliberately conservative — this is metadata repair, never content insertion):
/// <list type="bullet">
/// <item><description>A row is backfilled only when it has **no** selection metadata yet (all four
/// fields null) — existing values are never overwritten, so re-running is a no-op.</description></item>
/// <item><description>A row is backfilled only when it traces to **exactly one** published
/// <see cref="ResourceCandidate"/> (matched by <see cref="ResourceCandidate.PublishedEntityType"/>/
/// <see cref="ResourceCandidate.PublishedEntityId"/>). Rows with no match, or an ambiguous
/// multi-candidate match, are skipped — metadata is never guessed.</description></item>
/// <item><description>Only rows whose candidate actually carries some metadata are touched.</description></item>
/// </list>
/// Never inserts a bank row and never touches <see cref="CefrReadingPassage"/> (which already
/// stored this metadata since Phase E7).
/// </summary>
public static class PublishedBankMetadataBackfillSeeder
{
    public static async Task<int> RunAsync(LinguaCoachDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var total = 0;

        total += await BackfillAsync(
            db, db.CefrVocabularyEntries, nameof(CefrVocabularyEntry),
            e => e.Id,
            e => e.Subskill is null && e.DifficultyBand is null && e.ContextTagsJson is null && e.FocusTagsJson is null,
            (e, s, d, c, f) => e.SetSelectionMetadata(s, d, c, f), ct);

        total += await BackfillAsync(
            db, db.CefrGrammarProfileEntries, nameof(CefrGrammarProfileEntry),
            e => e.Id,
            e => e.Subskill is null && e.DifficultyBand is null && e.ContextTagsJson is null && e.FocusTagsJson is null,
            (e, s, d, c, f) => e.SetSelectionMetadata(s, d, c, f), ct);

        total += await BackfillAsync(
            db, db.CefrReadingReferences, nameof(CefrReadingReference),
            e => e.Id,
            e => e.Subskill is null && e.DifficultyBand is null && e.ContextTagsJson is null && e.FocusTagsJson is null,
            (e, s, d, c, f) => e.SetSelectionMetadata(s, d, c, f), ct);

        if (total > 0)
            await db.SaveChangesAsync(ct);

        logger.LogInformation("PublishedBankMetadataBackfillSeeder: backfilled selection metadata onto {Count} published bank row(s).", total);
        return total;
    }

    private static async Task<int> BackfillAsync<TEntity>(
        LinguaCoachDbContext db,
        DbSet<TEntity> set,
        string publishedEntityType,
        Func<TEntity, Guid> idOf,
        Func<TEntity, bool> hasNoMetadata,
        Action<TEntity, string?, int?, string?, string?> apply,
        CancellationToken ct) where TEntity : class
    {
        var rows = await set.ToListAsync(ct);
        var candidateRows = rows.Where(hasNoMetadata).ToList();
        if (candidateRows.Count == 0)
            return 0;

        // Published candidates for this bank type, grouped by the row they published so an ambiguous
        // (multi-candidate) match can be detected and skipped rather than guessed.
        var candidatesByPublishedId = await db.ResourceCandidates
            .AsNoTracking()
            .Where(c => c.PublishedEntityType == publishedEntityType && c.PublishedEntityId != null)
            .Select(c => new { c.PublishedEntityId, c.Subskill, c.DifficultyBand, c.ContextTagsJson, c.FocusTagsJson })
            .ToListAsync(ct);

        var lookup = candidatesByPublishedId
            .GroupBy(c => c.PublishedEntityId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var updated = 0;
        foreach (var row in candidateRows)
        {
            if (!lookup.TryGetValue(idOf(row), out var matches) || matches.Count != 1)
                continue; // no match or ambiguous — never guess

            var candidate = matches[0];
            var difficulty = candidate.DifficultyBand is >= 1 and <= 5 ? candidate.DifficultyBand : null;
            if (!HasAnyMetadata(candidate.Subskill, difficulty, candidate.ContextTagsJson, candidate.FocusTagsJson))
                continue; // candidate carries nothing useful — leave the row's fields null

            apply(row, candidate.Subskill, difficulty, candidate.ContextTagsJson, candidate.FocusTagsJson);
            updated++;
        }

        return updated;
    }

    private static bool HasAnyMetadata(string? subskill, int? difficultyBand, string? contextTagsJson, string? focusTagsJson) =>
        !string.IsNullOrWhiteSpace(subskill)
        || difficultyBand is not null
        || IsNonEmptyTagJson(contextTagsJson)
        || IsNonEmptyTagJson(focusTagsJson);

    private static bool IsNonEmptyTagJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        var trimmed = json.Trim();
        return trimmed is not ("[]" or "{}" or "null");
    }
}
