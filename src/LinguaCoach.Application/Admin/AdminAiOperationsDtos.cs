namespace LinguaCoach.Application.Admin;

/// <summary>
/// Phase 20A — read-only admin AI operations dashboard summary. Aggregates existing
/// speaking/writing evaluation, generation quality, and AI usage data sources into one
/// operational view. Never adds new AI behaviour, scoring, CEFR mutation, objective completion,
/// or Learning Plan regeneration — it only reads existing state.
/// Phase I2C: the readiness-pool/review-scaffold section was removed along with
/// StudentActivityReadinessItem — see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
/// </summary>
public sealed record AdminAiOperationsSummaryDto(
    DateTime GeneratedAtUtc,
    string OverallStatus, // Healthy | Degraded | AttentionNeeded
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> UnavailableSections,
    AiOperationsProviderUsageSummary ProviderUsage,
    AiOperationsSpeakingSummary SpeakingEvaluationSummary,
    AiOperationsWritingSummary WritingEvaluationSummary,
    AiOperationsGenerationQualitySummary GenerationQualitySummary,
    AiOperationsSignalGateSummary SignalGateSummary,
    IReadOnlyList<AiOperationsRecentFailureItem> RecentFailures);

public sealed record AiOperationsProviderUsageSummary(
    int TotalCalls,
    int SuccessfulCalls,
    int FailedCalls,
    int FallbackCalls,
    decimal TotalCostUsd,
    long TotalInputTokens,
    long TotalOutputTokens,
    long TotalTokens,
    int ZeroCostCallCount,
    IReadOnlyList<AiUsageByProvider> ByProvider,
    IReadOnlyList<AiUsageByFeature> ByFeature);

public sealed record AiOperationsSpeakingSummary(
    bool ConfigEnabled,
    string ProviderName,
    int PendingCount,
    int CompletedCount,
    int FailedCount,
    int NotSupportedCount,
    double? OldestPendingAgeMinutes,
    IReadOnlyList<LinguaCoach.Application.Speaking.SpeakingProviderModelCount> ProviderModelDistribution,
    IReadOnlyList<string> LatestFailureReasons);

public sealed record AiOperationsWritingSummary(
    bool ConfigEnabled,
    string? ProviderName,
    string? ModelName,
    int PendingCount,
    int EvaluatingCount,
    int CompletedCount,
    int FailedCount,
    int NotSupportedCount,
    double? OldestPendingAgeMinutes,
    IReadOnlyList<string> LatestFailureReasons);

public sealed record AiOperationsGenerationQualitySummary(
    int TotalValidationFailures,
    int AbandonedGenerations,
    int RecentFailureCount,
    int RetentionDays,
    IReadOnlyList<PatternFailureBreakdownItem> PatternBreakdown,
    IReadOnlyList<CefrFailureBreakdownItem> CefrBreakdown,
    IReadOnlyList<ProviderModelBreakdownItem> ProviderBreakdown,
    IReadOnlyList<ValidationFailureItem> LatestFailures);

/// <summary>
/// Speaking and writing signal gates are tracked per-pipeline (they are configured
/// independently), never combined into a single misleading flag.
/// </summary>
public sealed record AiOperationsSignalGateSummary(
    bool SpeakingCefrUpdatesEnabled,
    bool WritingCefrUpdatesEnabled,
    bool SpeakingObjectiveCompletionEnabled,
    bool WritingObjectiveCompletionEnabled,
    bool SpeakingLearningPlanAutoRegenEnabled,
    bool WritingLearningPlanAutoRegenEnabled,
    bool SpeakingPositiveSignalsEnabled,
    bool WritingPositiveSignalsEnabled,
    bool SpeakingReviewSignalsEnabled,
    bool WritingReviewSignalsEnabled,
    bool AnyInvariantViolationsDetected);

/// <summary>
/// One row in the combined recent-failures table. Reason is a curated failure-reason string
/// already produced by our own evaluation/generation services — never a raw provider payload,
/// prompt, or secret.
/// </summary>
public sealed record AiOperationsRecentFailureItem(
    DateTime TimestampUtc,
    string Area, // Speaking | Writing | Generation
    Guid? StudentProfileId,
    Guid? EvaluationId,
    string? ProviderName,
    string? ModelName,
    string Reason,
    string Status);
