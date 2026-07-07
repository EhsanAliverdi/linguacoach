using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Implements IAiActivityGenerator for supported AI-generated activity types.
/// </summary>
public sealed class AiActivityGeneratorHandler : IAiActivityGenerator
{
    private static readonly HashSet<string> StagedPatternKeys = new(StringComparer.Ordinal)
    {
        "phrase_match",
        "gap_fill_workplace_phrase",
        "listen_and_answer",
        "listen_and_gap_fill",
        "spoken_response_from_prompt",
        "speaking_roleplay_turn",
        "lesson_reflection",
        "reading_multiple_choice_single",
        "reading_multiple_choice_multi",
        "reading_fill_in_blanks",
        "reorder_paragraphs",
        "reading_writing_fill_in_blanks",
        "summarize_written_text",
        "write_essay",
        "listening_multiple_choice_single",
        "listening_multiple_choice_multi",
        "listening_fill_in_blanks",
        "select_missing_word",
        "highlight_correct_summary",
        "highlight_incorrect_words",
        "write_from_dictation",
        "summarize_spoken_text",
        "answer_short_question",
        "read_aloud",
        "repeat_sentence",
        "respond_to_situation",
        "describe_image",
        "retell_lecture",
        "summarize_group_discussion",
    };

    private const string GenerateWritingPromptKey = "activity_generate_writing";
    private const string GenerateListeningPromptKey = "activity_generate_listening";
    private const string GenerateSpeakingRolePlayPromptKey = "activity_generate_speaking_roleplay";
    private const string EvaluateWritingPromptKey = "activity_evaluate_writing";
    private const string EvaluateSpeakingRolePlayPromptKey = SpeakingRolePlayEvaluator.EvaluatePromptKey;

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<AiActivityGeneratorHandler> _logger;

    public AiActivityGeneratorHandler(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        ILogger<AiActivityGeneratorHandler> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public async Task<string> GenerateActivityContentAsync(
        ActivityGenerationContext context,
        CancellationToken ct = default)
    {
        // VocabularyPractice is supported when driven by a pattern's OverridePromptKey.
        // Bare VocabularyPractice (no override) is handled by VocabularyPracticeGenerator, not here.
        var isPatternDriven = !string.IsNullOrWhiteSpace(context.OverridePromptKey);
        if (context.ActivityType is not ActivityType.WritingScenario
            and not ActivityType.ListeningComprehension
            and not ActivityType.SpeakingRolePlay
            and not ActivityType.ReadingTask
            && !(context.ActivityType == ActivityType.VocabularyPractice && isPatternDriven))
            throw new NotSupportedException(
                $"AI generation for {context.ActivityType} is not yet implemented.");

        var variables = new Dictionary<string, string>
        {
            ["cefrLevel"] = context.CefrLevel,
            ["careerContext"] = context.CareerContext,
            ["sourceLanguageName"] = context.SourceLanguageName,
            ["targetLanguageName"] = context.TargetLanguageName,
            ["recentMistakes"] = context.RecentMistakesSummary ?? "none",
            ["topicHint"] = context.TopicHint ?? "everyday real-life communication",
            ["learnerPreferences"] = context.LearnerPreferenceContext ?? string.Empty,
            ["routingContext"] = context.RoutingContext ?? string.Empty,
            ["routingReason"] = context.RoutingReason ?? "normal",
        };

        // Phase 8N: configurable per-format item/option counts.
        // Looked up by pattern key so prompts can reference target counts.
        var countSettings = await LoadCountSettingsAsync(context.ExercisePatternKey, ct);
        if (countSettings is not null)
        {
            variables["minItemsPerPractice"] = countSettings.Value.MinItems.ToString();
            variables["defaultItemsPerPractice"] = countSettings.Value.DefItems.ToString();
            variables["maxItemsPerPractice"] = countSettings.Value.MaxItems.ToString();
            variables["minOptionsPerItem"] = countSettings.Value.MinOpts.ToString();
            variables["defaultOptionsPerItem"] = countSettings.Value.DefOpts.ToString();
            variables["maxOptionsPerItem"] = countSettings.Value.MaxOpts.ToString();
        }

        // Pattern-aware: use the override prompt key from the ExercisePatternDefinition if provided.
        // Fall back to legacy broad ActivityType routing otherwise.
        var promptKey = !string.IsNullOrWhiteSpace(context.OverridePromptKey)
            ? context.OverridePromptKey
            : context.ActivityType switch
            {
                ActivityType.ListeningComprehension => GenerateListeningPromptKey,
                ActivityType.SpeakingRolePlay       => GenerateSpeakingRolePlayPromptKey,
                _                                   => GenerateWritingPromptKey,
            };

        var aiRequest = await _contextBuilder.BuildAsync(promptKey, variables, ct);

        var correlationId = Guid.NewGuid().ToString("N")[..16];
        var result = await _aiExecution.ExecuteWithMetaAsync(
            promptKey, aiRequest, studentProfileId: context.StudentProfileId, correlationId: correlationId, ct);

        var cleaned = CleanJson(result.ResponseJson);
        switch (context.ActivityType)
        {
            case ActivityType.ListeningComprehension:
            case ActivityType.WritingScenario:
            case ActivityType.SpeakingRolePlay:
            case ActivityType.ReadingTask:
            {
                var check = TryValidateStagedContent(cleaned, context.ActivityType, context.ExercisePatternKey, countSettings);
                if (!check.IsValid)
                {
                    await LogValidationFailureAsync(context, check.Errors, attemptNumber: 1, correlationId, result, ct);
                    var retryResult = await _aiExecution.ExecuteWithMetaAsync(
                        promptKey, aiRequest, studentProfileId: context.StudentProfileId, correlationId: correlationId, ct);
                    cleaned = CleanJson(retryResult.ResponseJson);
                    var retryCheck = TryValidateStagedContent(cleaned, context.ActivityType, context.ExercisePatternKey, countSettings);
                    if (!retryCheck.IsValid)
                    {
                        await LogValidationFailureAsync(context, retryCheck.Errors, attemptNumber: 2, correlationId, retryResult, ct);
                        throw new AiResponseValidationException(
                            $"AI staged activity failed validation after retry: {string.Join("; ", retryCheck.Errors)}");
                    }
                }
                break;
            }
            case ActivityType.VocabularyPractice when isPatternDriven:
            {
                var isStaged = StagedPatternKeys.Contains(context.ExercisePatternKey ?? string.Empty);
                var check = isStaged
                    ? TryValidateStagedContent(cleaned, context.ActivityType, context.ExercisePatternKey, countSettings)
                    : TryValidateJsonOnly(cleaned);
                if (!check.IsValid)
                {
                    await LogValidationFailureAsync(context, check.Errors, attemptNumber: 1, correlationId, result, ct);
                    var retryResult = await _aiExecution.ExecuteWithMetaAsync(
                        promptKey, aiRequest, studentProfileId: context.StudentProfileId, correlationId: correlationId, ct);
                    cleaned = CleanJson(retryResult.ResponseJson);
                    var retryCheck = isStaged
                        ? TryValidateStagedContent(cleaned, context.ActivityType, context.ExercisePatternKey, countSettings)
                        : TryValidateJsonOnly(cleaned);
                    if (!retryCheck.IsValid)
                    {
                        await LogValidationFailureAsync(context, retryCheck.Errors, attemptNumber: 2, correlationId, retryResult, ct);
                        throw new AiResponseValidationException(
                            $"AI staged activity failed validation after retry: {string.Join("; ", retryCheck.Errors)}");
                    }
                }
                break;
            }
            default:
                ValidateWritingActivityJson(cleaned);
                break;
        }
        return cleaned;
    }

    public async Task<string> EvaluateAttemptAsync(
        ActivityEvaluationContext context,
        CancellationToken ct = default)
    {
        if (context.ActivityType is not ActivityType.WritingScenario
            and not ActivityType.SpeakingRolePlay)
            throw new NotSupportedException(
                $"AI evaluation for {context.ActivityType} is not yet implemented.");

        var variables = new Dictionary<string, string>
        {
            ["activityContent"] = context.ActivityType == ActivityType.WritingScenario
                ? BuildWritingEvaluationContent(context.ActivityContentJson)
                : context.ActivityType == ActivityType.SpeakingRolePlay
                    ? BuildSpeakingEvaluationContent(context.ActivityContentJson)
                    : context.ActivityContentJson,
            ["studentSubmission"] = context.StudentSubmission,
            ["cefrLevel"] = context.CefrLevel,
            ["careerContext"] = context.CareerContext,
            ["sourceLanguageName"] = context.SourceLanguageName,
            ["targetLanguageName"] = context.TargetLanguageName,
            ["learnerPreferences"] = context.LearnerPreferenceContext ?? string.Empty,
            ["learningGoalContext"] = context.LearningGoalContext ?? string.Empty,
        };

        var evalPromptKey = context.ActivityType == ActivityType.SpeakingRolePlay
            ? EvaluateSpeakingRolePlayPromptKey
            : EvaluateWritingPromptKey;

        var aiRequest = await _contextBuilder.BuildAsync(evalPromptKey, variables, ct);

        var response = await _aiExecution.ExecuteAsync(
            evalPromptKey, aiRequest, studentProfileId: null, correlationId: null, ct);

        return CleanJson(response);
    }


    private static string BuildWritingEvaluationContent(string contentJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("schemaVersion", out var sv)
                || sv.GetString() != ModuleStageSchema.Version)
                return contentJson;

            var payload = new
            {
                schemaVersion = ModuleStageSchema.Version,
                practiceContent = root.TryGetProperty("practiceContent", out var practice)
                    ? JsonSerializer.Deserialize<object>(practice.GetRawText())
                    : null,
                feedbackPlan = root.TryGetProperty("feedbackPlan", out var feedbackPlan)
                    ? JsonSerializer.Deserialize<object>(feedbackPlan.GetRawText())
                    : null,
                learnContent = root.TryGetProperty("learnContent", out var learn)
                    ? JsonSerializer.Deserialize<object>(learn.GetRawText())
                    : null,
            };
            return JsonSerializer.Serialize(payload);
        }
        catch
        {
            return contentJson;
        }
    }

    private static void ValidateIsJson(string json)
    {
        try { JsonDocument.Parse(json); }
        catch (JsonException ex)
        {
            throw new AiResponseValidationException($"AI response is not valid JSON: {ex.Message}");
        }
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

    private static void ValidateWritingActivityJson(string json)
    {
        // Validate the JSON has the expected writing activity shape.
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("situation", out _) && !root.TryGetProperty("learningGoal", out _))
            throw new AiResponseValidationException(
                "AI writing activity response missing required fields (situation, learningGoal).");
    }

    private static string BuildSpeakingEvaluationContent(string contentJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("schemaVersion", out var sv)
                || sv.GetString() != ModuleStageSchema.Version)
                return contentJson;

            var payload = new
            {
                schemaVersion = ModuleStageSchema.Version,
                practiceContent = root.TryGetProperty("practiceContent", out var practice)
                    ? JsonSerializer.Deserialize<object>(practice.GetRawText())
                    : null,
                feedbackPlan = root.TryGetProperty("feedbackPlan", out var feedbackPlan)
                    ? JsonSerializer.Deserialize<object>(feedbackPlan.GetRawText())
                    : null,
                learnContent = root.TryGetProperty("learnContent", out var learn)
                    ? JsonSerializer.Deserialize<object>(learn.GetRawText())
                    : null,
            };
            return JsonSerializer.Serialize(payload);
        }
        catch
        {
            return contentJson;
        }
    }

    private async Task<(int MinItems, int DefItems, int MaxItems, int MinOpts, int DefOpts, int MaxOpts)?> LoadCountSettingsAsync(
        string? exercisePatternKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(exercisePatternKey))
            return null;
        var def = await _db.ExerciseTypeDefinitions
            .AsNoTracking()
            .Where(e => e.ExercisePatternKey == exercisePatternKey)
            .Select(e => new { e.MinItemsPerPractice, e.DefaultItemsPerPractice, e.MaxItemsPerPractice, e.MinOptionsPerItem, e.DefaultOptionsPerItem, e.MaxOptionsPerItem })
            .FirstOrDefaultAsync(ct);
        return def is null
            ? null
            : (def.MinItemsPerPractice, def.DefaultItemsPerPractice, def.MaxItemsPerPractice, def.MinOptionsPerItem, def.DefaultOptionsPerItem, def.MaxOptionsPerItem);
    }

    private static ValidationResult ValidateStagedContent(
        string json,
        ActivityType activityType,
        string? exercisePatternKey = null,
        (int MinItems, int DefItems, int MaxItems, int MinOpts, int DefOpts, int MaxOpts)? counts = null)
    {
        using var doc = JsonDocument.Parse(json);
        var countSettings = counts is null
            ? null
            : new PracticeCountSettings(counts.Value.MinItems, counts.Value.MaxItems, counts.Value.MinOpts, counts.Value.MaxOpts);
        return ModuleStageContentValidator.Validate(doc.RootElement, activityType, exercisePatternKey, countSettings);
    }

    /// <summary>Same as <see cref="ValidateStagedContent"/> but folds a malformed-JSON response into
    /// the returned ValidationResult instead of throwing — LLMs occasionally emit invalid JSON (e.g. a
    /// trailing comma), and that failure needs to flow through the same retry-once path as a semantic
    /// content-validation failure rather than aborting the request on the first attempt.</summary>
    private static ValidationResult TryValidateStagedContent(
        string json,
        ActivityType activityType,
        string? exercisePatternKey,
        (int MinItems, int DefItems, int MaxItems, int MinOpts, int DefOpts, int MaxOpts)? counts)
    {
        try
        {
            return ValidateStagedContent(json, activityType, exercisePatternKey, counts);
        }
        catch (JsonException ex)
        {
            return new ValidationResult(false, new[] { $"AI response is not valid JSON: {ex.Message}" });
        }
    }

    /// <summary>JSON-only counterpart of <see cref="TryValidateStagedContent"/>, for activity types that
    /// don't run staged-content validation but still need a malformed-JSON response to retry rather than throw.</summary>
    private static ValidationResult TryValidateJsonOnly(string json)
    {
        try
        {
            ValidateIsJson(json);
            return new ValidationResult(true, Array.Empty<string>());
        }
        catch (AiResponseValidationException ex)
        {
            return new ValidationResult(false, new[] { ex.Message });
        }
    }

    private async Task LogValidationFailureAsync(
        ActivityGenerationContext context,
        IReadOnlyList<string> errors,
        int attemptNumber,
        string? correlationId,
        AiExecutionResult? executionResult,
        CancellationToken ct)
    {
        try
        {
            var failure = new GenerationValidationFailure(
                activityTypeName: context.ActivityType.ToString(),
                validationErrors: string.Join("; ", errors),
                attemptNumber: attemptNumber,
                patternKey: context.ExercisePatternKey,
                cefrLevel: context.CefrLevel,
                objectiveKey: context.ObjectiveKey,
                providerName: executionResult?.ProviderName,
                modelName: executionResult?.ModelName,
                correlationId: correlationId,
                studentProfileId: context.StudentProfileId);

            _db.GenerationValidationFailures.Add(failure);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist generation validation failure record (non-blocking).");
        }
    }
}
