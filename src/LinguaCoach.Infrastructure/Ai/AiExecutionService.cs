using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Ai;

public sealed class AiExecutionService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IAiProviderResolver _resolver;
    private readonly ILogger<AiExecutionService> _logger;

    public AiExecutionService(
        LinguaCoachDbContext db,
        IAiProviderResolver resolver,
        ILogger<AiExecutionService> logger)
    {
        _db = db;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(
        string featureKey,
        AiRequest baseRequest,
        Guid? studentProfileId,
        string? correlationId,
        CancellationToken ct)
    {
        var pair = _resolver.ResolveLlm(featureKey, ResolveLlmCategory(featureKey));
        var started = DateTime.UtcNow;
        try
        {
            return await ExecuteOneAsync(pair.Primary, baseRequest, featureKey, false, studentProfileId, correlationId, ct);
        }
        catch (Exception primaryEx) when (primaryEx is not AiUnavailableException)
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

        try
        {
            started = DateTime.UtcNow;
            return await ExecuteOneAsync(pair.Fallback!, baseRequest, featureKey, true, studentProfileId, correlationId, ct);
        }
        catch (Exception fallbackEx) when (fallbackEx is not AiUnavailableException)
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

    private static string ResolveLlmCategory(string featureKey)
    {
        return featureKey switch
        {
            "activity_generate_writing" => "llm.generation",
            "activity_generate_listening" => "llm.generation",
            "activity_generate_speaking_roleplay" => "llm.generation",
            "activity_generate_phrase_match" => "llm.generation",
            "activity_generate_gap_fill_workplace_phrase" => "llm.generation",
            "activity_generate_listen_and_answer" => "llm.generation",
            "activity_generate_listen_and_gap_fill" => "llm.generation",
            "activity_generate_email_reply" => "llm.generation",
            "activity_generate_teams_chat_simulation" => "llm.generation",
            "activity_generate_spoken_response_from_prompt" => "llm.generation",
            "activity_generate_lesson_reflection" => "llm.generation",
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

    private async Task<string> ExecuteOneAsync(
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
        return response.ResponseJson;
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
