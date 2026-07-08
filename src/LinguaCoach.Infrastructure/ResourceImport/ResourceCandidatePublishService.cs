using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase E4 — publishes one approved, validated <see cref="ResourceCandidate"/> into its target
/// Cefr* bank table. Every gate is re-checked live against current field values (never trusting
/// <see cref="ResourceCandidate.ValidationStatus"/>/<see cref="ResourceCandidate.ReviewStatus"/>
/// alone, since a source's approval or license flags can change after staging/validation/approval
/// happened). Publishing an already-published candidate is a no-op that returns the existing
/// target reference — never a second bank row.
///
/// Candidate-type support decisions made for this phase:
/// <list type="bullet">
/// <item><description><see cref="ResourceCandidateType.VocabularyEntry"/> and
/// <see cref="ResourceCandidateType.GrammarProfileEntry"/> — fully supported. Their bank entities
/// (<see cref="CefrVocabularyEntry"/>/<see cref="CefrGrammarProfileEntry"/>) need only a handful
/// of fields that a staged candidate reliably carries.</description></item>
/// <item><description><see cref="ResourceCandidateType.ReadingPassage"/> — supported ONLY when the
/// staged passage text is short enough to plausibly already be an excerpt/citation (see
/// <see cref="MaxReadingExcerptLength"/>). <see cref="CefrReadingReference"/>'s own doc comment is
/// explicit that it holds "only a short excerpt/citation, not a full copyrighted text — reading
/// difficulty guidance, not a content library." A full reading passage (the normal shape a
/// ReadingPassage candidate carries) does not fit that entity's documented purpose — rather than
/// silently truncating a full passage into <see cref="CefrReadingReference.ReferenceExcerpt"/>
/// (lossy and dishonest about what was actually published), publish is blocked with a clear error
/// for anything over the threshold. Genuinely short passages/excerpts can still publish.</description></item>
/// <item><description><see cref="ResourceCandidateType.ActivityTemplateCandidate"/> — deferred
/// entirely in this phase. <see cref="ActivityTemplate"/> is a much richer entity than the Cefr*
/// ones: it needs a stable, unique <c>Key</c>, a curriculum-taxonomy-valid Skill/Subskill pair,
/// and (for the entity to be useful at all — <c>ActivityTemplateInstanceGenerator</c> requires it)
/// real hand-authored <c>GenerationInstructions</c> prose. A row staged from a simple CSV/JSON
/// import was never designed to carry a curriculum designer's generation instructions, and
/// inventing placeholder text to force the row through would publish something dishonest — a
/// "template" that looks complete but was never actually authored. Blocked with a clear error;
/// left for a future phase once/if a real staging shape for this candidate type exists.</description></item>
/// <item><description><see cref="ResourceCandidateType.Unknown"/> — always blocked; there is no
/// bank table it could possibly map to.</description></item>
/// </list>
/// </summary>
public sealed class ResourceCandidatePublishService : IResourceCandidatePublishService
{
    // Judgment call — see class doc comment's ReadingPassage bullet for the full reasoning.
    public const int MaxReadingExcerptLength = 500;

    private readonly LinguaCoachDbContext _db;

    public ResourceCandidatePublishService(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<ResourceCandidatePublishResult> PublishAsync(
        Guid candidateId, Guid? publishedByUserId, CancellationToken ct = default)
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

        // Idempotency — a repeated publish call must never create a duplicate bank row.
        if (candidate.IsPublished)
        {
            return new ResourceCandidatePublishResult(
                true, candidate.PublishedEntityType, candidate.PublishedEntityId, candidate.PublishedAtUtc,
                Array.Empty<string>());
        }

        var errors = new List<string>();

        // ── Gate: English-only, re-checked live (defense-in-depth; never trust the stored
        // LanguageCode/ValidationStatus alone — this mirrors ResourceCandidateValidationService's
        // own check exactly, run again here rather than assumed still true). ──
        if (!string.Equals(candidate.LanguageCode, "en", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"LanguageCode '{candidate.LanguageCode}' is not English.");
        }
        else
        {
            var combinedText = $"{candidate.CanonicalText} {candidate.NormalizedJson}";
            if (ResourceLanguageHeuristic.LooksNonEnglish(combinedText, out var reason))
                errors.Add($"Candidate content failed the English-only script check: {reason}");
        }

        // ── Gate: source import approval, re-checked live — approval may have been revoked
        // since the candidate was staged or last validated. ──
        if (!loaded.Source.IsImportApproved)
        {
            errors.Add(
                $"Source '{loaded.Source.Name}' is no longer approved for import — approval may have been revoked since staging.");
        }

        // ── Gate: student-display/commercial-use license permission. Judgment call: E2's
        // validation pass only warns + flags NeedsReview for this (a missing permission doesn't
        // mean the candidate's own content is bad). Publish is different — it is the actual step
        // that moves content toward a live, paying-student-facing bank table, so by this point the
        // permission gap must be hard-blocked, not merely acknowledged by a human reviewer. ──
        if (!loaded.Source.AllowsStudentDisplay)
        {
            errors.Add($"Source '{loaded.Source.Name}' does not allow student display — cannot publish to a student-facing bank.");
        }
        if (!loaded.Source.AllowsCommercialUse)
        {
            errors.Add($"Source '{loaded.Source.Name}' does not allow commercial use — cannot publish to a paying-student bank.");
        }

        // ── Gate: deterministic validation must have passed. ──
        if (candidate.ValidationStatus != ResourceCandidateValidationStatus.Passed)
        {
            errors.Add($"ValidationStatus must be 'Passed' to publish (current: '{candidate.ValidationStatus}').");
        }

        // ── Gate: admin approval — separate from validation. ──
        if (candidate.ReviewStatus != AdminReviewStatus.Approved)
        {
            errors.Add($"ReviewStatus must be 'Approved' to publish (current: '{candidate.ReviewStatus}').");
        }

        if (errors.Count > 0)
            return new ResourceCandidatePublishResult(false, null, null, null, errors);

        var (entity, entityTypeName, mappingErrors) = BuildTargetEntity(candidate, loaded.Source.Id);
        if (entity is null)
            return new ResourceCandidatePublishResult(false, null, null, null, mappingErrors);

        switch (entity)
        {
            case CefrVocabularyEntry vocabularyEntry:
                _db.CefrVocabularyEntries.Add(vocabularyEntry);
                break;
            case CefrGrammarProfileEntry grammarEntry:
                _db.CefrGrammarProfileEntries.Add(grammarEntry);
                break;
            case CefrReadingReference readingReference:
                _db.CefrReadingReferences.Add(readingReference);
                break;
        }

        var publishedAtUtc = DateTimeOffset.UtcNow;
        candidate.MarkPublished(entityTypeName!, entity.Id, publishedAtUtc, publishedByUserId);

        // Single SaveChangesAsync call — the new bank row and the candidate's publish-state
        // mutation are written together, so a failure here leaves neither half persisted (this
        // codebase's convention for multi-entity writes; see e.g. AdminActivityTemplatePublishHandler).
        await _db.SaveChangesAsync(ct);

        return new ResourceCandidatePublishResult(true, entityTypeName, entity.Id, publishedAtUtc, Array.Empty<string>());
    }

    private static (BaseEntity? Entity, string? EntityTypeName, List<string> Errors) BuildTargetEntity(
        ResourceCandidate candidate, Guid sourceId)
    {
        var errors = new List<string>();
        var fields = ResourceCandidateFieldHelper.ParseFields(candidate.NormalizedJson);

        switch (candidate.CandidateType)
        {
            case ResourceCandidateType.VocabularyEntry:
                return BuildVocabularyEntry(candidate, sourceId, fields, errors);

            case ResourceCandidateType.GrammarProfileEntry:
                return BuildGrammarProfileEntry(candidate, sourceId, fields, errors);

            case ResourceCandidateType.ReadingPassage:
                return BuildReadingReference(candidate, sourceId, fields, errors);

            case ResourceCandidateType.ActivityTemplateCandidate:
                errors.Add(
                    "ActivityTemplateCandidate publishing is deferred in Phase E4: ActivityTemplate requires a " +
                    "stable unique Key, a curriculum-taxonomy-valid Skill/Subskill pair, and hand-authored " +
                    "GenerationInstructions prose that a staged import row does not reliably carry. Publish this " +
                    "type once a real staging shape for those fields exists — see ResourceCandidatePublishService's " +
                    "class doc comment for the full reasoning.");
                return (null, null, errors);

            case ResourceCandidateType.Unknown:
            default:
                errors.Add($"CandidateType '{candidate.CandidateType}' has no supported bank publish target.");
                return (null, null, errors);
        }
    }

    private static (BaseEntity?, string?, List<string>) BuildVocabularyEntry(
        ResourceCandidate candidate, Guid sourceId, IReadOnlyDictionary<string, string?> fields, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(candidate.CefrLevel))
        {
            errors.Add("CefrLevel is required to publish a VocabularyEntry candidate but is not set.");
            return (null, null, errors);
        }

        // Same field-name convention ResourceCandidatePreviewService's BuildVocabularyPreview
        // uses — reused via ResourceCandidateFieldHelper rather than duplicated.
        var word = ResourceCandidateFieldHelper.GetFieldCI(fields, "word", "lemma") ?? candidate.CanonicalText;
        var partOfSpeech = ResourceCandidateFieldHelper.GetFieldCI(fields, "partofspeech", "pos");
        var notes = ResourceCandidateFieldHelper.GetFieldCI(fields, "definition", "meaning");

        try
        {
            var entity = new CefrVocabularyEntry(sourceId, word, candidate.CefrLevel, partOfSpeech, notes);
            return (entity, nameof(CefrVocabularyEntry), errors);
        }
        catch (ArgumentException ex)
        {
            errors.Add($"Could not construct a CefrVocabularyEntry: {ex.Message}");
            return (null, null, errors);
        }
    }

    private static (BaseEntity?, string?, List<string>) BuildGrammarProfileEntry(
        ResourceCandidate candidate, Guid sourceId, IReadOnlyDictionary<string, string?> fields, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(candidate.CefrLevel))
        {
            errors.Add("CefrLevel is required to publish a GrammarProfileEntry candidate but is not set.");
            return (null, null, errors);
        }

        var grammarPoint = ResourceCandidateFieldHelper.GetFieldCI(fields, "grammarkey", "title") ?? candidate.CanonicalText;
        var description = ResourceCandidateFieldHelper.GetFieldCI(fields, "explanation");

        try
        {
            var entity = new CefrGrammarProfileEntry(sourceId, candidate.CefrLevel, grammarPoint, description);
            return (entity, nameof(CefrGrammarProfileEntry), errors);
        }
        catch (ArgumentException ex)
        {
            errors.Add($"Could not construct a CefrGrammarProfileEntry: {ex.Message}");
            return (null, null, errors);
        }
    }

    private static (BaseEntity?, string?, List<string>) BuildReadingReference(
        ResourceCandidate candidate, Guid sourceId, IReadOnlyDictionary<string, string?> fields, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(candidate.CefrLevel))
        {
            errors.Add("CefrLevel is required to publish a ReadingPassage candidate but is not set.");
            return (null, null, errors);
        }

        var passage = ResourceCandidateFieldHelper.GetFieldCI(fields, "passage", "text") ?? candidate.CanonicalText;
        if (passage.Length > MaxReadingExcerptLength)
        {
            errors.Add(
                $"Passage text is {passage.Length} characters, which exceeds the {MaxReadingExcerptLength}-character " +
                "excerpt limit for CefrReadingReference. That entity intentionally holds only a short excerpt/" +
                "citation, not a full copyrighted text (see its own doc comment) — publishing a full passage into " +
                "it would misuse that field. Publish is blocked for this candidate rather than silently truncating " +
                "it. Shorten the staged content or defer this candidate.");
            return (null, null, errors);
        }

        var textType = ResourceCandidateFieldHelper.GetFieldCI(fields, "texttype", "type");
        var title = ResourceCandidateFieldHelper.GetFieldCI(fields, "title");
        var difficultyNotes = title is null ? null : $"Title: {title}";

        try
        {
            var entity = new CefrReadingReference(sourceId, candidate.CefrLevel, textType, difficultyNotes, passage);
            return (entity, nameof(CefrReadingReference), errors);
        }
        catch (ArgumentException ex)
        {
            errors.Add($"Could not construct a CefrReadingReference: {ex.Message}");
            return (null, null, errors);
        }
    }
}
