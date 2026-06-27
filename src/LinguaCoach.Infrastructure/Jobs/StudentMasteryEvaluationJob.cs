using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace LinguaCoach.Infrastructure.Jobs;

/// <summary>
/// Daily Quartz job that runs the mastery re-evaluation sweep for all active students.
///
/// Responsibilities:
///   - Select students who have new learning events since their last mastery evaluation.
///   - Call EvaluateStudentAsync for each (builds mastery report + demotes readiness items).
///   - Log counts; never throws on per-student failure (continues to next student).
/// </summary>
[DisallowConcurrentExecution]
public sealed class StudentMasteryEvaluationJob : IJob
{
    public const string JobName = "student-mastery-evaluation";

    private readonly IStudentMasteryEvaluationService _mastery;
    private readonly ILearningPlanService _learningPlan;
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<StudentMasteryEvaluationJob> _logger;

    public StudentMasteryEvaluationJob(
        IStudentMasteryEvaluationService mastery,
        ILearningPlanService learningPlan,
        LinguaCoachDbContext db,
        ILogger<StudentMasteryEvaluationJob> logger)
    {
        _mastery = mastery;
        _learningPlan = learningPlan;
        _db = db;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        _logger.LogInformation("StudentMasteryEvaluationJob starting.");

        // Select all student profile IDs that have at least one learning event.
        // This avoids evaluating students who have never practised.
        var studentIds = await _db.Set<LinguaCoach.Domain.Entities.StudentLearningEvent>()
            .Select(e => e.StudentProfileId)
            .Distinct()
            .ToListAsync(ct);

        _logger.LogInformation(
            "StudentMasteryEvaluationJob: evaluating {Count} students.", studentIds.Count);

        var succeeded = 0;
        var failed = 0;
        var totalDemoted = 0;

        foreach (var studentId in studentIds)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var report = await _mastery.EvaluateStudentAsync(
                    studentId, MasteryEvaluationReason.ScheduledSweep, ct);

                totalDemoted += report.DemotedCount;
                succeeded++;

                _logger.LogDebug(
                    "StudentMasteryEvaluationJob: student {StudentId} — mastered={M} weak={W} atRisk={A} demoted={D}.",
                    studentId,
                    report.MasteredObjectiveKeys.Count,
                    report.WeakObjectiveKeys.Count,
                    report.AtRiskObjectiveKeys.Count,
                    report.DemotedCount);

                // Phase 12D — regenerate learning plan when mastery changed
                if (report.DemotedCount > 0 || report.MasteredObjectiveKeys.Count > 0)
                {
                    try
                    {
                        await _learningPlan.RegeneratePlanAsync(
                            studentId, "mastery_sweep", ct);
                    }
                    catch (Exception planEx)
                    {
                        _logger.LogWarning(planEx,
                            "StudentMasteryEvaluationJob: plan regeneration failed for student {StudentId}.", studentId);
                    }
                }
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(ex,
                    "StudentMasteryEvaluationJob: failed for student {StudentId}.", studentId);
            }
        }

        _logger.LogInformation(
            "StudentMasteryEvaluationJob complete. succeeded={S} failed={F} totalDemoted={D}.",
            succeeded, failed, totalDemoted);
    }
}
