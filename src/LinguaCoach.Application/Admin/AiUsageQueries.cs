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

/// <summary>
/// Column filters for recent-call queries. All fields are optional; null/empty = no filter applied.
/// Status values: "success" = WasSuccessful and not fallback, "failed" = not WasSuccessful, "fallback" = IsFallback.
/// </summary>
public sealed record AiUsageRecentFilter(
    string? Provider = null,
    string? Model = null,
    string? FeatureKey = null,
    string? Status = null,
    Guid? StudentId = null)
{
    public static readonly string[] ValidStatuses = ["success", "failed", "fallback"];
    public bool HasInvalidStatus => Status is not null && !ValidStatuses.Contains(Status, StringComparer.OrdinalIgnoreCase);
}

public interface IAdminAiUsageHandler
{
    Task<AiUsageSummaryDto> GetSummaryAsync(AiUsageDateFilter? dateFilter = null, AiUsageRecentFilter? columnFilter = null, CancellationToken ct = default);
    Task<AiUsagePagedResult> GetRecentAsync(int page = 1, int pageSize = 25, AiUsageDateFilter? dateFilter = null, AiUsageRecentFilter? recentFilter = null, CancellationToken ct = default);
    Task<IReadOnlyList<AiUsageRecentItem>> GetExportAsync(AiUsageDateFilter? dateFilter = null, AiUsageRecentFilter? columnFilter = null, int maxRows = 10_000, CancellationToken ct = default);
    Task<IReadOnlyList<AiUsageTrendBucket>> GetTrendsAsync(AiUsageDateFilter? dateFilter = null, AiUsageRecentFilter? columnFilter = null, CancellationToken ct = default);

    /// <summary>
    /// Reprices one batch of zero-cost rows (CostUsd == 0 and tokens > 0) matching the given filters,
    /// using pricing resolvable right now (DB override or config). Call repeatedly — driven by the
    /// caller, batch by batch — until <see cref="AiUsageRepriceBatchResult.RemainingZeroCost"/> is 0.
    /// Rows whose provider/model still has no resolvable pricing are left untouched and counted as
    /// skipped, not removed from the remaining count.
    /// </summary>
    Task<AiUsageRepriceBatchResult> RepriceZeroCostBatchAsync(AiUsageDateFilter? dateFilter = null, AiUsageRecentFilter? columnFilter = null, int batchSize = 200, CancellationToken ct = default);
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
    IReadOnlyList<AiUsageByFeature> ByFeature,
    int ZeroCostCallCount,
    long ZeroCostTotalTokens,
    IReadOnlyList<AiUsageZeroCostProviderModel> ZeroCostProviderModels);

public sealed record AiUsageZeroCostProviderModel(
    string Provider,
    string Model,
    int CallCount,
    long TotalTokens);

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

public sealed record AiUsageTrendBucket(
    DateOnly Date,
    int CallCount,
    int SuccessCount,
    int FailureCount,
    int FallbackCount,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    decimal CostUsd);

public sealed record AiUsageRepriceBatchResult(
    int ProcessedInBatch,
    int FixedInBatch,
    int SkippedInBatch,
    decimal CostAddedUsd,
    int RemainingZeroCost);

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
