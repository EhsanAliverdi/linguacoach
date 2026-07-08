using System.Text.Json;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase E2 — deterministic rule validation + exact-fingerprint dedup gate for one staged
/// <see cref="ResourceCandidate"/>. Runs entirely over the candidate's current field values (its
/// own content plus whatever <see cref="ResourceCandidateAnalysisService"/> most recently
/// suggested) — the AI's output is never trusted to decide <see cref="ResourceCandidateValidationStatus"/>
/// itself; every gate here is a plain deterministic check. Never writes to any published Cefr*
/// bank table and never deletes a candidate (including duplicates — a duplicate is flagged
/// NeedsReview for a human to look at, not auto-removed).
/// </summary>
public sealed class ResourceCandidateValidationService : IResourceCandidateValidationService
{
    // Judgment call: below this AI-reported CEFR confidence, the level suggestion is not
    // trusted enough to pass automatically — it must get a human look. 0.6 is a conservative
    // middle point (roughly "more likely right than wrong, but not confidently so").
    public const double CefrConfidenceReviewThreshold = 0.6;

    // Judgment call: a conservative upper bound for a single candidate's canonical text — well
    // beyond a realistic vocabulary/grammar entry or even a full reading passage, but bounded so
    // one pathological row can't sail through staging unbounded. Mirrors the spirit of
    // ResourceImportService.MaxFileSizeBytes' "keep it bounded" discipline.
    public const int MaxCanonicalTextLength = 5000;

    private readonly LinguaCoachDbContext _db;
    private readonly IFormIoSchemaValidationService _formIoValidator;

    public ResourceCandidateValidationService(LinguaCoachDbContext db, IFormIoSchemaValidationService formIoValidator)
    {
        _db = db;
        _formIoValidator = formIoValidator;
    }

    public async Task<ResourceCandidateValidationResult> ValidateAsync(Guid candidateId, CancellationToken ct = default)
    {
        var loaded = await (
            from c in _db.ResourceCandidates
            join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
            join run in _db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
            join s in _db.CefrResourceSources on run.CefrResourceSourceId equals s.Id
            where c.Id == candidateId
            select new { Candidate = c, RawRecord = r, Run = run, Source = s })
            .FirstOrDefaultAsync(ct)
            ?? throw new ResourceImportValidationException($"Resource candidate '{candidateId}' was not found.");

        var candidate = loaded.Candidate;
        var errors = new List<string>();
        var warnings = new List<string>();
        var needsHumanReview = false;

        ValidateLanguage(candidate, errors);
        ValidateCefr(candidate, errors, warnings, ref needsHumanReview);
        ValidateSkillSubskill(candidate, errors);
        ValidateCandidateType(candidate, warnings, ref needsHumanReview);
        ValidateTextBounds(candidate, errors);
        ValidateSafety(candidate, errors);
        ValidateSourceLicense(loaded.Source, errors, warnings, ref needsHumanReview);
        ValidateFormIoSchema(candidate, errors);
        ValidateAttribution(loaded.Source, warnings, ref needsHumanReview);
        await ValidateDedupAsync(candidate, loaded.Run.Id, loaded.Run.CefrResourceSourceId, warnings, ct);
        if (warnings.Any(w => w.StartsWith("Duplicate", StringComparison.OrdinalIgnoreCase)))
            needsHumanReview = true;

        var status = errors.Count > 0
            ? ResourceCandidateValidationStatus.Failed
            : needsHumanReview
                ? ResourceCandidateValidationStatus.NeedsReview
                : ResourceCandidateValidationStatus.Passed;

        candidate.ApplyValidation(status, JsonSerializer.Serialize(new { errors, warnings }));
        await _db.SaveChangesAsync(ct);

        return new ResourceCandidateValidationResult(candidateId, status.ToString(), errors, warnings, needsHumanReview);
    }

    // ── Gate: English-only ──────────────────────────────────────────────────────
    private static void ValidateLanguage(ResourceCandidate candidate, List<string> errors)
    {
        if (!string.Equals(candidate.LanguageCode, "en", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"LanguageCode '{candidate.LanguageCode}' is not English.");
            return;
        }

        // Reuse the same conservative script heuristic Phase E1 uses at import time — documented
        // limitation carried over unchanged: catches Persian/Arabic-script and predominantly
        // non-Latin text only, not non-English Latin-script text (e.g. French, Turkish).
        var combinedText = $"{candidate.CanonicalText} {candidate.NormalizedJson}";
        if (ResourceLanguageHeuristic.LooksNonEnglish(combinedText, out var reason))
            errors.Add($"Candidate content failed the English-only script check: {reason}");
    }

    // ── Gate: CEFR level/confidence ─────────────────────────────────────────────
    private static void ValidateCefr(
        ResourceCandidate candidate, List<string> errors, List<string> warnings, ref bool needsHumanReview)
    {
        if (candidate.CefrLevel is not null && !CefrLevelConstants.IsValid(candidate.CefrLevel))
            errors.Add($"CefrLevel '{candidate.CefrLevel}' is not a recognized CEFR level.");

        if (candidate.CefrConfidence is < 0 or > 1)
        {
            errors.Add($"CefrConfidence {candidate.CefrConfidence} is out of the valid [0,1] range.");
            return;
        }

        // Low confidence must never auto-pass — flag for a human, don't fail outright (the level
        // suggestion may still be correct, it's just not trusted enough to publish unreviewed).
        if (candidate.CefrConfidence is { } confidence && confidence < CefrConfidenceReviewThreshold)
        {
            warnings.Add($"CEFR confidence {confidence:0.00} is below the {CefrConfidenceReviewThreshold:0.00} review threshold.");
            needsHumanReview = true;
        }
    }

    // ── Gate: skill/subskill taxonomy ───────────────────────────────────────────
    private static void ValidateSkillSubskill(ResourceCandidate candidate, List<string> errors)
    {
        if (candidate.PrimarySkill is not null && !CurriculumSkillConstants.IsValid(candidate.PrimarySkill))
        {
            errors.Add($"PrimarySkill '{candidate.PrimarySkill}' is not a recognized curriculum skill.");
            return;
        }

        if (candidate.Subskill is not null && candidate.PrimarySkill is null)
        {
            errors.Add("Subskill is set but PrimarySkill is not — a subskill requires a valid owning skill.");
            return;
        }

        if (candidate.PrimarySkill is not null
            && !CurriculumSubskillConstants.IsValidForSkill(candidate.PrimarySkill, candidate.Subskill))
        {
            errors.Add($"Subskill '{candidate.Subskill}' does not belong to skill '{candidate.PrimarySkill}'.");
        }
    }

    // ── Gate: candidate type ────────────────────────────────────────────────────
    private static void ValidateCandidateType(ResourceCandidate candidate, List<string> warnings, ref bool needsHumanReview)
    {
        // Judgment call: Unknown isn't an error (E1 already stages Unknown-typed rows on
        // purpose when no recognizable shape was inferred), but it has no clear downstream bank
        // mapping, so it always needs a human to classify it rather than auto-passing.
        if (candidate.CandidateType == ResourceCandidateType.Unknown)
        {
            warnings.Add("CandidateType is Unknown — needs manual classification before it can be considered further.");
            needsHumanReview = true;
        }
    }

    // ── Gate: text bounds ────────────────────────────────────────────────────────
    private static void ValidateTextBounds(ResourceCandidate candidate, List<string> errors)
    {
        // Non-empty/non-whitespace is already guaranteed by the constructor; only the upper
        // bound is new here.
        if (candidate.CanonicalText.Length > MaxCanonicalTextLength)
            errors.Add($"CanonicalText length {candidate.CanonicalText.Length} exceeds the maximum of {MaxCanonicalTextLength} characters.");
    }

    // ── Gate: safety ─────────────────────────────────────────────────────────────
    private static void ValidateSafety(ResourceCandidate candidate, List<string> errors)
    {
        // Judgment call: any AI-reported safety tag is treated as a hard fail, not just a
        // review flag — "never silently pass unsafe content" per spec means safety concerns
        // must block automatic progression, not merely get flagged.
        var safetyTags = ParseJsonStringArray(candidate.SafetyTagsJson);
        if (safetyTags.Count > 0)
            errors.Add($"Safety concern(s) reported: {string.Join("; ", safetyTags)}.");
    }

    // ── Gate: license/source re-check (revocable after original import) ────────
    private static void ValidateSourceLicense(
        CefrResourceSource source, List<string> errors, List<string> warnings, ref bool needsHumanReview)
    {
        if (!source.IsImportApproved)
        {
            errors.Add($"Source '{source.Name}' is no longer approved for import — approval may have been revoked since staging.");
            return;
        }

        // Judgment call: missing student-display/commercial-use permission doesn't invalidate
        // the candidate's own content quality, but it does mean the candidate can't move toward
        // a paying-student-facing publish yet, so it's worth a human decision rather than a
        // silent pass.
        if (!source.AllowsStudentDisplay || !source.AllowsCommercialUse)
        {
            warnings.Add($"Source '{source.Name}' does not currently allow student display and/or commercial use.");
            needsHumanReview = true;
        }
    }

    // ── Gate: Form.io schema safety (ActivityTemplateCandidate rows only) ──────
    private void ValidateFormIoSchema(ResourceCandidate candidate, List<string> errors)
    {
        if (candidate.CandidateType != ResourceCandidateType.ActivityTemplateCandidate)
            return;

        var schemaJson = ExtractFormIoSchemaJson(candidate);
        if (schemaJson is null)
        {
            errors.Add("ActivityTemplateCandidate has no recognizable Form.io schema JSON (expected a 'formio'/'schema'/'template' field).");
            return;
        }

        var result = _formIoValidator.ValidateSchema(schemaJson);
        if (!result.IsValid)
            errors.Add($"Form.io schema failed student-safe validation: {result.Error}");
    }

    /// <summary>
    /// Looks for the Form.io-shaped payload staged for an ActivityTemplateCandidate. E1's
    /// ResourceImportService stores the whole imported row (field-name-keyed) as
    /// <c>NormalizedJson</c> — the actual schema lives under whichever of 'formio'/'schema'/
    /// 'template' was populated on that row.
    /// </summary>
    private static string? ExtractFormIoSchemaJson(ResourceCandidate candidate)
    {
        try
        {
            using var doc = JsonDocument.Parse(candidate.NormalizedJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var fieldName in new[] { "formio", "schema", "template" })
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (string.Equals(prop.Name, fieldName, StringComparison.OrdinalIgnoreCase)
                        && prop.Value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(prop.Value.GetString()))
                    {
                        return prop.Value.GetString();
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not parseable — ExtractFormIoSchemaJson returns null, caller records the "no
            // recognizable schema" error rather than throwing.
        }

        return null;
    }

    // ── Gate: attribution ───────────────────────────────────────────────────────
    private static void ValidateAttribution(CefrResourceSource source, List<string> warnings, ref bool needsHumanReview)
    {
        // Judgment call for "required": treat any license whose name contains "BY" (covers every
        // Creative Commons "Attribution" variant: CC-BY, CC-BY-SA, CC-BY-NC, etc. — the family
        // that explicitly conditions reuse on giving credit) as attribution-required. This is a
        // simple, conservative name-based rule, not a full license-clause parser.
        var requiresAttribution = source.LicenseType.Contains("BY", StringComparison.OrdinalIgnoreCase);
        if (requiresAttribution && string.IsNullOrWhiteSpace(source.AttributionText))
        {
            // Warning, not a fail: a missing attribution string is a metadata gap an admin can
            // fix on the source record — it doesn't mean this specific candidate's content is
            // bad, so it shouldn't block it outright.
            warnings.Add($"Source '{source.Name}' license ('{source.LicenseType}') appears to require attribution, but AttributionText is not set.");
            needsHumanReview = true;
        }
    }

    // ── Gate: exact-fingerprint dedup ───────────────────────────────────────────
    private async Task ValidateDedupAsync(
        ResourceCandidate candidate, Guid runId, Guid sourceId, List<string> warnings, CancellationToken ct)
    {
        var withinRun = await (
            from c in _db.ResourceCandidates
            join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
            where c.Id != candidate.Id
                  && c.ContentFingerprint == candidate.ContentFingerprint
                  && r.ResourceImportRunId == runId
            select c.Id)
            .AnyAsync(ct);

        if (withinRun)
        {
            warnings.Add("Duplicate: another candidate with the same content fingerprint exists within this same import run.");
            return; // within-run duplicate implies the broader checks below would also match — no need to run them too
        }

        var withinSource = await (
            from c in _db.ResourceCandidates
            join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
            join run in _db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
            where c.Id != candidate.Id
                  && c.ContentFingerprint == candidate.ContentFingerprint
                  && run.CefrResourceSourceId == sourceId
            select c.Id)
            .AnyAsync(ct);

        if (withinSource)
        {
            warnings.Add("Duplicate: another candidate with the same content fingerprint exists elsewhere from the same source.");
            return;
        }

        // Global cross-run/cross-source check. Deliberately NOT cross-checked against published
        // Cefr* bank tables (CefrVocabularyEntry/CefrGrammarProfileEntry/CefrReadingReference) —
        // those entities have no fingerprint-shaped column today (confirmed: they predate
        // IActivityContentFingerprintService and were never retrofitted with one), and adding
        // one is out of scope for E2 (that's a schema change to published tables, not a staging
        // concern). A simpler direct-text cross-check was considered but rejected: it would need
        // a different comparison per entity/candidate type (Word vs. GrammarKey vs. Passage
        // text) and risks being a second, subtly different dedup heuristic living alongside the
        // fingerprint one — left for a future phase if bank-side dedup becomes a real need.
        var global = await _db.ResourceCandidates
            .Where(c => c.Id != candidate.Id && c.ContentFingerprint == candidate.ContentFingerprint)
            .Select(c => c.Id)
            .AnyAsync(ct);

        if (global)
            warnings.Add("Duplicate: another candidate with the same content fingerprint exists elsewhere in the candidate table (different run/source).");
    }

    private static IReadOnlyList<string> ParseJsonStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            var list = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    list.Add(item.GetString()!);
            }
            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
