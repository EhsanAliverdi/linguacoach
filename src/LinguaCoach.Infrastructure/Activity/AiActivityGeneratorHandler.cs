using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Implements IAiActivityGenerator for supported AI-generated activity types.
/// </summary>
public sealed class AiActivityGeneratorHandler : IAiActivityGenerator
{
    private const string GenerateWritingPromptKey = "activity_generate_writing";
    private const string GenerateListeningPromptKey = "activity_generate_listening";
    private const string EvaluateWritingPromptKey = "activity_evaluate_writing";

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
        if (context.ActivityType is not ActivityType.WritingScenario and not ActivityType.ListeningComprehension)
            throw new NotSupportedException(
                $"AI generation for {context.ActivityType} is not implemented in this sprint.");

        var variables = new Dictionary<string, string>
        {
            ["cefrLevel"] = context.CefrLevel,
            ["careerContext"] = context.CareerContext,
            ["sourceLanguageName"] = context.SourceLanguageName,
            ["targetLanguageName"] = context.TargetLanguageName,
            ["recentMistakes"] = context.RecentMistakesSummary ?? "none",
            ["topicHint"] = context.TopicHint ?? "workplace communication",
        };

        var promptKey = context.ActivityType == ActivityType.ListeningComprehension
            ? GenerateListeningPromptKey
            : GenerateWritingPromptKey;

        var aiRequest = await _contextBuilder.BuildAsync(promptKey, variables, ct);

        var response = await _aiExecution.ExecuteWithFallbackAsync(
            promptKey, aiRequest, studentProfileId: null, correlationId: null, ct);

        var cleaned = CleanJson(response);
        if (context.ActivityType == ActivityType.ListeningComprehension)
            ValidateListeningActivityJson(cleaned);
        else
            ValidateWritingActivityJson(cleaned);
        return cleaned;
    }

    public async Task<string> EvaluateAttemptAsync(
        ActivityEvaluationContext context,
        CancellationToken ct = default)
    {
        if (context.ActivityType != ActivityType.WritingScenario)
            throw new NotSupportedException(
                $"AI evaluation for {context.ActivityType} is not implemented in this sprint.");

        var variables = new Dictionary<string, string>
        {
            ["activityContent"] = context.ActivityContentJson,
            ["studentSubmission"] = context.StudentSubmission,
            ["cefrLevel"] = context.CefrLevel,
            ["careerContext"] = context.CareerContext,
            ["sourceLanguageName"] = context.SourceLanguageName,
            ["targetLanguageName"] = context.TargetLanguageName,
        };

        var aiRequest = await _contextBuilder.BuildAsync(EvaluateWritingPromptKey, variables, ct);

        var response = await _aiExecution.ExecuteWithFallbackAsync(
            EvaluateWritingPromptKey, aiRequest, studentProfileId: null, correlationId: null, ct);

        return CleanJson(response);
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

    private static void ValidateListeningActivityJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("audioScript", out var script) || script.ValueKind != JsonValueKind.String)
            throw new AiResponseValidationException("AI listening activity response missing audioScript.");
        if (!root.TryGetProperty("questions", out var questions) || questions.ValueKind != JsonValueKind.Array || questions.GetArrayLength() == 0)
            throw new AiResponseValidationException("AI listening activity response missing questions.");
    }
}
