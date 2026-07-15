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
/// <see cref="ResourceCandidateType.GrammarProfileEntry"/> — fully supported. Both publish into
/// <see cref="ResourceBankItem"/> (Phase I0) and need only a handful of fields that a staged
/// candidate reliably carries.</description></item>
/// <item><description><see cref="ResourceCandidateType.ReadingPassage"/> — routes to one of two
/// <see cref="ResourceBankItem"/> shapes based on staged text length (see
/// <see cref="MaxReadingExcerptLength"/>). Text at or under the threshold publishes as a
/// ReadingReference-typed row (short excerpt/citation only). Text over the threshold — a genuine
/// full-length passage — publishes as a ReadingPassage-typed row, never silently truncated into a
/// short excerpt (that would be lossy and dishonest about what was actually published). Both
/// still require CefrLevel and every publish gate above (English-only, source approval/license,
/// validation, admin approval).</description></item>
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
/// <item><description><see cref="ResourceCandidateType.WritingPrompt"/> (Phase J5a) — fully
/// supported, mirrors VocabularyEntry/GrammarProfileEntry's simple field-count needs. Publishes
/// content only (title/prompt/genre/suggested word count) — no rubric/answer-key field, since this
/// codebase has no writing-scoring implementation wired to the Resource Bank yet.</description></item>
/// <item><description><see cref="ResourceCandidateType.ListeningPassage"/> (Phase J5c) — requires
/// <see cref="Domain.Entities.ResourceCandidate.AudioStorageKey"/> to already be set (via
/// IResourceCandidateAudioService.UploadAsync) — blocked with a clear error otherwise, since
/// publishing a Listening resource with no actual audio would be dishonest about what was
/// published (same "don't publish something incomplete" reasoning as the ActivityTemplateCandidate
/// bullet below).</description></item>
/// <item><description><see cref="ResourceCandidateType.SpeakingPrompt"/> (Phase J5d) — fully
/// supported, text-only (no reference audio — see the J5d scope decision), mirrors WritingPrompt's
/// simple field-count needs.</description></item>
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

        // ── Phase 4.2 — mandatory Import Execution Plan provenance gate. Every candidate this
        // codebase can still create comes from ImportPackageProcessingService, which only ever
        // runs against a package with an Approved/Executing plan — so a candidate whose run has
        // no ImportPackageId, or whose package never had a plan approved, has no legitimate
        // provenance and must never publish, regardless of its own ValidationStatus/ReviewStatus. ──
        if (loaded.Run.ImportPackageId is not { } packageId)
        {
            errors.Add(
                "This candidate's Import Run has no associated Import Package — it cannot be traced to an " +
                "approved Import Execution Plan and cannot be published.");
        }
        else
        {
            var package = await _db.ImportPackages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == packageId, ct);
            if (package?.ApprovedImportProfileId is null)
            {
                errors.Add(
                    "This candidate's Import Package has no approved Import Execution Plan — publishing is " +
                    "blocked until the plan that governs this import is approved.");
            }
        }

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

        // ── Gate: deterministic validation must have passed, or at worst produced only advisory
        // warnings (NeedsReview) — see ResourceCandidateValidationStatus's doc comment. NeedsReview
        // is never a hard block; an admin approving it is exactly the override that's supposed to
        // let it through. Failed (hard errors) and Pending (never validated) both still block. ──
        if (candidate.ValidationStatus is not (ResourceCandidateValidationStatus.Passed or ResourceCandidateValidationStatus.NeedsReview))
        {
            errors.Add(
                $"ValidationStatus must be 'Passed' or 'NeedsReview' (warning-only) to publish — current: " +
                $"'{candidate.ValidationStatus}'. {ResourceCandidatePublishGateHelper.DescribeHardBlock(candidate)}");
        }

        // ── Gate: admin approval — separate from validation. ──
        if (candidate.ReviewStatus != ResourceCandidateReviewStatus.Approved)
        {
            errors.Add($"ReviewStatus must be 'Approved' to publish (current: '{candidate.ReviewStatus}').");
        }

        if (errors.Count > 0)
            return new ResourceCandidatePublishResult(false, null, null, null, errors);

        var (entity, entityTypeName, mappingErrors) = BuildTargetEntity(candidate, loaded.Source);
        if (entity is null)
            return new ResourceCandidatePublishResult(false, null, null, null, mappingErrors);

        _db.ResourceBankItems.Add((ResourceBankItem)entity);

        var publishedAtUtc = DateTimeOffset.UtcNow;
        candidate.MarkPublished(entityTypeName!, entity.Id, publishedAtUtc, publishedByUserId);

        // Single SaveChangesAsync call — the new bank row and the candidate's publish-state
        // mutation are written together, so a failure here leaves neither half persisted (this
        // codebase's convention for multi-entity writes; see e.g. AdminActivityTemplatePublishHandler).
        await _db.SaveChangesAsync(ct);

        return new ResourceCandidatePublishResult(true, entityTypeName, entity.Id, publishedAtUtc, Array.Empty<string>());
    }

    private static (BaseEntity? Entity, string? EntityTypeName, List<string> Errors) BuildTargetEntity(
        ResourceCandidate candidate, CefrResourceSource source)
    {
        var errors = new List<string>();
        var fields = ResourceCandidateFieldHelper.ParseFields(candidate.NormalizedJson);

        switch (candidate.CandidateType)
        {
            case ResourceCandidateType.VocabularyEntry:
                return BuildVocabularyEntry(candidate, source.Id, fields, errors);

            case ResourceCandidateType.GrammarProfileEntry:
                return BuildGrammarProfileEntry(candidate, source.Id, fields, errors);

            case ResourceCandidateType.ReadingPassage:
                return BuildReadingReferenceOrPassage(candidate, source, fields, errors);

            case ResourceCandidateType.WritingPrompt:
                return BuildWritingPromptEntity(candidate, source.Id, fields, errors);

            case ResourceCandidateType.ListeningPassage:
                return BuildListeningEntity(candidate, source, fields, errors);

            case ResourceCandidateType.SpeakingPrompt:
                return BuildSpeakingPromptEntity(candidate, source.Id, fields, errors);

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
        var word = (ResourceCandidateFieldHelper.GetFieldCI(fields, "word", "lemma", "headword") ?? candidate.CanonicalText).Trim();
        var partOfSpeech = ResourceCandidateFieldHelper.GetFieldCI(fields, "partofspeech", "pos")?.Trim();
        var notes = ResourceCandidateFieldHelper.GetFieldCI(fields, "definition", "meaning")?.Trim();

        try
        {
            var difficultyBand = candidate.DifficultyBand is >= 1 and <= 5 ? candidate.DifficultyBand : null;
            var entity = new ResourceBankItem(
                PublishedResourceType.Vocabulary, sourceId, candidate.CefrLevel,
                ResourceBankItemContent.Serialize(new VocabularyContent(word, partOfSpeech, notes)),
                candidate.Subskill, difficultyBand, candidate.ContextTagsJson, candidate.FocusTagsJson,
                candidate.ContentFingerprint);
            return (entity, "CefrVocabularyEntry", errors);
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

        var grammarPoint = (ResourceCandidateFieldHelper.GetFieldCI(fields, "grammarkey", "title") ?? candidate.CanonicalText).Trim();
        var description = ResourceCandidateFieldHelper.GetFieldCI(fields, "explanation")?.Trim();

        try
        {
            var difficultyBand = candidate.DifficultyBand is >= 1 and <= 5 ? candidate.DifficultyBand : null;
            var entity = new ResourceBankItem(
                PublishedResourceType.Grammar, sourceId, candidate.CefrLevel,
                ResourceBankItemContent.Serialize(new GrammarContent(grammarPoint, description)),
                candidate.Subskill, difficultyBand, candidate.ContextTagsJson, candidate.FocusTagsJson,
                candidate.ContentFingerprint);
            return (entity, "CefrGrammarProfileEntry", errors);
        }
        catch (ArgumentException ex)
        {
            errors.Add($"Could not construct a CefrGrammarProfileEntry: {ex.Message}");
            return (null, null, errors);
        }
    }

    private static (BaseEntity?, string?, List<string>) BuildWritingPromptEntity(
        ResourceCandidate candidate, Guid sourceId, IReadOnlyDictionary<string, string?> fields, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(candidate.CefrLevel))
        {
            errors.Add("CefrLevel is required to publish a WritingPrompt candidate but is not set.");
            return (null, null, errors);
        }

        var promptText = (ResourceCandidateFieldHelper.GetFieldCI(fields, "prompt") ?? candidate.CanonicalText).Trim();
        var title = ResourceCandidateFieldHelper.GetFieldCI(fields, "title")?.Trim()
            ?? (promptText.Length <= 80 ? promptText : promptText[..80].Trim() + "…");
        var genre = ResourceCandidateFieldHelper.GetFieldCI(fields, "genre", "tasktype")?.Trim();
        var suggestedMinWords = ParseSuggestedMinWords(
            ResourceCandidateFieldHelper.GetFieldCI(fields, "minwords", "suggestedminwords"));

        try
        {
            var difficultyBand = candidate.DifficultyBand is >= 1 and <= 5 ? candidate.DifficultyBand : null;
            var entity = new ResourceBankItem(
                PublishedResourceType.Writing, sourceId, candidate.CefrLevel,
                ResourceBankItemContent.Serialize(new WritingPromptContent(title, promptText, genre, suggestedMinWords)),
                candidate.Subskill, difficultyBand, candidate.ContextTagsJson, candidate.FocusTagsJson,
                candidate.ContentFingerprint);
            return (entity, "CefrWritingPrompt", errors);
        }
        catch (ArgumentException ex)
        {
            errors.Add($"Could not construct a CefrWritingPrompt: {ex.Message}");
            return (null, null, errors);
        }
    }

    private static int? ParseSuggestedMinWords(string? raw) =>
        int.TryParse(raw?.Trim(), out var value) && value > 0 ? value : null;

    private static (BaseEntity?, string?, List<string>) BuildListeningEntity(
        ResourceCandidate candidate, CefrResourceSource source, IReadOnlyDictionary<string, string?> fields, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(candidate.CefrLevel))
        {
            errors.Add("CefrLevel is required to publish a ListeningPassage candidate but is not set.");
            return (null, null, errors);
        }
        if (string.IsNullOrWhiteSpace(candidate.AudioStorageKey) || string.IsNullOrWhiteSpace(candidate.AudioContentType))
        {
            errors.Add(
                "An audio file is required to publish a ListeningPassage candidate — upload one via the candidate's " +
                "audio upload action before publishing.");
            return (null, null, errors);
        }

        var title = ResourceCandidateFieldHelper.GetFieldCI(fields, "title")?.Trim() ?? candidate.CanonicalText.Trim();
        var transcript = ResourceCandidateFieldHelper.GetFieldCI(fields, "transcript")?.Trim();

        try
        {
            var difficultyBand = candidate.DifficultyBand is >= 1 and <= 5 ? candidate.DifficultyBand : null;
            var entity = new ResourceBankItem(
                PublishedResourceType.Listening, source.Id, candidate.CefrLevel,
                ResourceBankItemContent.Serialize(new ListeningPassageContent(
                    title, transcript, candidate.AudioStorageKey, candidate.AudioContentType, source.AttributionText)),
                candidate.Subskill, difficultyBand, candidate.ContextTagsJson, candidate.FocusTagsJson,
                candidate.ContentFingerprint);
            return (entity, "CefrListeningPassage", errors);
        }
        catch (ArgumentException ex)
        {
            errors.Add($"Could not construct a CefrListeningPassage: {ex.Message}");
            return (null, null, errors);
        }
    }

    private static (BaseEntity?, string?, List<string>) BuildSpeakingPromptEntity(
        ResourceCandidate candidate, Guid sourceId, IReadOnlyDictionary<string, string?> fields, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(candidate.CefrLevel))
        {
            errors.Add("CefrLevel is required to publish a SpeakingPrompt candidate but is not set.");
            return (null, null, errors);
        }

        var promptText = (ResourceCandidateFieldHelper.GetFieldCI(fields, "scenario") ?? candidate.CanonicalText).Trim();
        var title = ResourceCandidateFieldHelper.GetFieldCI(fields, "title")?.Trim()
            ?? (promptText.Length <= 80 ? promptText : promptText[..80].Trim() + "…");
        var suggestedDurationSeconds = ParseSuggestedDurationSeconds(
            ResourceCandidateFieldHelper.GetFieldCI(fields, "durationseconds", "suggesteddurationseconds"));

        try
        {
            var difficultyBand = candidate.DifficultyBand is >= 1 and <= 5 ? candidate.DifficultyBand : null;
            var entity = new ResourceBankItem(
                PublishedResourceType.Speaking, sourceId, candidate.CefrLevel,
                ResourceBankItemContent.Serialize(new SpeakingPromptContent(title, promptText, suggestedDurationSeconds)),
                candidate.Subskill, difficultyBand, candidate.ContextTagsJson, candidate.FocusTagsJson,
                candidate.ContentFingerprint);
            return (entity, "CefrSpeakingPrompt", errors);
        }
        catch (ArgumentException ex)
        {
            errors.Add($"Could not construct a CefrSpeakingPrompt: {ex.Message}");
            return (null, null, errors);
        }
    }

    private static int? ParseSuggestedDurationSeconds(string? raw) =>
        int.TryParse(raw?.Trim(), out var value) && value > 0 ? value : null;

    private static (BaseEntity?, string?, List<string>) BuildReadingReferenceOrPassage(
        ResourceCandidate candidate, CefrResourceSource source, IReadOnlyDictionary<string, string?> fields, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(candidate.CefrLevel))
        {
            errors.Add("CefrLevel is required to publish a ReadingPassage candidate but is not set.");
            return (null, null, errors);
        }

        var passage = (ResourceCandidateFieldHelper.GetFieldCI(fields, "passage", "text") ?? candidate.CanonicalText).Trim();
        var title = ResourceCandidateFieldHelper.GetFieldCI(fields, "title")?.Trim();

        if (passage.Length <= MaxReadingExcerptLength)
        {
            var textType = ResourceCandidateFieldHelper.GetFieldCI(fields, "texttype", "type")?.Trim();
            var difficultyNotes = title is null ? null : $"Title: {title}";

            try
            {
                var difficultyBand = candidate.DifficultyBand is >= 1 and <= 5 ? candidate.DifficultyBand : null;
                var entity = new ResourceBankItem(
                    PublishedResourceType.ReadingReference, source.Id, candidate.CefrLevel,
                    ResourceBankItemContent.Serialize(new ReadingReferenceContent(textType, difficultyNotes, passage)),
                    candidate.Subskill, difficultyBand, candidate.ContextTagsJson, candidate.FocusTagsJson,
                    candidate.ContentFingerprint);
                return (entity, "CefrReadingReference", errors);
            }
            catch (ArgumentException ex)
            {
                errors.Add($"Could not construct a CefrReadingReference: {ex.Message}");
                return (null, null, errors);
            }
        }

        // Phase E7 — a genuine full-length passage. Published to CefrReadingPassage instead of
        // being blocked (Phase E4's original behavior) or silently truncated.
        if (string.IsNullOrWhiteSpace(title))
        {
            errors.Add(
                "A 'title' field is required to publish a full-length ReadingPassage candidate to CefrReadingPassage " +
                $"(passage text is {passage.Length} characters, over the {MaxReadingExcerptLength}-character " +
                "CefrReadingReference excerpt limit).");
            return (null, null, errors);
        }

        try
        {
            var difficultyBand = candidate.DifficultyBand is >= 1 and <= 5 ? candidate.DifficultyBand : null;
            var wordCount = passage.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            var estimatedReadingMinutes = Math.Max(1, (int)Math.Round(wordCount / AssumedWordsPerMinute, MidpointRounding.AwayFromZero));

            var entity = new ResourceBankItem(
                PublishedResourceType.ReadingPassage, source.Id, candidate.CefrLevel,
                ResourceBankItemContent.Serialize(new ReadingPassageContent(
                    title, passage, Summary: null, candidate.PrimarySkill ?? "Reading", TopicTagsJson: null,
                    wordCount, estimatedReadingMinutes, source.AttributionText, candidate.QualityScore)),
                candidate.Subskill, difficultyBand, candidate.ContextTagsJson, candidate.FocusTagsJson,
                candidate.ContentFingerprint);
            return (entity, "CefrReadingPassage", errors);
        }
        catch (ArgumentException ex)
        {
            errors.Add($"Could not construct a CefrReadingPassage: {ex.Message}");
            return (null, null, errors);
        }
    }

    // Assumed reading speed used to derive EstimatedReadingMinutes from WordCount — matches the
    // value the retired CefrReadingPassage entity used.
    private const double AssumedWordsPerMinute = 200;
}
