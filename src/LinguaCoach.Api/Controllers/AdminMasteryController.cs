using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.Memory;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Admin-wide mastery diagnostics. Relocated from AdminReadinessPoolController in Phase I2C
/// (readiness-pool removal) — GetMasteryValidationSummary is unrelated to the readiness pool
/// (mastery classification is a standalone, still-live concept) and the frontend still calls it.
/// See docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminMasteryController : ControllerBase
{
    private readonly IStudentMasteryEvaluationService _mastery;
    private readonly IStudentLearningLedger _ledger;
    private readonly LinguaCoachDbContext _db;

    public AdminMasteryController(
        IStudentMasteryEvaluationService mastery,
        IStudentLearningLedger ledger,
        LinguaCoachDbContext db)
    {
        _mastery = mastery;
        _ledger = ledger;
        _db = db;
    }

    /// <summary>
    /// Returns a mastery validation diagnostic summary across all active students.
    /// Aggregates mastery signal quality — no AI calls, no DB writes, read-only.
    /// </summary>
    [HttpGet("api/admin/mastery/validation-summary")]
    public async Task<IActionResult> GetMasteryValidationSummary(CancellationToken ct)
    {
        var activeStudents = await _db.StudentProfiles
            .Where(p => p.LifecycleStage != StudentLifecycleStage.Archived
                     && p.LifecycleStage >= StudentLifecycleStage.CourseReady
                     && p.OnboardingStatus == OnboardingStatus.Complete)
            .AsNoTracking()
            .Select(p => p.Id)
            .ToListAsync(ct);

        var totalObjectives = 0;
        var countInsufficient = 0;
        var countMastered = 0;
        var countNeedsReview = 0;
        var countNeedsPractice = 0;
        var countAtRisk = 0;
        var masteredExcluded = 0;
        var warnings = new List<string>();

        foreach (var studentId in activeStudents)
        {
            if (ct.IsCancellationRequested) break;

            var report = await _mastery.EvaluateStudentAsync(
                studentId, MasteryEvaluationReason.Manual, ct);

            var allKeys = report.MasteredObjectiveKeys
                .Concat(report.WeakObjectiveKeys)
                .Concat(report.AtRiskObjectiveKeys)
                .ToList();

            totalObjectives += allKeys.Count;
            countMastered    += report.MasteredObjectiveKeys.Count;
            countNeedsReview += report.WeakObjectiveKeys.Count;
            countAtRisk      += report.AtRiskObjectiveKeys.Count;

            masteredExcluded += report.MasteredObjectiveKeys.Count;

            // Insufficient evidence is not in the report — infer from ledger events.
            var events = await _ledger.GetRecentAsync(studentId, limit: 200, ct);
            var objectiveKeysWithEvents = events
                .Where(e => e.PatternKey is not null || e.PrimarySkill is not null)
                .Select(e => e.PatternKey ?? e.PrimarySkill!)
                .Distinct()
                .Count();
            countInsufficient += Math.Max(0, objectiveKeysWithEvents - allKeys.Count);

            // Suspicious: mastered with very few events total
            if (report.MasteredObjectiveKeys.Count > 0 && events.Count < 3)
                warnings.Add($"Student {studentId:N} has {report.MasteredObjectiveKeys.Count} mastered objective(s) but fewer than 3 total events.");

            // Suspicious: all objectives at risk
            if (report.AtRiskObjectiveKeys.Count > 0 && report.MasteredObjectiveKeys.Count == 0
                && report.WeakObjectiveKeys.Count == 0 && allKeys.Count > 0)
                warnings.Add($"Student {studentId:N} has all {report.AtRiskObjectiveKeys.Count} objective(s) at risk.");
        }

        var summary = new MasteryValidationSummary
        {
            TotalStudentsEvaluated = activeStudents.Count,
            TotalObjectivesEvaluated = totalObjectives,
            CountInsufficientEvidence = countInsufficient,
            CountMastered = countMastered,
            CountNeedsReview = countNeedsReview,
            CountNeedsPractice = countNeedsPractice,
            CountAtRisk = countAtRisk,
            MasteredExcludedFromNewLearning = masteredExcluded,
            Warnings = warnings,
            GeneratedAt = DateTime.UtcNow
        };

        return Ok(summary);
    }
}
