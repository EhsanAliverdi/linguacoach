using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Append-only ledger recording actual usage after a feature or provider call.
/// </summary>
public sealed class UsageEvent : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public Guid? WorkspaceId { get; private set; }

    public string FeatureKey { get; private set; }
    public UsageUnitType UnitType { get; private set; }
    public long UnitsUsed { get; private set; }

    // AI provider metadata
    public string? Provider { get; private set; }
    public string? Model { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int TotalTokens { get; private set; }

    // Media usage
    public decimal? AudioSeconds { get; private set; }
    public int? TtsCharacters { get; private set; }
    public decimal? SttMinutes { get; private set; }

    // Cost
    public decimal? EstimatedCost { get; private set; }
    public string Currency { get; private set; }

    // Tracing
    public string? RequestId { get; private set; }
    public string? CorrelationId { get; private set; }

    // Outcome
    public bool Success { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    private UsageEvent()
    {
        FeatureKey = string.Empty;
        Currency = "USD";
    }

    public UsageEvent(
        Guid studentProfileId,
        string featureKey,
        UsageUnitType unitType,
        long unitsUsed,
        string? provider,
        string? model,
        int inputTokens,
        int outputTokens,
        int totalTokens,
        decimal? estimatedCost,
        string? requestId,
        string? correlationId,
        bool success,
        string? errorCode = null,
        string? errorMessage = null,
        Guid? workspaceId = null,
        decimal? audioSeconds = null,
        int? ttsCharacters = null,
        decimal? sttMinutes = null,
        string currency = "USD")
    {
        if (string.IsNullOrWhiteSpace(featureKey)) throw new ArgumentException("Feature key is required.", nameof(featureKey));
        if (unitsUsed < 0) throw new ArgumentOutOfRangeException(nameof(unitsUsed), "Units used cannot be negative.");
        if (inputTokens < 0) throw new ArgumentOutOfRangeException(nameof(inputTokens));
        if (outputTokens < 0) throw new ArgumentOutOfRangeException(nameof(outputTokens));
        if (totalTokens < 0) throw new ArgumentOutOfRangeException(nameof(totalTokens));

        StudentProfileId = studentProfileId;
        WorkspaceId = workspaceId;
        FeatureKey = featureKey.Trim().ToLowerInvariant();
        UnitType = unitType;
        UnitsUsed = unitsUsed;
        Provider = provider?.Trim();
        Model = model?.Trim();
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        TotalTokens = totalTokens;
        AudioSeconds = audioSeconds;
        TtsCharacters = ttsCharacters;
        SttMinutes = sttMinutes;
        EstimatedCost = estimatedCost;
        Currency = currency;
        RequestId = requestId?.Trim();
        CorrelationId = correlationId?.Trim();
        Success = success;
        ErrorCode = errorCode?.Trim();
        ErrorMessage = errorMessage?.Trim();
    }
}
