using LinguaCoach.Application.Admin;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Phase 20A — read-only admin AI operations dashboard. Aggregates existing speaking/writing
/// evaluation, generation quality, and AI usage data sources into one operational summary.
/// Purely additive read layer over existing services — never mutates state, never adds AI
/// behaviour, and never touches CEFR, objective completion, or the Learning Plan.
/// Phase I2C: the readiness-pool/review-scaffold section was removed along with
/// StudentActivityReadinessItem — see docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/ai-operations")]
public sealed class AdminAiOperationsController : ControllerBase
{
    private readonly IAdminAiUsageHandler _aiUsage;
    private readonly ISpeakingEvaluationQualityQuery _speakingQuality;
    private readonly ISpeakingEvaluationProvider _speakingProvider;
    private readonly ISpeakingEvaluationSignalApplicationService _speakingSignals;
    private readonly IAdminWritingEvaluationQuery _writingQuery;
    private readonly IWritingEvaluationSignalApplicationService _writingSignals;
    private readonly IAdminGenerationQualityHandler _generationQuality;
    private readonly LinguaCoachDbContext _db;

    public AdminAiOperationsController(
        IAdminAiUsageHandler aiUsage,
        ISpeakingEvaluationQualityQuery speakingQuality,
        ISpeakingEvaluationProvider speakingProvider,
        ISpeakingEvaluationSignalApplicationService speakingSignals,
        IAdminWritingEvaluationQuery writingQuery,
        IWritingEvaluationSignalApplicationService writingSignals,
        IAdminGenerationQualityHandler generationQuality,
        LinguaCoachDbContext db)
    {
        _aiUsage = aiUsage;
        _speakingQuality = speakingQuality;
        _speakingProvider = speakingProvider;
        _speakingSignals = speakingSignals;
        _writingQuery = writingQuery;
        _writingSignals = writingSignals;
        _generationQuality = generationQuality;
        _db = db;
    }

    /// <summary>
    /// Returns the combined AI operations summary: provider/model usage, speaking and writing
    /// evaluation queue health, generation quality failures, readiness-pool/review-scaffold
    /// pilot state, signal safety gates, and a safe combined recent-failures table.
    /// Read-only, admin-only. No raw prompts, provider payloads, or secrets are ever returned.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<AdminAiOperationsSummaryDto>> GetSummary(CancellationToken ct = default)
    {
        var aiUsage = await _aiUsage.GetSummaryAsync(ct: ct);
        var speakingQuality = await _speakingQuality.GetQualitySummaryAsync(ct);
        var speakingSafety = await _speakingSignals.GetSignalSafetySummaryAsync(ct);
        var writingQuality = await _writingQuery.GetQualitySummaryAsync(ct);
        var writingSafety = await _writingSignals.GetSignalSafetySummaryAsync(ct);
        var generationQuality = await _generationQuality.GetSummaryAsync(recentDays: 30, ct: ct);

        var now = DateTime.UtcNow;

        var oldestPendingSpeaking = await _db.SpeakingEvaluations
            .Where(e => e.Status == SpeakingEvaluationStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Select(e => (DateTime?)e.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var oldestPendingWriting = await _db.WritingEvaluations
            .Where(e => e.Status == WritingEvaluationStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Select(e => (DateTime?)e.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var recentFailedSpeaking = await _db.SpeakingEvaluations
            .Where(e => e.Status == SpeakingEvaluationStatus.Failed)
            .OrderByDescending(e => e.FailedAtUtc ?? e.CreatedAt)
            .Take(5)
            .Select(e => new AiOperationsRecentFailureItem(
                e.FailedAtUtc ?? e.CreatedAt, "Speaking", e.StudentProfileId, e.Id,
                e.ProviderName, e.ModelName, Truncate(e.FailureReason), "Failed"))
            .ToListAsync(ct);

        var recentFailedWriting = await _db.WritingEvaluations
            .Where(e => e.Status == WritingEvaluationStatus.Failed)
            .OrderByDescending(e => e.FailedAtUtc ?? e.CreatedAt)
            .Take(5)
            .Select(e => new AiOperationsRecentFailureItem(
                e.FailedAtUtc ?? e.CreatedAt, "Writing", e.StudentProfileId, e.Id,
                e.ProviderName, e.ModelName, Truncate(e.FailureReason), "Failed"))
            .ToListAsync(ct);

        var recentFailedGeneration = generationQuality.LatestFailures
            .Take(5)
            .Select(f => new AiOperationsRecentFailureItem(
                f.TimestampUtc, "Generation", null, null,
                f.ProviderName, f.ModelName, Truncate(f.ValidationErrors), "ValidationFailed"))
            .ToList();

        var recentFailures = recentFailedSpeaking
            .Concat(recentFailedWriting)
            .Concat(recentFailedGeneration)
            .OrderByDescending(f => f.TimestampUtc)
            .Take(15)
            .ToList();

        var signalGates = new AiOperationsSignalGateSummary(
            SpeakingCefrUpdatesEnabled: !speakingSafety.CefrUpdatesDisabled,
            WritingCefrUpdatesEnabled: !writingSafety.CefrUpdatesDisabled,
            SpeakingObjectiveCompletionEnabled: !speakingSafety.ObjectiveCompletionsDisabled,
            WritingObjectiveCompletionEnabled: !writingSafety.ObjectiveCompletionsDisabled,
            SpeakingLearningPlanAutoRegenEnabled: !speakingSafety.LearningPlanAutoRegenDisabled,
            WritingLearningPlanAutoRegenEnabled: !writingSafety.LearningPlanAutoRegenDisabled,
            SpeakingPositiveSignalsEnabled: speakingSafety.PositiveSignalsEnabled,
            WritingPositiveSignalsEnabled: writingSafety.PositiveSignalsEnabled,
            SpeakingReviewSignalsEnabled: speakingSafety.ReviewSignalsEnabled,
            WritingReviewSignalsEnabled: writingSafety.ReviewSignalsEnabled,
            AnyInvariantViolationsDetected: speakingSafety.InvariantViolationsDetected || writingSafety.InvariantViolationsDetected);

        var warnings = new List<string>();
        if (generationQuality.AbandonedWarning.IsActive && generationQuality.AbandonedWarning.Message is not null)
            warnings.Add(generationQuality.AbandonedWarning.Message);
        if (signalGates.AnyInvariantViolationsDetected)
            warnings.Add("Signal safety invariant violation detected — CEFR/objective/Learning Plan gate may not be holding. Investigate immediately.");
        if (speakingQuality.Total > 0 && speakingQuality.FailureRate > 0.25)
            warnings.Add($"Speaking evaluation failure rate is {speakingQuality.FailureRate:P0} (elevated).");
        if (writingQuality.TotalEvaluations > 0 && writingQuality.FailureRate > 0.25)
            warnings.Add($"Writing evaluation failure rate is {writingQuality.FailureRate:P0} (elevated).");

        var overallStatus =
            signalGates.AnyInvariantViolationsDetected || generationQuality.AbandonedWarning.IsActive
                ? "AttentionNeeded"
                : warnings.Count > 0
                    ? "Degraded"
                    : "Healthy";

        var unavailableSections = new List<string>
        {
            "RealTimeJobQueueDepth — no dedicated job/queue table exists; pending counts above are the closest available signal.",
            "CostEstimationForZeroCostOrNoOpProviders — cost is only shown when already persisted on the AI usage log; it is never estimated here.",
        };

        var dto = new AdminAiOperationsSummaryDto(
            GeneratedAtUtc: now,
            OverallStatus: overallStatus,
            Warnings: warnings,
            UnavailableSections: unavailableSections,
            ProviderUsage: new AiOperationsProviderUsageSummary(
                aiUsage.TotalCalls, aiUsage.SuccessfulCalls, aiUsage.FailedCalls, aiUsage.FallbackCalls,
                aiUsage.TotalCostUsd, aiUsage.TotalInputTokens, aiUsage.TotalOutputTokens, aiUsage.TotalTokens,
                aiUsage.ZeroCostCallCount, aiUsage.ByProvider, aiUsage.ByFeature),
            SpeakingEvaluationSummary: new AiOperationsSpeakingSummary(
                speakingQuality.Total > 0 || _speakingProvider.ProviderName != "NoOp",
                _speakingProvider.ProviderName,
                speakingQuality.Pending, speakingQuality.Completed, speakingQuality.Failed, speakingQuality.NotSupported,
                oldestPendingSpeaking.HasValue ? (now - oldestPendingSpeaking.Value).TotalMinutes : null,
                speakingQuality.ProviderModelDistribution, speakingQuality.LatestFailureReasons),
            WritingEvaluationSummary: new AiOperationsWritingSummary(
                writingQuality.ConfigEnabled, writingQuality.ProviderName, writingQuality.ModelName,
                writingQuality.PendingCount, writingQuality.EvaluatingCount, writingQuality.CompletedCount,
                writingQuality.FailedCount, writingQuality.NotSupportedCount,
                oldestPendingWriting.HasValue ? (now - oldestPendingWriting.Value).TotalMinutes : null,
                writingQuality.LatestFailureReasons),
            GenerationQualitySummary: new AiOperationsGenerationQualitySummary(
                generationQuality.TotalValidationFailures, generationQuality.AbandonedGenerations,
                generationQuality.RecentFailureCount, generationQuality.RetentionDays,
                generationQuality.PatternBreakdown, generationQuality.CefrBreakdown,
                generationQuality.ProviderBreakdown, generationQuality.LatestFailures.Take(5).ToList()),
            SignalGateSummary: signalGates,
            RecentFailures: recentFailures);

        return Ok(dto);
    }

    private static string Truncate(string? value, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(value)) return "(no reason recorded)";
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "…";
    }
}
