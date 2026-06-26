using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.Memory;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Mastery;

/// <summary>
/// Deterministic mastery evaluation engine. No AI calls.
/// Reads StudentLearningEvent history and applies rule-based thresholds.
/// </summary>
public sealed class StudentMasteryEvaluationService : IStudentMasteryEvaluationService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IStudentLearningLedger _ledger;
    private readonly MasteryOptions _opts;
    private readonly ILogger<StudentMasteryEvaluationService> _logger;

    // Outcomes that count as a "success" for consecutive-run tracking.
    private static readonly HashSet<LearningEventOutcome> SuccessOutcomes =
    [
        LearningEventOutcome.Mastered,
        LearningEventOutcome.Practised,
        LearningEventOutcome.Reviewed
    ];

    private static readonly HashSet<LearningEventOutcome> FailureOutcomes =
    [
        LearningEventOutcome.Failed,
        LearningEventOutcome.NeedsReview
    ];

    // Terminal readiness statuses — never demote further.
    private static readonly HashSet<ReadinessPoolStatus> TerminalStatuses =
    [
        ReadinessPoolStatus.Consumed,
        ReadinessPoolStatus.Expired,
        ReadinessPoolStatus.Failed,
        ReadinessPoolStatus.Skipped
    ];

    public StudentMasteryEvaluationService(
        LinguaCoachDbContext db,
        IStudentLearningLedger ledger,
        IOptions<MasteryOptions> opts,
        ILogger<StudentMasteryEvaluationService> logger)
    {
        _db = db;
        _ledger = ledger;
        _opts = opts.Value;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public async Task<StudentMasteryReport> EvaluateStudentAsync(
        Guid studentId,
        MasteryEvaluationReason reason,
        CancellationToken ct = default)
    {
        var events = await _ledger.GetRecentAsync(studentId, limit: 200, ct: ct);

        // Group events by primary skill (our proxy for objective key when no explicit key).
        var byObjective = events
            .Where(e => e.PrimarySkill is not null || e.CurriculumObjectiveKey() is not null)
            .GroupBy(e => e.CurriculumObjectiveKey() ?? e.PrimarySkill!)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.OccurredAtUtc).ToList());

        var mastered = new List<string>();
        var weak = new List<string>();
        var atRisk = new List<string>();

        foreach (var (key, keyEvents) in byObjective)
        {
            var signal = ComputeSignal(key, keyEvents);
            switch (signal.MasteryStatus)
            {
                case MasteryStatus.Mastered:
                    mastered.Add(key);
                    break;
                case MasteryStatus.AtRisk:
                    atRisk.Add(key);
                    break;
                case MasteryStatus.NeedsPractice:
                case MasteryStatus.NeedsReview:
                    weak.Add(key);
                    break;
            }
        }

        var demoted = await EvaluateAndDemoteReadinessItemsAsync(studentId, ct);

        // Count demotion breakdown from the log (approximated from demoted total here;
        // detailed counts are tracked inside EvaluateAndDemoteReadinessItemsAsync).
        return new StudentMasteryReport
        {
            StudentId = studentId,
            EvaluatedAtUtc = DateTime.UtcNow,
            Reason = reason,
            MasteredObjectiveKeys = mastered,
            WeakObjectiveKeys = weak,
            AtRiskObjectiveKeys = atRisk,
            DemotedCount = demoted,
            SkippedCount = 0,          // Per-item breakdown not tracked at report level.
            MarkedReviewOnlyCount = 0  // Same — see EvaluateAndDemoteReadinessItemsAsync logs.
        };
    }

    public async Task<ObjectiveMasterySignal> EvaluateObjectiveMasteryAsync(
        Guid studentId,
        string objectiveKey,
        CancellationToken ct = default)
    {
        var allEvents = await _ledger.GetRecentAsync(studentId, limit: 200, ct: ct);
        // Ledger contract: GetRecentAsync returns events newest-first. Preserve that order.
        var relevant = allEvents
            .Where(e => string.Equals(e.CurriculumObjectiveKey() ?? e.PrimarySkill, objectiveKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return ComputeSignal(objectiveKey, relevant);
    }

    public async Task<ReadinessDemotionDecision> EvaluateReadinessItemFitAsync(
        Guid studentId,
        Guid readinessItemId,
        CancellationToken ct = default)
    {
        var item = await _db.Set<StudentActivityReadinessItem>()
            .FirstOrDefaultAsync(i => i.Id == readinessItemId && i.StudentId == studentId, ct);

        if (item is null)
            return ReadinessDemotionDecision.NoChange;

        return await DecideDemotionAsync(studentId, item, ct);
    }

    public async Task<int> EvaluateAndDemoteReadinessItemsAsync(
        Guid studentId,
        CancellationToken ct = default)
    {
        var items = await _db.Set<StudentActivityReadinessItem>()
            .Where(i => i.StudentId == studentId)
            .ToListAsync(ct);

        var changed = 0;

        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var decision = await DecideDemotionAsync(studentId, item, ct);
                ApplyDecision(item, decision);
                item.RecordEvaluation();

                if (decision != ReadinessDemotionDecision.NoChange &&
                    decision != ReadinessDemotionDecision.KeepReady)
                    changed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Mastery demotion failed for item {ItemId} student {StudentId}.",
                    item.Id, studentId);
            }
        }

        if (changed > 0)
            await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Mastery demotion sweep complete for student {StudentId}. Items changed: {Changed}.",
            studentId, changed);

        return changed;
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private ObjectiveMasterySignal ComputeSignal(string objectiveKey, List<StudentLearningEvent> events)
    {
        var skillKey = events.FirstOrDefault()?.PrimarySkill;

        if (events.Count == 0)
        {
            return new ObjectiveMasterySignal
            {
                ObjectiveKey = objectiveKey,
                SkillKey = skillKey,
                MasteryStatus = MasteryStatus.InsufficientEvidence,
                EvidenceCount = 0,
                ConsecutiveSuccesses = 0,
                ConsecutiveFailures = 0,
                RecentAverageScore = 0,
                LastSeenUtc = null
            };
        }

        var avgScore = events
            .Where(e => e.Score.HasValue)
            .Select(e => e.Score!.Value)
            .DefaultIfEmpty(0)
            .Average();

        // Consecutive success/failure counting from most-recent first.
        var consecutiveSuccesses = 0;
        var consecutiveFailures = 0;

        foreach (var ev in events)
        {
            if (SuccessOutcomes.Contains(ev.Outcome))
            {
                if (consecutiveFailures == 0) consecutiveSuccesses++;
                else break;
            }
            else if (FailureOutcomes.Contains(ev.Outcome))
            {
                if (consecutiveSuccesses == 0) consecutiveFailures++;
                else break;
            }
            else
            {
                break;
            }
        }

        var status = ClassifyStatus(events.Count, consecutiveSuccesses, consecutiveFailures, avgScore);

        return new ObjectiveMasterySignal
        {
            ObjectiveKey = objectiveKey,
            SkillKey = skillKey,
            MasteryStatus = status,
            EvidenceCount = events.Count,
            ConsecutiveSuccesses = consecutiveSuccesses,
            ConsecutiveFailures = consecutiveFailures,
            RecentAverageScore = avgScore,
            LastSeenUtc = events[0].OccurredAtUtc
        };
    }

    private MasteryStatus ClassifyStatus(
        int count,
        int consecutiveSuccesses,
        int consecutiveFailures,
        double avgScore)
    {
        if (count < 3)
            return MasteryStatus.InsufficientEvidence;

        // AtRisk: 2+ consecutive failures OR avg score < 30
        if (consecutiveFailures >= 2 || avgScore < 30)
            return MasteryStatus.AtRisk;

        // Mastered: >= threshold events, last N consecutive successes, high avg
        if (count >= _opts.EvidenceCountThreshold
            && consecutiveSuccesses >= _opts.ConsecutiveSuccessThreshold
            && avgScore >= _opts.MasteryScoreThreshold)
            return MasteryStatus.Mastered;

        // NeedsReview: mixed but recent success, avg 50-79
        if (consecutiveSuccesses >= 1 && avgScore >= 50 && avgScore < 80)
            return MasteryStatus.NeedsReview;

        // NeedsPractice: mixed, avg 30-79
        if (avgScore >= 30 && avgScore < 80)
            return MasteryStatus.NeedsPractice;

        return MasteryStatus.NeedsPractice;
    }

    private async Task<ReadinessDemotionDecision> DecideDemotionAsync(
        Guid studentId,
        StudentActivityReadinessItem item,
        CancellationToken ct)
    {
        // Terminal states — never touch.
        if (TerminalStatuses.Contains(item.Status))
            return ReadinessDemotionDecision.NoChange;

        // Age check: old + never consumed → Expire
        var ageDays = (DateTime.UtcNow - item.CreatedAt).TotalDays;
        if (ageDays > _opts.StaleDaysThreshold && item.ConsumedAt is null)
            return ReadinessDemotionDecision.Expire;

        // CEFR mismatch check
        var studentCefr = await GetStudentCefrAsync(studentId, ct);
        if (studentCefr is not null && item.TargetCefrLevel is not null)
        {
            var allLevels = CefrLevelConstants.All.ToList();
            var itemIdx = allLevels.IndexOf(item.TargetCefrLevel.ToUpperInvariant());
            var studentIdx = allLevels.IndexOf(studentCefr.ToUpperInvariant());
            if (itemIdx >= 0 && studentIdx >= 0 && Math.Abs(itemIdx - studentIdx) > 1)
                return ReadinessDemotionDecision.MarkStale;
        }

        // Mastery check
        var objectiveKey = item.CurriculumObjectiveKey ?? item.PrimarySkill;
        if (objectiveKey is null)
            return ReadinessDemotionDecision.KeepReady;

        var signal = await EvaluateObjectiveMasteryAsync(studentId, objectiveKey, ct);

        if (signal.MasteryStatus == MasteryStatus.Mastered)
        {
            // Review-eligible: item has IsLowerLevelContent or ReviewOnly routing
            var isReviewEligible = item.IsLowerLevelContent
                || item.RoutingReason == RoutingReason.Review
                || item.Status == ReadinessPoolStatus.ReviewOnly;

            return isReviewEligible
                ? ReadinessDemotionDecision.ConvertToReviewOnly
                : ReadinessDemotionDecision.Skip;
        }

        if (signal.MasteryStatus is MasteryStatus.AtRisk or MasteryStatus.NeedsPractice)
            return ReadinessDemotionDecision.KeepReady;

        return ReadinessDemotionDecision.KeepReady;
    }

    private void ApplyDecision(StudentActivityReadinessItem item, ReadinessDemotionDecision decision)
    {
        switch (decision)
        {
            case ReadinessDemotionDecision.ConvertToReviewOnly:
                if (item.Status is ReadinessPoolStatus.Ready or ReadinessPoolStatus.Reserved)
                    item.MarkReviewOnly("Objective mastered — converted to review only.");
                break;

            case ReadinessDemotionDecision.Skip:
                if (item.Status is not (ReadinessPoolStatus.Consumed
                    or ReadinessPoolStatus.Expired
                    or ReadinessPoolStatus.Skipped))
                    item.MarkSkipped("Objective mastered — not useful for review.");
                break;

            case ReadinessDemotionDecision.MarkStale:
                if (item.Status is ReadinessPoolStatus.Ready or ReadinessPoolStatus.Reserved)
                    item.MarkStale("CEFR level mismatch exceeds 1 level.");
                break;

            case ReadinessDemotionDecision.Expire:
                if (item.Status is not (ReadinessPoolStatus.Consumed or ReadinessPoolStatus.Expired))
                    item.Expire("Item exceeded stale-days threshold without being consumed.");
                break;

            case ReadinessDemotionDecision.KeepReady:
            case ReadinessDemotionDecision.NoChange:
            default:
                break;
        }
    }

    private async Task<string?> GetStudentCefrAsync(Guid studentId, CancellationToken ct)
    {
        return await _db.Set<StudentProfile>()
            .Where(p => p.Id == studentId)
            .Select(p => p.CefrLevel)
            .FirstOrDefaultAsync(ct);
    }
}

/// <summary>Extension to extract the curriculum objective key from a learning event.</summary>
file static class LearningEventExtensions
{
    // StudentLearningEvent does not expose CurriculumObjectiveKey directly.
    // We use PatternKey as the objective proxy when set; fall back to null.
    public static string? CurriculumObjectiveKey(this StudentLearningEvent e) => e.PatternKey;
}
