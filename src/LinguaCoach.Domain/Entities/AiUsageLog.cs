using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Records AI provider usage, cost, and fallback status per call.
/// Append-only — never mutated after creation.
/// </summary>
public sealed class AiUsageLog : BaseEntity
{
    public Guid? StudentProfileId { get; private set; }  // nullable — path generation has no profile
    public string FeatureKey { get; private set; }       // e.g. "activity_evaluate_writing"
    public string ProviderName { get; private set; }
    public string ModelName { get; private set; }
    public bool IsFallback { get; private set; }
    public bool WasSuccessful { get; private set; }
    public string? FailureReason { get; private set; }   // exception type name; null on success
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public decimal CostUsd { get; private set; }
    public long DurationMs { get; private set; }
    public string? CorrelationId { get; private set; }

    private AiUsageLog()
    {
        FeatureKey = string.Empty;
        ProviderName = string.Empty;
        ModelName = string.Empty;
    }

    public AiUsageLog(
        Guid? studentProfileId,
        string featureKey,
        string providerName,
        string modelName,
        bool isFallback,
        bool wasSuccessful,
        string? failureReason,
        int inputTokens,
        int outputTokens,
        decimal costUsd,
        long durationMs,
        string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(featureKey)) throw new ArgumentException("FeatureKey is required.", nameof(featureKey));
        if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Provider name is required.", nameof(providerName));
        if (string.IsNullOrWhiteSpace(modelName)) throw new ArgumentException("Model name is required.", nameof(modelName));
        if (inputTokens < 0) throw new ArgumentOutOfRangeException(nameof(inputTokens), "Input token count cannot be negative.");
        if (outputTokens < 0) throw new ArgumentOutOfRangeException(nameof(outputTokens), "Output token count cannot be negative.");
        if (costUsd < 0) throw new ArgumentOutOfRangeException(nameof(costUsd), "Cost cannot be negative.");
        if (durationMs < 0) throw new ArgumentOutOfRangeException(nameof(durationMs), "Duration cannot be negative.");

        StudentProfileId = studentProfileId;
        FeatureKey = featureKey.Trim();
        ProviderName = providerName.Trim();
        ModelName = modelName.Trim();
        IsFallback = isFallback;
        WasSuccessful = wasSuccessful;
        FailureReason = failureReason;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CostUsd = costUsd;
        DurationMs = durationMs;
        CorrelationId = correlationId;
    }

    /// <summary>
    /// Backfills CostUsd for a historical row logged at $0 because pricing was missing at the time
    /// of the call. The one intentional exception to "append-only, never mutated" — gated by the
    /// caller to rows where CostUsd is currently 0 and tokens were actually consumed.
    /// </summary>
    public void BackfillCost(decimal costUsd)
    {
        if (costUsd < 0) throw new ArgumentOutOfRangeException(nameof(costUsd), "Cost cannot be negative.");
        CostUsd = costUsd;
    }
}
