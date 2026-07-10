using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Implements IAiActivityGenerator's remaining EvaluateAttemptAsync method.
/// Phase I2C: narrowed from generate+evaluate to evaluate-only — see the interface's doc
/// comment and docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md. The dead
/// generation-only helpers (staged-pattern validation, per-format count settings lookup,
/// validation-failure logging) were removed alongside GenerateActivityContentAsync.
/// </summary>
public sealed class AiActivityGeneratorHandler : IAiActivityGenerator
{
    private const string EvaluateWritingPromptKey = "activity_evaluate_writing";
    private const string EvaluateSpeakingRolePlayPromptKey = SpeakingRolePlayEvaluator.EvaluatePromptKey;

    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;

    public AiActivityGeneratorHandler(
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution)
    {
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
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
}
