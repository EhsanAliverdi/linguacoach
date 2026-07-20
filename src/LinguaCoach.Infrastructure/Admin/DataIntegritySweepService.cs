using LinguaCoach.Application.Admin;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.Lessons;
using LinguaCoach.Application.Modules;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Admin;

/// <summary>See <see cref="IDataIntegritySweepService"/> for the full rationale.</summary>
public sealed class DataIntegritySweepService : IDataIntegritySweepService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IModuleRepairService _moduleRepair;
    private readonly ILessonRepairService _lessonRepair;
    private readonly IExerciseRepairService _exerciseRepair;
    private readonly IResourceBankRepairService _resourceBankRepair;

    public DataIntegritySweepService(
        LinguaCoachDbContext db,
        IModuleRepairService moduleRepair,
        ILessonRepairService lessonRepair,
        IExerciseRepairService exerciseRepair,
        IResourceBankRepairService resourceBankRepair)
    {
        _db = db;
        _moduleRepair = moduleRepair;
        _lessonRepair = lessonRepair;
        _exerciseRepair = exerciseRepair;
        _resourceBankRepair = resourceBankRepair;
    }

    public async Task<DataIntegritySweepResult> RunAsync(CancellationToken ct = default)
    {
        var categories = new List<DataIntegrityCategoryResult>();

        // ── Orphan/FK checks — expected to always be zero (the DB's own RESTRICT foreign keys
        // already structurally prevent this corruption class); the value of running this live,
        // on demand, is proving that stays true rather than assuming it from the schema alone. ──

        var totalObjectives = await _db.StudentLearningPlanObjectives.CountAsync(ct);
        var orphanedObjectives = await _db.StudentLearningPlanObjectives
            .Where(o => !_db.StudentLearningPlans.Any(p => p.Id == o.StudentLearningPlanId))
            .CountAsync(ct);
        categories.Add(new DataIntegrityCategoryResult(
            "Learning Plan Objectives",
            "StudentLearningPlanObjective rows whose StudentLearningPlanId does not resolve to a real plan.",
            totalObjectives, orphanedObjectives, orphanedObjectives == 0));

        var totalAttempts = await _db.ActivityAttempts.CountAsync(ct);
        var orphanedAttempts = await _db.ActivityAttempts
            .Where(a => !_db.StudentProfiles.Any(p => p.Id == a.StudentProfileId)
                || !_db.LearningActivities.Any(la => la.Id == a.LearningActivityId))
            .CountAsync(ct);
        categories.Add(new DataIntegrityCategoryResult(
            "Activity Attempts",
            "ActivityAttempt rows whose StudentProfileId or LearningActivityId does not resolve.",
            totalAttempts, orphanedAttempts, orphanedAttempts == 0));

        var totalLaunches = await _db.StudentExerciseLaunches.CountAsync(ct);
        var orphanedLaunches = await _db.StudentExerciseLaunches
            .Where(l => !_db.StudentProfiles.Any(p => p.Id == l.StudentId)
                || !_db.Exercises.Any(e => e.Id == l.ExerciseId)
                || !_db.LearningActivities.Any(la => la.Id == l.LearningActivityId))
            .CountAsync(ct);
        categories.Add(new DataIntegrityCategoryResult(
            "Exercise Launches",
            "StudentExerciseLaunch rows whose StudentId, ExerciseId, or LearningActivityId does not resolve.",
            totalLaunches, orphanedLaunches, orphanedLaunches == 0));

        // ── Existing per-entity content-completeness checks — previously each on its own
        // separate, unconnected admin page; unified into this one sweep view. ──

        var moduleIssues = await _moduleRepair.GetIssuesSummaryAsync(ct);
        categories.Add(new DataIntegrityCategoryResult(
            "Modules", "Modules with missing/invalid required fields.",
            moduleIssues.TotalItems, moduleIssues.ItemsWithIssues, moduleIssues.ItemsWithIssues == 0));

        var lessonIssues = await _lessonRepair.GetIssuesSummaryAsync(ct);
        categories.Add(new DataIntegrityCategoryResult(
            "Lessons", "Lessons with missing/invalid required fields.",
            lessonIssues.TotalItems, lessonIssues.ItemsWithIssues, lessonIssues.ItemsWithIssues == 0));

        var exerciseIssues = await _exerciseRepair.GetIssuesSummaryAsync(ct);
        categories.Add(new DataIntegrityCategoryResult(
            "Exercises", "Exercises with missing/invalid required fields.",
            exerciseIssues.TotalItems, exerciseIssues.ItemsWithIssues, exerciseIssues.ItemsWithIssues == 0));

        var resourceBankIssues = await _resourceBankRepair.GetIssuesSummaryAsync(ct);
        categories.Add(new DataIntegrityCategoryResult(
            "Resource Bank", "Published Resource Bank items with missing/invalid required fields.",
            resourceBankIssues.TotalItems, resourceBankIssues.ItemsWithIssues, resourceBankIssues.ItemsWithIssues == 0));

        return new DataIntegritySweepResult(DateTimeOffset.UtcNow, categories);
    }
}
