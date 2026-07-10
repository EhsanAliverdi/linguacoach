using LinguaCoach.Application.Ai;
using LinguaCoach.Application.UsageGovernance;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Ai;

/// <summary>AI execution result enriched with provider and model metadata.</summary>
public sealed record AiExecutionResult(string ResponseJson, string ProviderName, string ModelName, bool IsFallback);

public sealed class AiExecutionService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IAiProviderResolver _resolver;
    private readonly IUsageQuotaService _quota;
    private readonly ILogger<AiExecutionService> _logger;

    // Feature keys that require a pre-call quota check
    private static readonly HashSet<string> QuotaCheckedFeatures = new(StringComparer.OrdinalIgnoreCase)
    {
        "writing.evaluate",
        "speaking.evaluate",
        "tts.generate",
        "practice.dynamic.generate",
        "lesson.generate",
        "lesson.regenerate",
        "learning_path.generate",
        "learning_path.regenerate",
    };

    // Mapping from internal AI feature keys to governance feature keys
    private static readonly Dictionary<string, string> GovernanceKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["activity_evaluate_writing"] = "writing.evaluate",
        ["writing.exercise"] = "writing.evaluate",
        ["writing.exercise.v2"] = "writing.evaluate",
        ["activity_evaluate_speaking_roleplay"] = "speaking.evaluate",
        ["activity_evaluate_lesson_reflection"] = "speaking.evaluate",
        ["learning_path_generate"] = "learning_path.generate",
        ["learning_path_generate_adaptive"] = "learning_path.regenerate",
    };

    public AiExecutionService(
        LinguaCoachDbContext db,
        IAiProviderResolver resolver,
        IUsageQuotaService quota,
        ILogger<AiExecutionService> logger)
    {
        _db = db;
        _resolver = resolver;
        _quota = quota;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string featureKey,
        AiRequest baseRequest,
        Guid? studentProfileId,
        string? correlationId,
        CancellationToken ct)
    {
        var governanceKey = ResolveGovernanceKey(featureKey);

        // Pre-call quota check for expensive features when a student is known
        if (studentProfileId.HasValue && studentProfileId != Guid.Empty
            && QuotaCheckedFeatures.Contains(governanceKey))
        {
            var decision = await _quota.CheckAsync(studentProfileId.Value, governanceKey, estimatedUnits: 1, ct: ct);
            if (!decision.Allowed)
                throw new QuotaExceededException(decision);
        }

        var pair = _resolver.ResolveLlm(featureKey, ResolveLlmCategory(featureKey));
        var started = DateTime.UtcNow;
        AiResponse? successResponse = null;

        try
        {
            successResponse = await ExecuteOneInternalAsync(pair.Primary, baseRequest, featureKey, false, studentProfileId, correlationId, ct);
        }
        catch (Exception primaryEx) when (primaryEx is not AiUnavailableException and not QuotaExceededException)
        {
            _logger.LogWarning(primaryEx,
                "Primary AI provider failed FeatureKey={FeatureKey} Provider={Provider} Model={Model} CorrelationId={CorrelationId}",
                featureKey, pair.Primary.ProviderName, pair.Primary.ModelName, correlationId);

            await LogUsageAsync(new AiResponse("", 0, 0, 0m, pair.Primary.ModelName, pair.Primary.ProviderName),
                featureKey, false, false, primaryEx.GetType().Name, studentProfileId, correlationId, ct,
                (long)(DateTime.UtcNow - started).TotalMilliseconds);

            if (pair.Fallback is null)
                throw new AiUnavailableException($"AI provider '{pair.Primary.ProviderName}' failed and no fallback is configured.", primaryEx);
        }

        if (successResponse is null)
        {
            try
            {
                started = DateTime.UtcNow;
                successResponse = await ExecuteOneInternalAsync(pair.Fallback!, baseRequest, featureKey, true, studentProfileId, correlationId, ct);
            }
            catch (Exception fallbackEx) when (fallbackEx is not AiUnavailableException and not QuotaExceededException)
            {
                _logger.LogError(fallbackEx,
                    "Fallback AI provider failed FeatureKey={FeatureKey} Provider={Provider} Model={Model} CorrelationId={CorrelationId}",
                    featureKey, pair.Fallback!.ProviderName, pair.Fallback.ModelName, correlationId);

                await LogUsageAsync(new AiResponse("", 0, 0, 0m, pair.Fallback.ModelName, pair.Fallback.ProviderName),
                    featureKey, true, false, fallbackEx.GetType().Name, studentProfileId, correlationId, ct,
                    (long)(DateTime.UtcNow - started).TotalMilliseconds);

                throw new AiUnavailableException($"All AI providers failed for feature '{featureKey}'.", fallbackEx);
            }
        }

        // Record governance usage event after successful call
        if (studentProfileId.HasValue && studentProfileId != Guid.Empty && successResponse is not null)
        {
            try
            {
                await _quota.RecordAsync(new UsageEvent(
                    studentProfileId.Value,
                    governanceKey,
                    UsageUnitType.Count,
                    unitsUsed: 1,
                    provider: successResponse.ProviderName,
                    model: successResponse.ModelName,
                    inputTokens: successResponse.InputTokens,
                    outputTokens: successResponse.OutputTokens,
                    totalTokens: successResponse.InputTokens + successResponse.OutputTokens,
                    estimatedCost: successResponse.CostUsd,
                    requestId: null,
                    correlationId: correlationId,
                    success: true), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record usage event FeatureKey={FeatureKey}", governanceKey);
            }
        }

        return successResponse!.ResponseJson;
    }

    /// <summary>
    /// Executes an AI request and returns the response enriched with provider/model metadata.
    /// Use this overload when the caller needs traceability (e.g. diagnostic failure logging).
    /// </summary>
    public async Task<AiExecutionResult> ExecuteWithMetaAsync(
        string featureKey,
        AiRequest baseRequest,
        Guid? studentProfileId,
        string? correlationId,
        CancellationToken ct)
    {
        var pair = _resolver.ResolveLlm(featureKey, ResolveLlmCategory(featureKey));

        // Delegate to the existing full execution logic via a thin wrapper that captures metadata.
        // We record which provider resolved at call time; execution may fall back internally.
        var governanceKey = ResolveGovernanceKey(featureKey);

        if (studentProfileId.HasValue && studentProfileId != Guid.Empty
            && QuotaCheckedFeatures.Contains(governanceKey))
        {
            var decision = await _quota.CheckAsync(studentProfileId.Value, governanceKey, estimatedUnits: 1, ct: ct);
            if (!decision.Allowed)
                throw new QuotaExceededException(decision);
        }

        var started = DateTime.UtcNow;
        AiResponse? successResponse = null;
        var isFallback = false;

        try
        {
            successResponse = await ExecuteOneInternalAsync(pair.Primary, baseRequest, featureKey, false, studentProfileId, correlationId, ct);
        }
        catch (Exception primaryEx) when (primaryEx is not AiUnavailableException and not QuotaExceededException)
        {
            _logger.LogWarning(primaryEx,
                "Primary AI provider failed FeatureKey={FeatureKey} Provider={Provider} Model={Model} CorrelationId={CorrelationId}",
                featureKey, pair.Primary.ProviderName, pair.Primary.ModelName, correlationId);

            await LogUsageAsync(new AiResponse("", 0, 0, 0m, pair.Primary.ModelName, pair.Primary.ProviderName),
                featureKey, false, false, primaryEx.GetType().Name, studentProfileId, correlationId, ct,
                (long)(DateTime.UtcNow - started).TotalMilliseconds);

            if (pair.Fallback is null)
                throw new AiUnavailableException($"AI provider '{pair.Primary.ProviderName}' failed and no fallback is configured.", primaryEx);
        }

        if (successResponse is null)
        {
            try
            {
                started = DateTime.UtcNow;
                successResponse = await ExecuteOneInternalAsync(pair.Fallback!, baseRequest, featureKey, true, studentProfileId, correlationId, ct);
                isFallback = true;
            }
            catch (Exception fallbackEx) when (fallbackEx is not AiUnavailableException and not QuotaExceededException)
            {
                _logger.LogError(fallbackEx,
                    "Fallback AI provider failed FeatureKey={FeatureKey} Provider={Provider} Model={Model} CorrelationId={CorrelationId}",
                    featureKey, pair.Fallback!.ProviderName, pair.Fallback.ModelName, correlationId);

                await LogUsageAsync(new AiResponse("", 0, 0, 0m, pair.Fallback.ModelName, pair.Fallback.ProviderName),
                    featureKey, true, false, fallbackEx.GetType().Name, studentProfileId, correlationId, ct,
                    (long)(DateTime.UtcNow - started).TotalMilliseconds);

                throw new AiUnavailableException($"All AI providers failed for feature '{featureKey}'.", fallbackEx);
            }
        }

        if (studentProfileId.HasValue && studentProfileId != Guid.Empty && successResponse is not null)
        {
            try
            {
                await _quota.RecordAsync(new UsageEvent(
                    studentProfileId.Value,
                    governanceKey,
                    UsageUnitType.Count,
                    unitsUsed: 1,
                    provider: successResponse.ProviderName,
                    model: successResponse.ModelName,
                    inputTokens: successResponse.InputTokens,
                    outputTokens: successResponse.OutputTokens,
                    totalTokens: successResponse.InputTokens + successResponse.OutputTokens,
                    estimatedCost: successResponse.CostUsd,
                    requestId: null,
                    correlationId: correlationId,
                    success: true), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record usage event FeatureKey={FeatureKey}", governanceKey);
            }
        }

        return new AiExecutionResult(
            successResponse!.ResponseJson,
            successResponse.ProviderName ?? pair.Primary.ProviderName,
            successResponse.ModelName ?? pair.Primary.ModelName,
            isFallback);
    }

    private static string ResolveGovernanceKey(string featureKey) =>
        GovernanceKeyMap.TryGetValue(featureKey, out var mapped) ? mapped : featureKey;

    private static string ResolveLlmCategory(string featureKey)
    {
        return featureKey switch
        {
            "activity_generate_writing" => "llm.generation",
            "activity_generate_listening" => "llm.generation",
            "activity_generate_speaking_roleplay" => "llm.generation",
            "activity_template_generate_instance" => "llm.generation",
            "resource_candidate_analyze" => "llm.evaluation",
            "activity_generate_phrase_match" => "llm.generation",
            "activity_generate_gap_fill_workplace_phrase" => "llm.generation",
            "activity_generate_listen_and_answer" => "llm.generation",
            "activity_generate_listen_and_gap_fill" => "llm.generation",
            "activity_generate_email_reply" => "llm.generation",
            "activity_generate_teams_chat_simulation" => "llm.generation",
            "activity_generate_spoken_response_from_prompt" => "llm.generation",
            "activity_generate_lesson_reflection" => "llm.generation",
            "lesson_generate_from_resources" => "llm.generation",
            "exercise_generate_from_resources" => "llm.generation",
            "module_generate_from_resource" => "llm.generation",
            "activity_evaluate_writing" => "llm.evaluation",
            "activity_evaluate_speaking_roleplay" => "llm.evaluation",
            "activity_evaluate_phrase_match" => "llm.evaluation",
            "activity_evaluate_gap_fill_workplace_phrase" => "llm.evaluation",
            "activity_evaluate_listen_and_answer" => "llm.evaluation",
            "activity_evaluate_listen_and_gap_fill" => "llm.evaluation",
            "activity_evaluate_email_reply" => "llm.evaluation",
            "activity_evaluate_teams_chat_simulation" => "llm.evaluation",
            "activity_evaluate_spoken_response_from_prompt" => "llm.evaluation",
            "activity_evaluate_lesson_reflection" => "llm.evaluation",
            "writing.exercise" => "llm.evaluation",
            "writing.exercise.v2" => "llm.evaluation",
            "placement_assessment_evaluate" => "llm.evaluation",
            "learning_path_generate" => "llm.memory",
            "learning_path_generate_adaptive" => "llm.memory",
            "student_memory_update" => "llm.memory",
            "vocabulary_extract_from_attempt" => "llm.memory",
            _ => AiProviderResolver.DefaultLlmCategory
        };
    }

    private async Task<AiResponse> ExecuteOneInternalAsync(
        AiProviderSelection selection,
        AiRequest baseRequest,
        string featureKey,
        bool isFallback,
        Guid? studentProfileId,
        string? correlationId,
        CancellationToken ct)
    {
        var request = baseRequest with
        {
            ModelHint = selection.ModelName,
            ApiKeyOverride = selection.ApiKeyOverride,
            EndpointOverride = selection.EndpointOverride,
        };

        var started = DateTime.UtcNow;
        var response = await selection.Provider.CompleteAsync(request, ct);
        var durationMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;
        await LogUsageAsync(response, featureKey, isFallback, true, null, studentProfileId, correlationId, ct, durationMs);
        return response;
    }

    private async Task LogUsageAsync(
        AiResponse response,
        string featureKey,
        bool isFallback,
        bool wasSuccessful,
        string? failureReason,
        Guid? studentProfileId,
        string? correlationId,
        CancellationToken ct,
        long durationMs = 0)
    {
        try
        {
            _db.AiUsageLogs.Add(new AiUsageLog(
                studentProfileId == Guid.Empty ? null : studentProfileId,
                featureKey,
                string.IsNullOrWhiteSpace(response.ProviderName) ? "unknown" : response.ProviderName,
                string.IsNullOrWhiteSpace(response.ModelName) ? "unknown" : response.ModelName,
                isFallback,
                wasSuccessful,
                failureReason,
                response.InputTokens,
                response.OutputTokens,
                response.CostUsd,
                durationMs,
                correlationId));

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log AI usage FeatureKey={FeatureKey} CorrelationId={CorrelationId}", featureKey, correlationId);
        }
    }
}
