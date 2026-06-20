namespace LinguaCoach.Application.Admin;

public sealed record GetAiUsageSummaryQuery;

/// <summary>
/// Inclusive start / exclusive end date filter for AI usage queries.
/// Both nullable — omit either to leave that boundary open.
/// </summary>
public sealed record AiUsageDateFilter(DateTime? From, DateTime? To)
{
    public static readonly AiUsageDateFilter None = new(null, null);

    /// <summary>Returns true when From >= To (both non-null), which is always an empty result set.</summary>
    public bool IsInverted => From.HasValue && To.HasValue && From.Value >= To.Value;
}

public interface IAdminAiUsageHandler
{
    Task<AiUsageSummaryDto> GetSummaryAsync(AiUsageDateFilter? filter = null, CancellationToken ct = default);
    Task<AiUsagePagedResult> GetRecentAsync(int page = 1, int pageSize = 25, AiUsageDateFilter? filter = null, CancellationToken ct = default);
}

public sealed record AiUsagePagedResult(
    IReadOnlyList<AiUsageRecentItem> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

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
