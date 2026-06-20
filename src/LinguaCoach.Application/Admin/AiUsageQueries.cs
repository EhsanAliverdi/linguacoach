namespace LinguaCoach.Application.Admin;

public sealed record GetAiUsageSummaryQuery;

public interface IAdminAiUsageHandler
{
    Task<AiUsageSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AiUsageRecentItem>> GetRecentAsync(int limit = 100, CancellationToken ct = default);
}

public sealed record AiUsageSummaryDto(
    int TotalCalls,
    int SuccessfulCalls,
    int FailedCalls,
    int FallbackCalls,
    decimal TotalCostUsd,
    long TotalInputTokens,
    long TotalOutputTokens,
    long TotalTokens,
    IReadOnlyList<AiUsageByProvider> ByProvider,
    IReadOnlyList<AiUsageByFeature> ByFeature);

public sealed record AiUsageByProvider(
    string Provider,
    int Calls,
    int Successful,
    int Fallback,
    decimal CostUsd);

public sealed record AiUsageByFeature(
    string Feature,
    int Calls,
    int Successful,
    decimal CostUsd);

public sealed record AiUsageRecentItem(
    Guid Id,
    DateTime CreatedAt,
    Guid? StudentProfileId,
    string FeatureKey,
    string Provider,
    string Model,
    bool IsFallback,
    bool WasSuccessful,
    string? FailureReason,
    int InputTokens,
    int OutputTokens,
    decimal CostUsd,
    long DurationMs,
    string? CorrelationId);
