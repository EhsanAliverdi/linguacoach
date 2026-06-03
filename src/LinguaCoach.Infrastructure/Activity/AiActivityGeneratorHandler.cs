using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Implements IAiActivityGenerator for WritingScenario activities.
/// Other ActivityTypes will add their generation logic here in future sprints.
/// </summary>
public sealed class AiActivityGeneratorHandler : IAiActivityGenerator
{
    private const string GenerateWritingPromptKey = "activity_generate_writing";
    private const string EvaluateWritingPromptKey = "activity_evaluate_writing";

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly IAiProviderResolver _aiProviderResolver;
    private readonly ILogger<AiActivityGeneratorHandler> _logger;

    public AiActivityGeneratorHandler(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        IAiProviderResolver aiProviderResolver,
        ILogger<AiActivityGeneratorHandler> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiProviderResolver = aiProviderResolver;
        _logger = logger;
    }

    public async Task<string> GenerateActivityContentAsync(
        ActivityGenerationContext context,
        CancellationToken ct = default)
    {
        if (context.ActivityType != ActivityType.WritingScenario)
            throw new NotSupportedException(
                $"AI generation for {context.ActivityType} is not implemented in this sprint.");

        var promptKey = GenerateWritingPromptKey;
        var variables = new Dictionary<string, string>
        {
            ["cefrLevel"] = context.CefrLevel,
            ["careerContext"] = context.CareerContext,
            ["sourceLanguageName"] = context.SourceLanguageName,
            ["targetLanguageName"] = context.TargetLanguageName,
            ["recentMistakes"] = context.RecentMistakesSummary ?? "none",
            ["topicHint"] = context.TopicHint ?? "workplace communication",
        };

        var selection = _aiProviderResolver.ResolveWritingFeedbackProvider();
        var aiRequest = await _contextBuilder.BuildAsync(promptKey, variables, ct);
        aiRequest = aiRequest with { ModelHint = selection.ModelName, ApiKeyOverride = selection.ApiKeyOverride };

        var aiResponse = await selection.Provider.CompleteAsync(aiRequest, ct);

        await LogUsageAsync(aiResponse, selection.ProviderName, studentProfileId: null, ct);

        var cleaned = CleanJson(aiResponse.ResponseJson);
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

        var promptKey = EvaluateWritingPromptKey;
        var variables = new Dictionary<string, string>
        {
            ["activityContent"] = context.ActivityContentJson,
            ["studentSubmission"] = context.StudentSubmission,
            ["cefrLevel"] = context.CefrLevel,
            ["careerContext"] = context.CareerContext,
            ["sourceLanguageName"] = context.SourceLanguageName,
            ["targetLanguageName"] = context.TargetLanguageName,
        };

        var selection = _aiProviderResolver.ResolveWritingFeedbackProvider();
        var aiRequest = await _contextBuilder.BuildAsync(promptKey, variables, ct);
        aiRequest = aiRequest with { ModelHint = selection.ModelName, ApiKeyOverride = selection.ApiKeyOverride };

        var aiResponse = await selection.Provider.CompleteAsync(aiRequest, ct);

        await LogUsageAsync(aiResponse, selection.ProviderName, studentProfileId: null, ct);

        return CleanJson(aiResponse.ResponseJson);
    }

    private async Task LogUsageAsync(
        Application.Ai.AiResponse aiResponse,
        string providerName,
        Guid? studentProfileId,
        CancellationToken ct)
    {
        if (studentProfileId is null || studentProfileId == Guid.Empty)
        {
            // Generation context has no profile — log telemetry only, no DB row
            _logger.LogDebug("AI usage: {Provider}, in={In}, out={Out}, cost={Cost:F6}",
                providerName, aiResponse.InputTokens, aiResponse.OutputTokens, aiResponse.CostUsd);
            return;
        }

        try
        {
            var modelName = string.IsNullOrEmpty(aiResponse.ModelName) ? "unknown" : aiResponse.ModelName;
            var usedProvider = string.IsNullOrWhiteSpace(aiResponse.ProviderName) ? providerName : aiResponse.ProviderName;
            var usageLog = new Domain.Entities.AiUsageLog(
                studentProfileId.Value,
                usedProvider,
                modelName,
                aiResponse.InputTokens,
                aiResponse.OutputTokens,
                aiResponse.CostUsd);
            _db.AiUsageLogs.Add(usageLog);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log AI usage. Non-fatal.");
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
}
