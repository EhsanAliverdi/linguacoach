using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Speaking;

/// <summary>
/// Applies high-confidence speaking evaluation signals to student learning state.
/// Conservative: disabled by default. CEFR and objective-completion are permanently off in Phase 16I.
/// Idempotent: one applied-signal record per evaluation enforced by unique index.
/// </summary>
public sealed class SpeakingEvaluationSignalApplicationService : ISpeakingEvaluationSignalApplicationService
{
    private const string RuleVersion = "16I-v1";
    private const string SkillKey = "speaking";

    private readonly LinguaCoachDbContext _db;
    private readonly SpeakingEvaluationOptions _options;
    private readonly ILogger<SpeakingEvaluationSignalApplicationService> _logger;

    public SpeakingEvaluationSignalApplicationService(
        LinguaCoachDbContext db,
        IOptions<SpeakingEvaluationOptions> options,
        ILogger<SpeakingEvaluationSignalApplicationService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SpeakingSignalApplicationBatchResult> ApplyPendingSignalsAsync(
        int maxBatch, CancellationToken ct = default)
    {
        // Load completed evaluations that do not yet have an applied signal.
        var evaluations = await _db.SpeakingEvaluations
            .Where(e => e.Status == SpeakingEvaluationStatus.Completed)
            .Where(e => !_db.SpeakingEvaluationAppliedSignals.Any(s => s.EvaluationId == e.Id))
            .OrderBy(e => e.CreatedAt)
            .Take(maxBatch)
            .ToListAsync(ct);

        var result = new SpeakingSignalApplicationBatchResult(0, 0, 0, 0, 0, 0, 0, 0);
        foreach (var evaluation in evaluations)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                result = await ProcessOneAsync(evaluation, result, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "SpeakingSignalApplication: unexpected error EvaluationId={Id}", evaluation.Id);
                result = result with { Processed = result.Processed + 1, Failed = result.Failed + 1 };
            }
        }
        return result;
    }

    private async Task<SpeakingSignalApplicationBatchResult> ProcessOneAsync(
        SpeakingEvaluation evaluation,
        SpeakingSignalApplicationBatchResult running,
        CancellationToken ct)
    {
        var dryRun = SpeakingDryRunSignalMapper.Map(evaluation);

        // Blocked by upstream conditions (failed, not-supported, missing score, low-confidence).
        if (dryRun.IsBlocked)
        {
            _logger.LogDebug(
                "SpeakingSignalApplication: dry-run blocked EvaluationId={Id} Reason={Reason}",
                evaluation.Id, dryRun.BlockedReason);
            return running with { Processed = running.Processed + 1 };
        }

        // No-signal outcome — score in middle band, no action.
        if (dryRun.Outcome == SpeakingDryRunSignalOutcome.CandidateNoSignal)
        {
            return running with { Processed = running.Processed + 1, NoSignal = running.NoSignal + 1 };
        }

        // Config gate: must explicitly enable mastery signal application.
        if (!_options.ApplyMasterySignals)
        {
            _logger.LogDebug(
                "SpeakingSignalApplication: blocked by config (ApplyMasterySignals=false) EvaluationId={Id}",
                evaluation.Id);
            return running with { Processed = running.Processed + 1, BlockedByConfig = running.BlockedByConfig + 1 };
        }

        // Confidence gate.
        var minBand = ParseConfidenceBand(_options.MinimumConfidenceForMasterySignal);
        if (!MeetsConfidenceThreshold(dryRun.ConfidenceBand, minBand))
        {
            _logger.LogDebug(
                "SpeakingSignalApplication: blocked by confidence EvaluationId={Id} Band={Band} Min={Min}",
                evaluation.Id, dryRun.ConfidenceBand, minBand);
            return running with { Processed = running.Processed + 1, BlockedByConfidence = running.BlockedByConfidence + 1 };
        }

        // Signal-type gate.
        var isReview = dryRun.Outcome == SpeakingDryRunSignalOutcome.CandidateReviewSignal;
        var isPositive = dryRun.Outcome == SpeakingDryRunSignalOutcome.CandidatePositiveSignal;

        if (isReview && !_options.AllowReviewSignals)
        {
            return running with { Processed = running.Processed + 1, BlockedBySignalType = running.BlockedBySignalType + 1 };
        }
        if (isPositive && !_options.AllowPositiveSignals)
        {
            return running with { Processed = running.Processed + 1, BlockedBySignalType = running.BlockedBySignalType + 1 };
        }

        // Idempotency: check again inside the operation to guard against race.
        var alreadyApplied = await _db.SpeakingEvaluationAppliedSignals
            .AnyAsync(s => s.EvaluationId == evaluation.Id, ct);
        if (alreadyApplied)
        {
            return running with { Processed = running.Processed + 1, DuplicateSkipped = running.DuplicateSkipped + 1 };
        }

        // Apply the signal.
        var signalType = isReview ? "Review" : "Positive";
        var confidenceStr = dryRun.ConfidenceBand?.ToString() ?? "Unknown";
        var reason = isReview
            ? $"Speaking review signal: score {evaluation.OverallScore:F0}, confidence {confidenceStr}."
            : $"Speaking positive signal: score {evaluation.OverallScore:F0}, confidence {confidenceStr}.";

        Guid? eventId = null;

        // Write StudentLearningEvent.
        var learningEvent = new StudentLearningEvent(
            studentProfileId: evaluation.StudentProfileId,
            source: LearningEventSource.SpeakingEvaluation,
            outcome: isReview ? LearningEventOutcome.NeedsReview : LearningEventOutcome.Practised,
            activityId: evaluation.LearningActivityId,
            activityAttemptId: evaluation.ActivityAttemptId,
            primarySkill: SkillKey,
            score: evaluation.OverallScore,
            normalizedScore: evaluation.OverallScore.HasValue ? evaluation.OverallScore.Value / 100.0 : null,
            metadataJson: $"{{\"source\":\"SpeakingEvaluation\",\"evaluationId\":\"{evaluation.Id:N}\",\"ruleVersion\":\"{RuleVersion}\"}}");
        _db.StudentLearningEvents.Add(learningEvent);
        eventId = learningEvent.Id;

        // For review signals: update or insert StudentSkillProfile with MarkWeak.
        if (isReview)
        {
            await ApplyWeakSkillSignalAsync(evaluation.StudentProfileId, ct);
        }

        // Write audit record.
        var appliedSignal = SpeakingEvaluationAppliedSignal.Create(
            evaluationId: evaluation.Id,
            attemptId: evaluation.ActivityAttemptId,
            studentProfileId: evaluation.StudentProfileId,
            activityId: evaluation.LearningActivityId,
            signalType: signalType,
            confidence: confidenceStr,
            scoreUsed: evaluation.OverallScore,
            skillAffected: SkillKey,
            dryRunOutcome: dryRun.Outcome.ToString(),
            reason: reason,
            learningEventId: eventId);
        _db.SpeakingEvaluationAppliedSignals.Add(appliedSignal);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SpeakingSignalApplication: applied EvaluationId={Id} SignalType={Type} Confidence={Confidence}",
            evaluation.Id, signalType, confidenceStr);

        return running with { Processed = running.Processed + 1, Applied = running.Applied + 1 };
    }

    private async Task ApplyWeakSkillSignalAsync(Guid studentProfileId, CancellationToken ct)
    {
        var normalizedKey = StudentSkillProfile.NormaliseSkillKey(SkillKey);
        var existing = await _db.StudentSkillProfiles
            .FirstOrDefaultAsync(s => s.StudentProfileId == studentProfileId && s.SkillKey == normalizedKey, ct);

        if (existing is not null)
        {
            existing.MarkWeak(true);
        }
        else
        {
            _db.StudentSkillProfiles.Add(
                new StudentSkillProfile(studentProfileId, SkillKey, "Speaking", isWeak: true));
        }
    }

    public async Task<SpeakingSignalApplicationSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var totalCompleted = await _db.SpeakingEvaluations
            .CountAsync(e => e.Status == SpeakingEvaluationStatus.Completed, ct);

        var appliedSignals = await _db.SpeakingEvaluationAppliedSignals
            .GroupBy(s => s.SignalType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalApplied = appliedSignals.Sum(g => g.Count);

        // Compute dry-run candidate count from all completed evaluations.
        var completedEvals = await _db.SpeakingEvaluations
            .Where(e => e.Status == SpeakingEvaluationStatus.Completed)
            .Select(e => new
            {
                e.Id,
                e.OverallScore,
                e.FluencyScore,
                e.CompletenessScore,
                e.RelevanceScore,
                e.FeedbackText,
            })
            .ToListAsync(ct);

        var appliedIds = await _db.SpeakingEvaluationAppliedSignals
            .Select(s => s.EvaluationId)
            .ToListAsync(ct);
        var appliedSet = appliedIds.ToHashSet();

        var candidateCount = 0;
        var blockedByMissingScore = 0;
        var blockedByFailedOrUnsupported = 0;
        var noSignal = 0;

        foreach (var e in completedEvals)
        {
            if (e.OverallScore is null) { blockedByMissingScore++; continue; }
            var signal = SpeakingDryRunSignalMapper.MapFromFields(
                Guid.Empty, Guid.Empty,
                SpeakingEvaluationStatus.Completed,
                e.OverallScore, e.FluencyScore, e.CompletenessScore, e.RelevanceScore, e.FeedbackText);
            if (signal.IsCandidate) candidateCount++;
            else if (signal.Outcome == SpeakingDryRunSignalOutcome.CandidateNoSignal) noSignal++;
        }

        return new SpeakingSignalApplicationSummaryDto(
            MasteryIntegrationEnabled: _options.ApplyMasterySignals,
            ReviewSignalsAllowed: _options.AllowReviewSignals,
            PositiveSignalsAllowed: _options.AllowPositiveSignals,
            ObjectiveCompletionAllowed: _options.AllowObjectiveCompletion,
            CefrUpdateAllowed: _options.AllowCefrUpdate,
            MinimumConfidenceRequired: _options.MinimumConfidenceForMasterySignal,
            TotalCompletedEvaluations: totalCompleted,
            CandidateSignals: candidateCount,
            AppliedSignals: totalApplied,
            BlockedByConfig: _options.ApplyMasterySignals ? 0 : candidateCount - totalApplied,
            BlockedByConfidence: 0,
            BlockedBySignalType: 0,
            BlockedByFailedOrUnsupported: blockedByFailedOrUnsupported,
            BlockedByMissingScore: blockedByMissingScore,
            DuplicateSkipped: 0,
            NoSignal: noSignal,
            FailedApplication: 0);
    }

    private static SpeakingDryRunConfidenceBand ParseConfidenceBand(string value) =>
        value?.ToUpperInvariant() switch
        {
            "HIGH"   => SpeakingDryRunConfidenceBand.High,
            "MEDIUM" => SpeakingDryRunConfidenceBand.Medium,
            _        => SpeakingDryRunConfidenceBand.High,
        };

    private static bool MeetsConfidenceThreshold(
        SpeakingDryRunConfidenceBand? actual,
        SpeakingDryRunConfidenceBand minimum)
    {
        if (actual is null) return false;
        return (int)actual.Value >= (int)minimum;
    }
}
