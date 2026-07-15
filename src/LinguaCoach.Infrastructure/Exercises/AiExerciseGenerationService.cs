using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.Placement;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Exercises;

/// <summary>
/// Phase J2b — AI-assisted "Generate Activity" composer, "from resources" entry point only. Builds
/// a pending-review <see cref="Exercise"/> draft the same way <see cref="ActivityGenerationService"/>
/// does, but AI supplies the framing content (gap-fill sentence / multiple-choice distractors /
/// comprehension question) instead of the deterministic composer's fixed templates. For
/// gap_fill/multiple_choice_single/short_answer, the correct answer, scoring rule, and answer key
/// are always deterministically derived from the resource's own fields — never AI-supplied — see
/// <see cref="IGenerateActivityFromResourcesWithAiHandler"/>'s doc comment for why.
///
/// Phase K17 — <see cref="ActivityGenerationService.ActivityTypeReadingMultipleChoiceSingle"/>,
/// <see cref="ActivityGenerationService.ActivityTypeReadingMultipleChoiceMulti"/>, and
/// <see cref="ActivityGenerationService.ActivityTypeHighlightCorrectSummary"/> are a deliberate,
/// scoped exception: reading/listening comprehension has no single fact field to derive a correct
/// answer from, so AI supplies the correct answer(s) too (see those constants' doc comments). The
/// PendingReview admin-approval gate — which every generated Exercise already goes through — is
/// the safety net for this exception, not a new one.
/// <see cref="ActivityGenerationService.ActivityTypeSelectMissingWord"/> is different: its correct
/// answer IS deterministic (a real word picked from the transcript by
/// <see cref="ActivityGenerationService.PickBlankWord"/>), AI only supplies wrong-word
/// distractors — same safe shape as multiple_choice_single.
///
/// Mirrors <see cref="Lessons.AiLessonGenerationService"/>'s retry-once-then-
/// throw pattern: this is a synchronous admin-triggered action the admin is actively waiting on, so
/// failure after the retry throws rather than degrading silently.
/// </summary>
public sealed class AiExerciseGenerationService : IGenerateActivityFromResourcesWithAiHandler
{
    public const string GeneratePromptKey = "exercise_generate_from_resources";

    private const int MaxResourceTextLength = 2000;
    private const int MaxDistractors = 3;
    private const string BlankMarker = "___";

    private readonly LinguaCoachDbContext _db;
    private readonly IFormIoSchemaValidationService _schemaValidator;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<AiExerciseGenerationService> _logger;

    public AiExerciseGenerationService(
        LinguaCoachDbContext db,
        IFormIoSchemaValidationService schemaValidator,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        ILogger<AiExerciseGenerationService> logger)
    {
        _db = db;
        _schemaValidator = schemaValidator;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
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

        var primaryMatch = resolved.FirstOrDefault(r => r.Role == LessonResourceRole.Primary);
        var primary = primaryMatch.Snapshot is not null ? primaryMatch : resolved[0];

        // Phase K17 — was a binary isDefinitional (Vocab/Grammar) vs everything-else split; now
        // resource-type-driven since Listening needed its own supported-type bucket (no
        // deterministic short_answer equivalent — Listening comprehension has no deterministic
        // path at all, only these AI-assisted MC types).
        var supportedForCategory = primary.Type switch
        {
            PublishedResourceType.Vocabulary or PublishedResourceType.Grammar =>
                new[] { ActivityGenerationService.ActivityTypeGapFill, ActivityGenerationService.ActivityTypeMultipleChoiceSingle },
            PublishedResourceType.ReadingReference or PublishedResourceType.ReadingPassage =>
                new[]
                {
                    ActivityGenerationService.ActivityTypeShortAnswer,
                    ActivityGenerationService.ActivityTypeReadingMultipleChoiceSingle,
                    ActivityGenerationService.ActivityTypeReadingMultipleChoiceMulti,
                },
            PublishedResourceType.Listening =>
                new[]
                {
                    ActivityGenerationService.ActivityTypeListeningMultipleChoiceSingle,
                    ActivityGenerationService.ActivityTypeListeningMultipleChoiceMulti,
                    ActivityGenerationService.ActivityTypeHighlightCorrectSummary,
                    ActivityGenerationService.ActivityTypeSelectMissingWord,
                },
            _ => Array.Empty<string>(),
        };
        if (supportedForCategory.Length == 0)
            throw new ExerciseValidationException(
                $"No AI-assisted Exercise types are supported for resource type '{primary.Type}' yet.");

        var activityType = request.RequestedActivityType?.Trim().ToLowerInvariant() ?? supportedForCategory[0];
        if (!supportedForCategory.Contains(activityType))
            throw new ExerciseValidationException(
                $"Activity type '{activityType}' is not supported for resource type '{primary.Type}'. Supported: {string.Join(", ", supportedForCategory)}.");

        if (activityType == ActivityGenerationService.ActivityTypeMultipleChoiceSingle && string.IsNullOrWhiteSpace(primary.Snapshot.Body))
            throw new ExerciseValidationException(
                $"Resource '{primary.Snapshot.Title}' has no definition/description text to build a multiple-choice question from — use 'gap_fill' instead.");
        if ((activityType == ActivityGenerationService.ActivityTypeReadingMultipleChoiceSingle
                || activityType == ActivityGenerationService.ActivityTypeReadingMultipleChoiceMulti)
            && string.IsNullOrWhiteSpace(primary.Snapshot.Body))
            throw new ExerciseValidationException(
                $"Resource '{primary.Snapshot.Title}' has no excerpt/passage text to build a comprehension question from — use 'short_answer' instead.");
        if ((activityType == ActivityGenerationService.ActivityTypeListeningMultipleChoiceSingle
                || activityType == ActivityGenerationService.ActivityTypeListeningMultipleChoiceMulti
                || activityType == ActivityGenerationService.ActivityTypeHighlightCorrectSummary)
            && string.IsNullOrWhiteSpace(primary.Snapshot.Body))
            throw new ExerciseValidationException(
                $"Resource '{primary.Snapshot.Title}' has no transcript to build a comprehension question from.");

        // Phase K17 — select_missing_word's correct answer is deterministic (a real word picked
        // directly from the transcript), never AI-supplied — computed before any AI call so a
        // resource with no eligible word rejects immediately, same "reject before AI call"
        // discipline as multiple_choice_single's missing-definition check.
        (string Word, string DisplayTextWithBlank)? blankWordPick = null;
        if (activityType == ActivityGenerationService.ActivityTypeSelectMissingWord)
        {
            blankWordPick = ActivityGenerationService.PickBlankWord(primary.Snapshot.Body);
            if (blankWordPick is null)
                throw new ExerciseValidationException(
                    $"Resource '{primary.Snapshot.Title}' has no eligible content word to build a select_missing_word blank from.");
        }

        var variables = new Dictionary<string, string>
        {
            ["activityType"] = activityType,
            ["resourceTitle"] = primary.Snapshot.Title,
            ["resourceDefinition"] = blankWordPick?.DisplayTextWithBlank ?? Truncate(primary.Snapshot.Body ?? ""),
            ["missingWord"] = blankWordPick?.Word ?? "",
            ["resourceType"] = primary.Type.ToString(),
            ["cefrLevel"] = request.DefaultCefrLevel ?? primary.Snapshot.CefrLevel,
            ["skill"] = request.DefaultSkill ?? primary.Snapshot.Skill,
            ["notes"] = request.Notes ?? "",
        };

        var correlationId = Guid.NewGuid().ToString("N")[..16];
        AiRequest aiRequest;
        try
        {
            aiRequest = await _contextBuilder.BuildAsync(GeneratePromptKey, variables, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build AI Exercise generation prompt. CorrelationId={CorrelationId}", correlationId);
            throw new ExerciseValidationException(
                $"Could not build the AI prompt: {ex.Message} Use the deterministic Generate action instead.");
        }

        AiExecutionResult execResult;
        try
        {
            execResult = await _aiExecution.ExecuteWithMetaAsync(GeneratePromptKey, aiRequest, null, correlationId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AI provider unavailable for AI Exercise generation. CorrelationId={CorrelationId}", correlationId);
            throw new ExerciseValidationException(
                $"AI generation is currently unavailable: {ex.Message} Use the deterministic Generate action instead.");
        }

        var parsed = TryParseAndValidateOutput(execResult.ResponseJson, activityType, primary.Snapshot, blankWordPick?.Word, out var parseError);
        if (parsed is null)
        {
            try
            {
                execResult = await _aiExecution.ExecuteWithMetaAsync(GeneratePromptKey, aiRequest, null, correlationId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "AI provider unavailable on retry for AI Exercise generation. CorrelationId={CorrelationId}", correlationId);
                throw new ExerciseValidationException(
                    $"AI generation is currently unavailable: {ex.Message} Use the deterministic Generate action instead.");
            }

            parsed = TryParseAndValidateOutput(execResult.ResponseJson, activityType, primary.Snapshot, blankWordPick?.Word, out parseError);
            if (parsed is null)
            {
                throw new ExerciseValidationException(
                    $"AI response could not be parsed after retry: {parseError} Use the deterministic Generate action instead.");
            }
        }

        var (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson) = activityType switch
        {
            ActivityGenerationService.ActivityTypeGapFill => ComposeGapFill(primary.Snapshot, parsed.PromptText!),
            ActivityGenerationService.ActivityTypeMultipleChoiceSingle => ComposeMultipleChoiceSingle(primary.Snapshot, parsed.Distractors),
            ActivityGenerationService.ActivityTypeShortAnswer => ComposeShortAnswer(primary.Snapshot, parsed.PromptText!),
            ActivityGenerationService.ActivityTypeReadingMultipleChoiceSingle => ComposeReadingMultipleChoiceSingle(parsed.PromptText!, parsed.CorrectAnswerText!, parsed.Distractors),
            ActivityGenerationService.ActivityTypeReadingMultipleChoiceMulti => ComposeReadingMultipleChoiceMulti(parsed.PromptText!, parsed.CorrectAnswersText!, parsed.Distractors),
            // Phase K17 — listening_multiple_choice_single/multi reuse the exact same composers as
            // their reading counterparts (identical shape: radio/selectboxes + single_choice/
            // multiple_choice scoring); only the source text (transcript vs excerpt) differs, and
            // that's already handled upstream by resourceDefinition/the prompt template.
            ActivityGenerationService.ActivityTypeListeningMultipleChoiceSingle => ComposeReadingMultipleChoiceSingle(parsed.PromptText!, parsed.CorrectAnswerText!, parsed.Distractors),
            ActivityGenerationService.ActivityTypeListeningMultipleChoiceMulti => ComposeReadingMultipleChoiceMulti(parsed.PromptText!, parsed.CorrectAnswersText!, parsed.Distractors),
            // Phase K17 — highlight_correct_summary is the same "AI supplies the correct answer"
            // shape as reading/listening MC single — the correct answer is a one-sentence summary
            // the AI judges from the transcript, distractors are plausible-but-wrong summaries.
            ActivityGenerationService.ActivityTypeHighlightCorrectSummary => ComposeReadingMultipleChoiceSingle(parsed.PromptText!, parsed.CorrectAnswerText!, parsed.Distractors),
            // Phase K17 — select_missing_word is the odd one out: the correct answer is
            // deterministic (blankWordPick, computed before any AI call), AI only supplies
            // wrong-word distractors — same safe shape as multiple_choice_single.
            ActivityGenerationService.ActivityTypeSelectMissingWord => ComposeSelectMissingWord(blankWordPick!.Value.DisplayTextWithBlank, blankWordPick!.Value.Word, parsed.Distractors),
            _ => throw new ExerciseValidationException($"Unsupported activity type '{activityType}'."),
        };

        var schemaCheck = _schemaValidator.ValidateSchema(formSchemaJson);
        if (!schemaCheck.IsValid)
            throw new ExerciseValidationException($"Generated Form.io schema failed validation: {schemaCheck.Error}");

        var resolvedTitle = !string.IsNullOrWhiteSpace(request.Title) ? request.Title!.Trim() : primary.Snapshot.Title;
        var cefrLevel = request.DefaultCefrLevel ?? primary.Snapshot.CefrLevel;
        var skill = request.DefaultSkill ?? primary.Snapshot.Skill;
        var subskill = request.DefaultSubskill ?? primary.Snapshot.Subskill;
        var contextTags = request.DefaultContextTags is { Count: > 0 }
            ? request.DefaultContextTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.ContextTagsJson));
        var focusTags = request.DefaultFocusTags is { Count: > 0 }
            ? request.DefaultFocusTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.FocusTagsJson));
        var difficultyBand = request.DefaultDifficultyBand ?? primary.Snapshot.DifficultyBand;

        var description = $"AI draft ({execResult.ProviderName}/{execResult.ModelName}) — review and edit before approval. "
            + $"Generated from: {string.Join(", ", resolved.Select(r => r.Snapshot.Title))}."
            + (request.Notes is not null ? $" {request.Notes.Trim()}" : string.Empty);

        // Phase 1 (2026-07-15 pipeline safety audit) — request.LessonId is set when this request
        // was synthesized from a Lesson's own resources (LessonExerciseBatchGenerationService's
        // AI-preferred-type routing); previously hardcoded to null here, which silently dropped
        // Exercise.LessonId for every AI-generated Exercise reached via that path. SourceMode
        // mirrors ExerciseGenerationService.ComposeAndSaveAsync's own lessonId.HasValue check for
        // consistency between the deterministic and AI composers.
        var sourceMode = request.LessonId.HasValue ? ExerciseSourceMode.GeneratedFromLesson : ExerciseSourceMode.GeneratedFromResources;

        Exercise activity;
        try
        {
            activity = new Exercise(
                resolvedTitle, instructions, activityType, ExerciseRendererType.Formio, sourceMode,
                description, patternKey: null, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson,
                cefrLevel, skill, subskill,
                JsonSerializer.Serialize(contextTags), JsonSerializer.Serialize(focusTags),
                difficultyBand, estimatedMinutes: null, lessonId: request.LessonId,
                execResult.ProviderName, execResult.ModelName, request.CreatedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ExerciseValidationException($"AI-generated content failed validation: {ex.Message}");
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

    /// <summary>AI supplies only the sentence; the blank's answer is always the resource's own
    /// term, never AI-supplied — the correct answer can never drift from what the resource bank
    /// actually says.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeGapFill(LessonResourceSnapshot primary, string aiSentence)
    {
        var term = primary.Title;
        var instructions = "Read the sentence below and type the missing word or phrase.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "prompt", html = $"<p>{System.Net.WebUtility.HtmlEncode(aiSentence)}</p>" },
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

    /// <summary>AI supplies only the wrong-option distractors; the correct option's text is always
    /// the resource's own definition, verbatim — never AI-paraphrased, so the scoring key can never
    /// be wrong.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeMultipleChoiceSingle(LessonResourceSnapshot primary, IReadOnlyList<string> distractors)
    {
        var correctText = primary.Body!.Trim();
        var options = new List<(string Key, string Text)> { ("opt_0", correctText) };
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

    /// <summary>AI supplies only the question; the excerpt shown is always the resource's own body
    /// text. Already honestly marked as requiring manual/AI evaluation — same as the deterministic
    /// composer — so there is no scoring-integrity risk here either way.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeShortAnswer(LessonResourceSnapshot primary, string aiQuestion)
    {
        var excerpt = !string.IsNullOrWhiteSpace(primary.Body) ? primary.Body!.Trim() : "(no excerpt available)";
        var instructions = "Read the passage below and answer the question.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "passage", html = $"<p>{System.Net.WebUtility.HtmlEncode(excerpt)}</p>" },
                new { type = "textarea", key = "answer", label = aiQuestion, input = true },
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

    /// <summary>Phase K17 — AI supplies the question, the correct answer, AND the distractors
    /// (see <see cref="ActivityGenerationService.ActivityTypeReadingMultipleChoiceSingle"/>'s
    /// doc comment for why this is a deliberate exception to "AI never supplies the correct
    /// answer"). The generated Exercise still starts PendingReview like every other one — an
    /// admin must read the passage and verify the AI's answer before approving.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeReadingMultipleChoiceSingle(string question, string correctAnswerText, IReadOnlyList<string> distractors)
    {
        var options = new List<(string Key, string Text)> { ("opt_0", correctAnswerText) };
        for (var i = 0; i < distractors.Count; i++)
            options.Add(($"opt_{i + 1}", distractors[i]));

        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new
                {
                    type = "radio", key = "answer", label = question, input = true,
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
            incorrectFeedback = $"Not quite — the correct answer was: \"{options[0].Text}\".",
        });

        return (question, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Phase K17 — multi-select variant of <see cref="ComposeReadingMultipleChoiceSingle"/>,
    /// same "AI supplies the correct answer(s)" exception. Correct options are listed first,
    /// distractors after — same non-shuffled convention the single-choice composer already
    /// uses.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeReadingMultipleChoiceMulti(string question, IReadOnlyList<string> correctAnswers, IReadOnlyList<string> distractors)
    {
        var options = new List<(string Key, string Text, bool IsCorrect)>();
        for (var i = 0; i < correctAnswers.Count; i++)
            options.Add(($"opt_{i}", correctAnswers[i], true));
        for (var i = 0; i < distractors.Count; i++)
            options.Add(($"opt_{correctAnswers.Count + i}", distractors[i], false));

        var correctKeys = options.Where(o => o.IsCorrect).Select(o => o.Key).ToList();
        var correctTexts = options.Where(o => o.IsCorrect).Select(o => o.Text).ToList();

        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new
                {
                    type = "selectboxes", key = "answer", label = question, input = true,
                    values = options.Select(o => new { label = o.Text, value = o.Key }).ToArray(),
                },
            }
        });
        var answerKeyJson = JsonSerializer.Serialize(new Dictionary<string, IReadOnlyList<string>> { ["answer"] = correctTexts });
        var scoringRulesJson = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.MultipleChoice, CorrectAnswers: correctKeys, Points: 1.0) }));
        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            correctFeedback = "Correct!",
            incorrectFeedback = $"Not quite — the correct answers were: {string.Join(", ", correctTexts)}.",
        });

        return (question, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    /// <summary>Phase K17 — the correct answer is <paramref name="correctWord"/>, picked
    /// deterministically by <see cref="ActivityGenerationService.PickBlankWord"/> before any AI
    /// call — AI only supplies the wrong-word <paramref name="distractors"/>. Shows the transcript
    /// with the word already blanked (computed by PickBlankWord), radio options in the same shape
    /// as multiple_choice_single.</summary>
    private static (string Instructions, string FormSchemaJson, string AnswerKeyJson, string ScoringRulesJson, string FeedbackPlanJson)
        ComposeSelectMissingWord(string transcriptWithBlank, string correctWord, IReadOnlyList<string> distractors)
    {
        var options = new List<(string Key, string Text)> { ("opt_0", correctWord) };
        for (var i = 0; i < distractors.Count; i++)
            options.Add(($"opt_{i + 1}", distractors[i]));

        var instructions = "Read the transcript below and select the word that completes the blank.";
        var formSchemaJson = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "passage", html = $"<p>{System.Net.WebUtility.HtmlEncode(transcriptWithBlank)}</p>" },
                new
                {
                    type = "radio", key = "answer", label = "Select the missing word", input = true,
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
            incorrectFeedback = $"Not quite — the correct word was: \"{options[0].Text}\".",
        });

        return (instructions, formSchemaJson, answerKeyJson, scoringRulesJson, feedbackPlanJson);
    }

    private static string Truncate(string value) =>
        value.Length <= MaxResourceTextLength ? value : value[..MaxResourceTextLength];

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

    private static string CleanJson(string raw)
    {
        var cleaned = raw.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                cleaned = cleaned[(firstNewline + 1)..lastFence].Trim();
        }
        return cleaned;
    }

    private sealed record AiExerciseOutput(
        string? PromptText, List<string> Distractors, string? CorrectAnswerText = null, List<string>? CorrectAnswersText = null);

    /// <summary>
    /// Fully defensive JSON-&gt;output parsing plus per-activityType safety validation. Returns
    /// null (with <paramref name="parseError"/> set) on unparseable JSON, or when the output
    /// violates a type-specific safety rule — most importantly, for "gap_fill", when the AI
    /// sentence doesn't contain the blank marker or leaks the answer term outside the blank. A
    /// caller must never use an unvalidated result.
    /// </summary>
    private static AiExerciseOutput? TryParseAndValidateOutput(
        string rawResponse, string activityType, LessonResourceSnapshot primary, string? knownCorrectAnswer, out string? parseError)
    {
        parseError = null;
        var cleaned = CleanJson(rawResponse);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(cleaned);
        }
        catch (JsonException ex)
        {
            parseError = $"Response is not valid JSON: {ex.Message}";
            return null;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                parseError = "Response is not a JSON object.";
                return null;
            }

            var root = doc.RootElement;
            var promptText = GetString(root, "promptText");
            var distractors = GetStringArray(root, "distractors")
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (activityType == ActivityGenerationService.ActivityTypeGapFill)
            {
                if (string.IsNullOrWhiteSpace(promptText))
                {
                    parseError = "Response is missing a non-empty 'promptText' for a gap_fill sentence.";
                    return null;
                }
                if (!promptText.Contains(BlankMarker, StringComparison.Ordinal))
                {
                    parseError = $"promptText does not contain the required blank marker '{BlankMarker}'.";
                    return null;
                }
                var withoutBlank = promptText.Replace(BlankMarker, "", StringComparison.Ordinal);
                if (withoutBlank.Contains(primary.Title, StringComparison.OrdinalIgnoreCase))
                {
                    parseError = "promptText leaks the answer term outside the blank marker.";
                    return null;
                }
                return new AiExerciseOutput(promptText.Trim(), new List<string>());
            }

            if (activityType == ActivityGenerationService.ActivityTypeMultipleChoiceSingle)
            {
                var correctText = primary.Body?.Trim() ?? "";
                var filtered = distractors
                    .Where(d => !string.Equals(d, correctText, StringComparison.OrdinalIgnoreCase))
                    .Take(MaxDistractors)
                    .ToList();
                if (filtered.Count == 0)
                {
                    parseError = "Response contained no usable distractors (empty, or all matched the correct answer).";
                    return null;
                }
                return new AiExerciseOutput(promptText, filtered);
            }

            if (activityType == ActivityGenerationService.ActivityTypeReadingMultipleChoiceSingle
                || activityType == ActivityGenerationService.ActivityTypeListeningMultipleChoiceSingle
                || activityType == ActivityGenerationService.ActivityTypeHighlightCorrectSummary)
            {
                if (string.IsNullOrWhiteSpace(promptText))
                {
                    parseError = "Response is missing a non-empty 'promptText' for the comprehension question.";
                    return null;
                }
                var correctAnswerText = GetString(root, "correctAnswerText")?.Trim();
                if (string.IsNullOrWhiteSpace(correctAnswerText))
                {
                    parseError = "Response is missing a non-empty 'correctAnswerText'.";
                    return null;
                }
                var filtered = distractors
                    .Where(d => !string.Equals(d, correctAnswerText, StringComparison.OrdinalIgnoreCase))
                    .Take(MaxDistractors)
                    .ToList();
                if (filtered.Count == 0)
                {
                    parseError = "Response contained no usable distractors (empty, or all matched the correct answer).";
                    return null;
                }
                return new AiExerciseOutput(promptText.Trim(), filtered, correctAnswerText);
            }

            if (activityType == ActivityGenerationService.ActivityTypeReadingMultipleChoiceMulti
                || activityType == ActivityGenerationService.ActivityTypeListeningMultipleChoiceMulti)
            {
                if (string.IsNullOrWhiteSpace(promptText))
                {
                    parseError = "Response is missing a non-empty 'promptText' for the comprehension question.";
                    return null;
                }
                var correctAnswersText = GetStringArray(root, "correctAnswersText")
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Select(a => a.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (correctAnswersText.Count < 2)
                {
                    parseError = "Response must contain at least 2 distinct non-empty 'correctAnswersText' entries.";
                    return null;
                }
                var filteredMulti = distractors
                    .Where(d => !correctAnswersText.Contains(d, StringComparer.OrdinalIgnoreCase))
                    .Take(MaxDistractors)
                    .ToList();
                if (filteredMulti.Count == 0)
                {
                    parseError = "Response contained no usable distractors (empty, or all matched a correct answer).";
                    return null;
                }
                return new AiExerciseOutput(promptText.Trim(), filteredMulti, CorrectAnswersText: correctAnswersText);
            }

            if (activityType == ActivityGenerationService.ActivityTypeSelectMissingWord)
            {
                // promptText/correctAnswerText are irrelevant here — the correct answer is
                // knownCorrectAnswer (computed deterministically before the AI call, see
                // ActivityGenerationService.PickBlankWord). Only distractors matter.
                var filteredWords = distractors
                    .Where(d => !string.Equals(d, knownCorrectAnswer, StringComparison.OrdinalIgnoreCase))
                    .Take(MaxDistractors)
                    .ToList();
                if (filteredWords.Count == 0)
                {
                    parseError = "Response contained no usable word distractors (empty, or all matched the correct word).";
                    return null;
                }
                return new AiExerciseOutput(null, filteredWords);
            }

            // short_answer
            if (string.IsNullOrWhiteSpace(promptText))
            {
                parseError = "Response is missing a non-empty 'promptText' for the comprehension question.";
                return null;
            }
            return new AiExerciseOutput(promptText.Trim(), new List<string>());
        }
    }

    private static string? GetString(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static IReadOnlyList<string> GetStringArray(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                list.Add(item.GetString()!.Trim());
        }
        return list;
    }
}
