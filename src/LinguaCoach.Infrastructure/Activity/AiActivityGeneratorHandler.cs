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

        var variables = new Dictionary<string, string>
        {
            ["cefrLevel"] = context.CefrLevel,
            ["careerContext"] = context.CareerContext,
            ["sourceLanguageName"] = context.SourceLanguageName,
            ["targetLanguageName"] = context.TargetLanguageName,
            ["recentMistakes"] = context.RecentMistakesSummary ?? "none",
            ["topicHint"] = context.TopicHint ?? "workplace communication",
        };

        var pair = _aiProviderResolver.ResolveWithFallback("activity_generate_writing");
        var aiRequest = await _contextBuilder.BuildAsync(GenerateWritingPromptKey, variables, ct);

        var response = await ExecuteWithFallbackAsync(
            pair, aiRequest, GenerateWritingPromptKey, studentProfileId: null, ct);

        var cleaned = CleanJson(response);
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

        var pair = _aiProviderResolver.ResolveWithFallback("activity_evaluate_writing");
        var aiRequest = await _contextBuilder.BuildAsync(EvaluateWritingPromptKey, variables, ct);

        var response = await ExecuteWithFallbackAsync(
            pair, aiRequest, EvaluateWritingPromptKey, studentProfileId: null, ct);

        return CleanJson(response);
    }

    private async Task<string> ExecuteWithFallbackAsync(
        Application.Ai.AiProviderPair pair,
        Application.Ai.AiRequest baseRequest,
        string featureKey,
        Guid? studentProfileId,
        CancellationToken ct)
    {
        // Try primary
        var primaryRequest = baseRequest with
        {
            ModelHint = pair.Primary.ModelName,
            ApiKeyOverride = pair.Primary.ApiKeyOverride,
        };

        try
        {
            var resp = await pair.Primary.Provider.CompleteAsync(primaryRequest, ct);
            await LogUsageAsync(resp, pair.Primary.ProviderName, featureKey, isFallback: false,
                wasSuccessful: true, failureReason: null, studentProfileId, ct);
            return resp.ResponseJson;
        }
        catch (Exception primaryEx)
        {
            _logger.LogWarning(primaryEx,
                "Primary AI provider failed FeatureKey={Feature} Provider={Provider} Model={Model} ExType={ExType}",
                featureKey, pair.Primary.ProviderName, pair.Primary.ModelName, primaryEx.GetType().Name);

            await LogUsageAsync(
                new Application.Ai.AiResponse("", 0, 0, 0m, pair.Primary.ModelName, pair.Primary.ProviderName),
                pair.Primary.ProviderName, featureKey, isFallback: false,
                wasSuccessful: false, failureReason: primaryEx.GetType().Name, studentProfileId, ct);

            if (pair.Fallback is null)
                throw new AiUnavailableException(
                    $"AI provider '{pair.Primary.ProviderName}' failed and no fallback is configured.", primaryEx);
        }

        // Try fallback
        var fallbackRequest = baseRequest with
        {
            ModelHint = pair.Fallback!.ModelName,
            ApiKeyOverride = pair.Fallback.ApiKeyOverride,
        };

        try
        {
            _logger.LogInformation(
                "Attempting fallback provider FeatureKey={Feature} FallbackProvider={Provider} FallbackModel={Model}",
                featureKey, pair.Fallback.ProviderName, pair.Fallback.ModelName);

            var fallbackResp = await pair.Fallback.Provider.CompleteAsync(fallbackRequest, ct);
            await LogUsageAsync(fallbackResp, pair.Fallback.ProviderName, featureKey, isFallback: true,
                wasSuccessful: true, failureReason: null, studentProfileId, ct);
            return fallbackResp.ResponseJson;
        }
        catch (Exception fallbackEx)
        {
            _logger.LogError(fallbackEx,
                "Fallback AI provider also failed FeatureKey={Feature} FallbackProvider={Provider} ExType={ExType}",
                featureKey, pair.Fallback.ProviderName, fallbackEx.GetType().Name);

            await LogUsageAsync(
                new Application.Ai.AiResponse("", 0, 0, 0m, pair.Fallback.ModelName, pair.Fallback.ProviderName),
                pair.Fallback.ProviderName, featureKey, isFallback: true,
                wasSuccessful: false, failureReason: fallbackEx.GetType().Name, studentProfileId, ct);

            throw new AiUnavailableException(
                $"All AI providers failed for feature '{featureKey}'.", fallbackEx);
        }
    }

    private async Task LogUsageAsync(
        Application.Ai.AiResponse aiResponse,
        string providerName,
        string featureKey,
        bool isFallback,
        bool wasSuccessful,
        string? failureReason,
        Guid? studentProfileId,
        CancellationToken ct,
        long durationMs = 0)
    {
        try
        {
            var modelName = string.IsNullOrEmpty(aiResponse.ModelName) ? "unknown" : aiResponse.ModelName;
            var usedProvider = string.IsNullOrWhiteSpace(aiResponse.ProviderName) ? providerName : aiResponse.ProviderName;

            var usageLog = new Domain.Entities.AiUsageLog(
                studentProfileId == Guid.Empty ? null : studentProfileId,
                featureKey,
                usedProvider,
                modelName,
                isFallback,
                wasSuccessful,
                failureReason,
                aiResponse.InputTokens,
                aiResponse.OutputTokens,
                aiResponse.CostUsd,
                durationMs,
                correlationId: null); // correlation ID injected in future if needed

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
