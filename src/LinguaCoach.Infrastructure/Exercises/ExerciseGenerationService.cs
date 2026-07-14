using System.Text.Json;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.Placement;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Exercises;

/// <summary>
/// Phase H4 — deterministic "Generate Activity" composer, for both entry points
/// (<see cref="IGenerateActivityFromResourcesHandler"/>/<see cref="IGenerateActivityFromLessonHandler"/>).
/// Builds a pending-review <see cref="Exercise"/> draft directly from the fields of one
/// or more selected published Resource Bank rows — no AI provider call, matching Phase H3's
/// <c>LessonGenerationService</c> decision (no existing AI service in this codebase generates a
/// scored practice exercise from source text). Never modifies the resources or Lesson it reads
/// from, never creates a Module row, never assigns anything to a student.
///
/// Supported <c>ActivityType</c>s:
/// <list type="bullet">
/// <item><description><c>gap_fill</c> — Vocabulary/Grammar only. Shows the resource's own
/// definition/description, asks the student to type the term. Deterministically scored
/// (text_normalized).</description></item>
/// <item><description><c>multiple_choice_single</c> — Vocabulary/Grammar only. Asks "what does X
/// mean", with distractor options pulled from sibling published resources of the same type
/// already in the bank. Deterministically scored (single_choice). Requires at least one usable
/// distractor — generation is rejected (not silently degraded to a single-option "choice") when
/// none exist.</description></item>
/// <item><description><c>short_answer</c> — ReadingReference/ReadingPassage only. Open-ended
/// comprehension prompt over the resource's own excerpt/summary text, explicitly marked
/// <see cref="ComponentScoringRule.RequiresManualOrAiEvaluation"/> — reading comprehension can't be
/// deterministically graded from thin metadata, so this is honestly left ungraded rather than
/// faking a score.</description></item>
/// <item><description><c>reading_fill_in_blanks</c> — ReadingReference/ReadingPassage only
/// (Phase K16). Deterministic cloze over the resource's own excerpt/passage text — blanks out up
/// to 4 content words (length &gt;= 5, alphabetic), deterministically scored per-blank
/// (text_normalized), same "never AI-supplied correct answer" shape as gap_fill. Rejected when the
/// source text doesn't have enough distinct content words to build a meaningful cloze (fewer than
/// 2) — same "reject rather than degrade" discipline as multiple_choice_single's distractor
/// check.</description></item>
/// </list>
/// <see cref="Exercise.ScoringRulesJson"/> is serialized straight from the shared
/// <see cref="ScoringRulesDocument"/>/<see cref="ComponentScoringRule"/> types already used by
/// placement/onboarding/reorder_paragraphs scoring, so a future runtime integration can reuse
/// <see cref="LinguaCoach.Application.FormIo.ComponentAnswerScorer"/> as-is.
/// </summary>
public sealed class ActivityGenerationService : IGenerateActivityFromResourcesHandler, IGenerateActivityFromLessonHandler
{
    private const string GenerationProvider = "Deterministic";
    private const string GenerationModel = "activity-draft-composer-v1";
    private const int MaxDistractors = 3;

    public const string ActivityTypeGapFill = "gap_fill";
    public const string ActivityTypeMultipleChoiceSingle = "multiple_choice_single";
    public const string ActivityTypeShortAnswer = "short_answer";
    public const string ActivityTypeReadingFillInBlanks = "reading_fill_in_blanks";

    /// <summary>Phase K17 — AI-only activity type, no deterministic composer exists (or can
    /// exist) for it: unlike gap_fill/multiple_choice_single, ReadingReference/ReadingPassage
    /// resources have no single "the answer is X" field to derive a correct answer from, so
    /// building a real comprehension question requires the AI to judge the correct answer from
    /// the passage text itself — a deliberate, documented, scoped exception to this project's
    /// "AI never supplies the correct answer" rule (confirmed via AskUserQuestion). The existing
    /// PendingReview admin-approval gate is the safety net, same as every other generated
    /// Exercise. See <see cref="LinguaCoach.Infrastructure.Exercises.AiExerciseGenerationService"/>.</summary>
    public const string ActivityTypeReadingMultipleChoiceSingle = "reading_multiple_choice_single";

    private const int MaxClozeBlanks = 4;
    private const int MinClozeWordLength = 5;

    private static readonly HashSet<PublishedResourceType> DefinitionalTypes = new()
    {
        PublishedResourceType.Vocabulary, PublishedResourceType.Grammar
    };

    private readonly LinguaCoachDbContext _db;
    private readonly IFormIoSchemaValidationService _schemaValidator;

    public ActivityGenerationService(LinguaCoachDbContext db, IFormIoSchemaValidationService schemaValidator)
    {
        _db = db;
        _schemaValidator = schemaValidator;
    }

    private readonly record struct ResolvedResource(
        ExerciseResourceLinkInput Input, PublishedResourceType Type, LessonResourceRole Role, LessonResourceSnapshot Snapshot);

    public async Task<GenerateExerciseResult> HandleAsync(
        GenerateActivityFromResourcesRequest request, CancellationToken ct = default)
    {
        if (request.Resources is not { Count: > 0 })
            throw new ExerciseValidationException("At least one resource is required to generate an Activity.");
        if (request.DefaultCefrLevel is not null && !CefrLevelConstants.IsValid(request.DefaultCefrLevel))
            throw new ExerciseValidationException($"Default CEFR level '{request.DefaultCefrLevel}' is not a valid CEFR level.");
        if (request.DefaultDifficultyBand is < 1 or > 5)
            throw new ExerciseValidationException("Default difficulty band must be between 1 and 5.");

        var resolved = new List<ResolvedResource>();
        foreach (var input in request.Resources)
        {
            if (!LessonResourceLookup.TryParseResourceType(input.ResourceType, out var resourceType))
                throw new ExerciseValidationException($"Unsupported resource type '{input.ResourceType}'.");
            if (!LessonResourceLookup.TryParseRole(input.Role, out var role))
                throw new ExerciseValidationException($"Unsupported resource link role '{input.Role}'.");

            var snapshot = await LessonResourceLookup.FindAsync(_db, resourceType, input.ResourceId, ct)
                ?? throw new ExerciseValidationException(
                    $"Resource '{input.ResourceType}:{input.ResourceId}' was not found in the published Resource Bank.");

            resolved.Add(new ResolvedResource(input, resourceType, role, snapshot));
        }

        return await ComposeAndSaveAsync(
            resolved, request.RequestedActivityType, request.Title,
            request.DefaultCefrLevel, request.DefaultSkill, request.DefaultSubskill,
            request.DefaultContextTags, request.DefaultFocusTags, request.DefaultDifficultyBand,
            request.Notes, request.CreatedByUserId, lessonId: null, ct);
    }

    public async Task<GenerateExerciseResult> HandleAsync(
        GenerateActivityFromLessonRequest request, CancellationToken ct = default)
    {
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == request.LessonId, ct)
            ?? throw new ExerciseValidationException($"Lesson '{request.LessonId}' was not found.");

        var lessonLinks = await _db.LessonResourceLinks
            .Where(l => l.LessonId == lesson.Id).ToListAsync(ct);
        if (lessonLinks.Count == 0)
            throw new ExerciseValidationException(
                "This Lesson has no linked resources to generate an Activity from.");

        var resolved = new List<ResolvedResource>();
        foreach (var link in lessonLinks)
        {
            var snapshot = await LessonResourceLookup.FindAsync(_db, link.ResourceType, link.ResourceId, ct)
                ?? throw new ExerciseValidationException(
                    $"Resource '{link.ResourceType}:{link.ResourceId}' linked to this Lesson is no longer available in the published Resource Bank.");

            resolved.Add(new ResolvedResource(
                new ExerciseResourceLinkInput(link.ResourceType.ToString(), link.ResourceId, link.Role.ToString()),
                link.ResourceType, link.Role, snapshot));
        }

        return await ComposeAndSaveAsync(
            resolved, request.RequestedActivityType, request.Title ?? lesson.Title,
            lesson.CefrLevel, lesson.Skill, lesson.Subskill,
            ParseTags(lesson.ContextTagsJson), ParseTags(lesson.FocusTagsJson), lesson.DifficultyBand,
            request.Notes, request.CreatedByUserId, lessonId: lesson.Id, ct);
    }

    private async Task<GenerateExerciseResult> ComposeAndSaveAsync(
        List<ResolvedResource> resolved,
        string? requestedActivityType,
        string? title,
        string? defaultCefrLevel,
        string? defaultSkill,
        string? defaultSubskill,
        IReadOnlyList<string>? defaultContextTags,
        IReadOnlyList<string>? defaultFocusTags,
        int? defaultDifficultyBand,
        string? notes,
        Guid? createdByUserId,
        Guid? lessonId,
        CancellationToken ct)
    {
        var primaryMatch = resolved.FirstOrDefault(r => r.Role == LessonResourceRole.Primary);
        var primary = primaryMatch.Snapshot is not null ? primaryMatch : resolved[0];

        var isDefinitional = DefinitionalTypes.Contains(primary.Type);
        var activityType = requestedActivityType?.Trim().ToLowerInvariant();
        if (activityType is null)
            activityType = isDefinitional ? ActivityTypeGapFill : ActivityTypeShortAnswer;

        var supportedForCategory = isDefinitional
            ? new[] { ActivityTypeGapFill, ActivityTypeMultipleChoiceSingle }
            : new[] { ActivityTypeShortAnswer, ActivityTypeReadingFillInBlanks };
        if (!supportedForCategory.Contains(activityType))
            throw new ExerciseValidationException(
                $"Activity type '{activityType}' is not supported for resource type '{primary.Type}'. Supported: {string.Join(", ", supportedForCategory)}.");

        var (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson) = activityType switch
        {
            ActivityTypeGapFill => ComposeGapFill(primary.Snapshot),
            ActivityTypeMultipleChoiceSingle => await ComposeMultipleChoiceSingleAsync(primary.Type, primary.Snapshot, primaryMatch.Input.ResourceId, ct),
            ActivityTypeShortAnswer => ComposeShortAnswer(primary.Snapshot),
            ActivityTypeReadingFillInBlanks => await ComposeReadingFillInBlanksAsync(primary.Type, primary.Snapshot, primaryMatch.Input.ResourceId, ct),
            _ => throw new ExerciseValidationException($"Unsupported activity type '{activityType}'."),
        };

        var schemaCheck = _schemaValidator.ValidateSchema(formSchemaJson);
        if (!schemaCheck.IsValid)
            throw new ExerciseValidationException($"Generated Form.io schema failed validation: {schemaCheck.Error}");

        var resolvedTitle = !string.IsNullOrWhiteSpace(title) ? title!.Trim() : primary.Snapshot.Title;
        var cefrLevel = defaultCefrLevel ?? primary.Snapshot.CefrLevel;
        var skill = defaultSkill ?? primary.Snapshot.Skill;
        var subskill = defaultSubskill ?? primary.Snapshot.Subskill;
        var contextTags = defaultContextTags is { Count: > 0 }
            ? defaultContextTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.ContextTagsJson));
        var focusTags = defaultFocusTags is { Count: > 0 }
            ? defaultFocusTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.FocusTagsJson));
        var difficultyBand = defaultDifficultyBand ?? primary.Snapshot.DifficultyBand;

        var sourceMode = lessonId.HasValue ? ExerciseSourceMode.GeneratedFromLesson : ExerciseSourceMode.GeneratedFromResources;
        var description = $"Deterministic draft — review and edit before approval. Generated from: "
            + string.Join(", ", resolved.Select(r => r.Snapshot.Title)) + "."
            + (notes is not null ? $" {notes.Trim()}" : string.Empty);

        Exercise activity;
        try
        {
            activity = new Exercise(
                resolvedTitle, instructions, activityType, ExerciseRendererType.Formio, sourceMode,
                description, patternKey: null, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson,
                cefrLevel, skill, subskill,
                JsonSerializer.Serialize(contextTags), JsonSerializer.Serialize(focusTags),
                difficultyBand, estimatedMinutes: null, lessonId,
                GenerationProvider, GenerationModel, createdByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ExerciseValidationException(ex.Message);
        }

        _db.Exercises.Add(activity);
        await _db.SaveChangesAsync(ct);

        var links = resolved
            .Select(r => new ExerciseResourceLink(
                activity.Id, r.Type, r.Input.ResourceId, r.Role, r.Snapshot.Title, r.Snapshot.ContentFingerprint))
            .ToList();
        _db.ExerciseResourceLinks.AddRange(links);
        await _db.SaveChangesAsync(ct);

        var dto = ExerciseMappers.ToDto(activity, links);
        return new GenerateExerciseResult(dto, $"/admin/exercises?id={activity.Id}");
    }

    /// <summary>Shows the resource's own definition/description, asks the student to type the
    /// term back — needs no distractor pool, so it's the safe default for any Vocabulary/Grammar
    /// resource regardless of how much other bank content exists.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeGapFill(LessonResourceSnapshot primary)
    {
        var term = primary.Title;
        var definition = !string.IsNullOrWhiteSpace(primary.Body) ? primary.Body!.Trim() : "(no definition available)";

        var instructions = "Read the definition below and type the missing word or phrase.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "prompt", html = $"<p>{System.Net.WebUtility.HtmlEncode(definition)}</p>" },
                new { type = "textfield", key = "answer", label = "Your answer", input = true },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["answer"] = term });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.TextNormalized, CorrectAnswer: term, Points: 1.0) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct!",
            incorrectFeedback = $"Not quite — the answer was \"{term}\".",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Asks "what does X mean" with distractor options pulled from sibling published
    /// resources of the same type. Rejects (throws) rather than degrading to a single-option
    /// "choice" when no usable distractor exists.</summary>
    private async Task<(string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)>
        ComposeMultipleChoiceSingleAsync(PublishedResourceType type, LessonResourceSnapshot primary, Guid primaryId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(primary.Body))
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' has no definition/description text to build a multiple-choice question from — use 'gap_fill' instead.");

        var distractors = await FindDistractorDefinitionsAsync(type, primaryId, primary.CefrLevel, ct);
        if (distractors.Count == 0)
            throw new ExerciseValidationException(
                "No other published resources of the same type have a definition/description to use as distractors — use 'gap_fill' instead.");

        var options = new List<(string Key, string Text)> { ("opt_0", primary.Body!.Trim()) };
        for (var i = 0; i < distractors.Count; i++)
            options.Add(($"opt_{i + 1}", distractors[i]));

        var instructions = $"Choose the correct meaning of \"{primary.Title}\".";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new
                {
                    type = "radio", key = "answer", label = instructions, input = true,
                    values = options.Select(o => new { label = o.Text, value = o.Key }).ToArray(),
                },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["answer"] = options[0].Text });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.SingleChoice, CorrectAnswer: options[0].Key, Points: 1.0) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct!",
            incorrectFeedback = $"Not quite — the correct meaning was: \"{options[0].Text}\".",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Open-ended comprehension prompt, honestly marked as requiring manual/AI
    /// evaluation — reading comprehension can't be deterministically graded from a short
    /// excerpt/summary.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeShortAnswer(LessonResourceSnapshot primary)
    {
        var excerpt = !string.IsNullOrWhiteSpace(primary.Body) ? primary.Body!.Trim() : "(no excerpt available)";

        var instructions = "Read the passage below and answer the question in one or two sentences.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "passage", html = $"<p>{System.Net.WebUtility.HtmlEncode(excerpt)}</p>" },
                new { type = "textarea", key = "answer", label = $"What is \"{primary.Title}\" mainly about?", input = true },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, string?> { ["answer"] = null });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.TextNormalized, RequiresManualOrAiEvaluation: true) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = (string?)null,
            incorrectFeedback = (string?)null,
            note = "Open-ended — requires manual or AI evaluation, not deterministically scored.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Phase K16 — deterministic cloze over the resource's own excerpt/passage text.
    /// ReadingPassage's <see cref="LessonResourceSnapshot.Body"/> prefers Summary over PassageText
    /// (see <see cref="LessonResourceLookup.FindAsync"/>), which is too short for a meaningful
    /// cloze — this re-fetches the full PassageText directly for that type. ReadingReference has
    /// no separate summary field, so its Snapshot.Body (ReferenceExcerpt) is used as-is.</summary>
    private async Task<(string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)>
        ComposeReadingFillInBlanksAsync(PublishedResourceType type, LessonResourceSnapshot primary, Guid resourceId, CancellationToken ct)
    {
        var sourceText = type == PublishedResourceType.ReadingPassage
            ? await FindFullPassageTextAsync(resourceId, ct) ?? primary.Body
            : primary.Body;

        if (string.IsNullOrWhiteSpace(sourceText))
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' has no excerpt/passage text to build a cloze from — use 'short_answer' instead.");

        var words = sourceText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var blankIndexes = new List<int>();
        var seenWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < words.Length && blankIndexes.Count < MaxClozeBlanks; i++)
        {
            var clean = words[i].Trim('.', ',', ';', ':', '!', '?', '"', '\'', '(', ')');
            if (clean.Length < MinClozeWordLength || !clean.All(char.IsLetter)) continue;
            if (!seenWords.Add(clean)) continue;
            blankIndexes.Add(i);
        }

        if (blankIndexes.Count < 2)
            throw new ExerciseValidationException(
                $"Resource '{primary.Title}' does not have enough distinct content words to build a cloze — use 'short_answer' instead.");

        var answerKey = new Dictionary<string, string>();
        var scoringComponents = new Dictionary<string, ComponentScoringRule>();
        var displayWords = new List<string>(words);
        for (var b = 0; b < blankIndexes.Count; b++)
        {
            var idx = blankIndexes[b];
            var clean = words[idx].Trim('.', ',', ';', ':', '!', '?', '"', '\'', '(', ')');
            var key = $"answer_{b}";
            answerKey[key] = clean;
            scoringComponents[key] = new ComponentScoringRule(ScoringRuleKinds.TextNormalized, CorrectAnswer: clean, Points: 1.0);
            displayWords[idx] = $"({b + 1}) _____";
        }

        var clozeHtml = System.Net.WebUtility.HtmlEncode(string.Join(' ', displayWords));
        var instructions = "Read the passage below and fill in each numbered blank.";
        var components = new List<object>
        {
            new { type = "content", key = "passage", html = $"<p>{clozeHtml}</p>" },
        };
        for (var b = 0; b < blankIndexes.Count; b++)
            components.Add(new { type = "textfield", key = $"answer_{b}", label = $"Blank {b + 1}", input = true });

        var formSchemaJson = JsonSerializer.Serialize(new { components });
        var answerKeyJson = JsonSerializer.Serialize(answerKey);
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(scoringComponents));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct!",
            incorrectFeedback = $"The correct words were: {string.Join(", ", answerKey.Values)}.",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    private async Task<string?> FindFullPassageTextAsync(Guid resourceId, CancellationToken ct)
    {
        var json = await _db.ResourceBankItems
            .Where(e => e.Id == resourceId && e.Type == PublishedResourceType.ReadingPassage)
            .Select(e => e.ContentJson)
            .FirstOrDefaultAsync(ct);
        return json is null ? null : ResourceBankItemContent.Deserialize<ReadingPassageContent>(json).PassageText;
    }

    private async Task<List<string>> FindDistractorDefinitionsAsync(
        PublishedResourceType type, Guid excludeId, string? cefrLevel, CancellationToken ct)
    {
        if (type is not (PublishedResourceType.Vocabulary or PublishedResourceType.Grammar))
            return new List<string>();

        var rows = await _db.ResourceBankItems
            .Where(e => e.Type == type && e.Id != excludeId)
            .OrderByDescending(e => e.CefrLevel == cefrLevel)
            .ThenBy(e => e.CreatedAt)
            .Take(MaxDistractors * 2) // over-fetch — some rows may lack a usable definition/description
            .Select(e => e.ContentJson)
            .ToListAsync(ct);

        var bodies = type == PublishedResourceType.Vocabulary
            ? rows.Select(json => ResourceBankItemContent.Deserialize<VocabularyContent>(json).Notes)
            : rows.Select(json => ResourceBankItemContent.Deserialize<GrammarContent>(json).Description);

        return bodies
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => b!.Trim())
            .Distinct()
            .Take(MaxDistractors)
            .ToList();
    }

    private static List<string> ParseTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static List<string> MergeTagArrays(IEnumerable<string?> jsonArrays)
    {
        var merged = new List<string>();
        foreach (var json in jsonArrays)
        {
            if (string.IsNullOrWhiteSpace(json)) continue;
            try
            {
                var tags = JsonSerializer.Deserialize<List<string>>(json);
                if (tags is null) continue;
                foreach (var tag in tags)
                    if (!string.IsNullOrWhiteSpace(tag) && !merged.Contains(tag, StringComparer.OrdinalIgnoreCase))
                        merged.Add(tag);
            }
            catch (JsonException)
            {
                // Malformed tag JSON on a source row is never fatal to generation — skip it.
            }
        }
        return merged;
    }
}
