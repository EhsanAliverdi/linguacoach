using LinguaCoach.Application.ExerciseLaunch;
using LinguaCoach.Application.Modules;
using LinguaCoach.Application.PracticeGymModules;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.ExerciseLaunch;

/// <summary>
/// Phase H10 — the first real runtime launch path for an approved <see cref="Exercise"/>,
/// reached through an approved <see cref="Module"/> suggestion. Deliberately a
/// <b>bridge</b>, not a new parallel attempt/scoring runtime (Option B/C from the H10 decision
/// review): it materializes the eligible Exercise into a real
/// <see cref="LearningActivity"/> using <see cref="LearningActivity.SetFormIoContent"/> — the
/// exact same mechanism the existing <c>ActivityTemplate</c> Form.io pilot
/// (<c>PracticeGymGenerationJob.TryMaterializeFromTemplateAsync</c>) already uses — so every
/// downstream piece (Form.io rendering, <c>POST api/activity/{id}/attempt</c>,
/// <c>ComponentAnswerScorer</c>/<c>FormIoPatternEvaluator</c> scoring, <c>ActivityAttempt</c>,
/// the learning ledger, multi-skill progress) works unchanged with zero new code. Only a small
/// traceability bridge row (<see cref="StudentExerciseLaunch"/>) is new.
/// </summary>
public sealed class ExerciseLaunchService : IExerciseLaunchService
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<ExerciseLaunchService> _logger;

    public ExerciseLaunchService(LinguaCoachDbContext db, ILogger<ExerciseLaunchService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ExerciseLaunchResult> LaunchAsync(
        ExerciseLaunchRequest request, CancellationToken ct = default)
    {
        try
        {
            var module = await _db.Modules
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == request.ModuleId, ct);

            if (module is null || module.ReviewStatus != AdminReviewStatus.Approved)
                return Unsupported(request.ModuleId, "This module is not available for practice right now.");

            // Phase 1 (2026-07-15 pipeline safety audit) — archival must block new launches even
            // when a stale client still holds a suggestion/assignment for a module that was
            // archived after being suggested. Already-created assignments/launches are untouched;
            // this only blocks materializing a *new* LearningActivity/StudentExerciseLaunch.
            if (!ModuleEligibility.IsAvailableForNewStudentDelivery(module))
                return Unsupported(request.ModuleId, "This module has been archived and is no longer available for new practice.");

            var exerciseLinks = await _db.ModuleExerciseLinks
                .AsNoTracking()
                .Where(l => l.ModuleId == request.ModuleId)
                .OrderBy(l => l.SortOrder)
                .ToListAsync(ct);

            if (exerciseLinks.Count == 0)
                return Unsupported(request.ModuleId, "This module has no launchable practice activity.");

            var exerciseIds = exerciseLinks.Select(l => l.ExerciseId).ToList();
            var activityDefsById = await _db.Exercises
                .AsNoTracking()
                .Where(a => exerciseIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, ct);

            Exercise? eligibleActivity = null;
            string? unsupportedReason = null;

            foreach (var link in exerciseLinks)
            {
                if (!activityDefsById.TryGetValue(link.ExerciseId, out var candidate))
                    continue;

                var eligibility = ExerciseLaunchEligibility.Evaluate(candidate);
                if (eligibility.CanLaunch)
                {
                    eligibleActivity = candidate;
                    break;
                }

                unsupportedReason ??= eligibility.UnsupportedReason;
            }

            if (eligibleActivity is null)
                return Unsupported(request.ModuleId,
                    unsupportedReason ?? "This module contains an activity type that is not launchable yet.");

            // Optional traceability/display: an Approved linked Lesson, if any.
            var lessonLink = await _db.ModuleLessonLinks
                .AsNoTracking()
                .Where(l => l.ModuleId == request.ModuleId)
                .OrderBy(l => l.SortOrder)
                .FirstOrDefaultAsync(ct);

            Lesson? approvedLesson = null;
            if (lessonLink is not null)
            {
                var lesson = await _db.Lessons
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == lessonLink.LessonId, ct);
                if (lesson is not null && lesson.ReviewStatus == AdminReviewStatus.Approved)
                    approvedLesson = lesson;
            }

            // Materialize into a real LearningActivity — same mechanism as the ActivityTemplate
            // Form.io pilot (PracticeGymGenerationJob.TryMaterializeFromTemplateAsync): a shared,
            // generic marker pattern key so the existing content-driven Form.io evaluation
            // dispatch (ActivitySubmitHandler.HandlePatternEvaluationAsync) picks it up unchanged.
            var learningActivity = new LearningActivity(
                activityType: MapToLearningActivityType(eligibleActivity),
                source: ActivitySource.SystemGenerated,
                title: eligibleActivity.Title,
                difficulty: eligibleActivity.CefrLevel ?? "B1",
                aiGeneratedContentJson: "{}",
                learningModuleId: null,
                exercisePatternKey: ExercisePatternKey.FormIoPracticeGymPilot);
            learningActivity.SetFormIoContent(eligibleActivity.FormSchemaJson!, eligibleActivity.ScoringRulesJson);

            _db.LearningActivities.Add(learningActivity);
            await _db.SaveChangesAsync(ct);

            var launch = new StudentExerciseLaunch(
                studentId: request.StudentId,
                moduleId: request.ModuleId,
                exerciseId: eligibleActivity.Id,
                learningActivityId: learningActivity.Id,
                source: request.Source,
                launchedAt: DateTimeOffset.UtcNow,
                lessonId: approvedLesson?.Id);
            _db.StudentExerciseLaunches.Add(launch);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "ExerciseLaunch: student={StudentId} module={ModuleId} activity={ExerciseId} learningActivity={LearningActivityId} source={Source}",
                request.StudentId, request.ModuleId, eligibleActivity.Id, learningActivity.Id, request.Source);

            return new ExerciseLaunchResult(
                Success: true,
                UnsupportedReason: null,
                ModuleId: request.ModuleId,
                ExerciseId: eligibleActivity.Id,
                LearningActivityId: learningActivity.Id,
                Title: eligibleActivity.Title,
                Instructions: eligibleActivity.Instructions,
                RendererType: eligibleActivity.RendererType.ToString(),
                FormSchemaJson: eligibleActivity.FormSchemaJson,
                EstimatedMinutes: eligibleActivity.EstimatedMinutes,
                Skill: eligibleActivity.Skill,
                Subskill: eligibleActivity.Subskill,
                CefrLevel: eligibleActivity.CefrLevel,
                CanSubmit: true,
                Lesson: approvedLesson is null ? null : ToLessonSummary(approvedLesson));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ExerciseLaunch failed safely for student {StudentId} module {ModuleId} — existing Practice Gym fallback remains available.",
                request.StudentId, request.ModuleId);
            return Unsupported(request.ModuleId, "This activity could not be started right now. Please try again shortly.");
        }
    }

    private static ExerciseLaunchResult Unsupported(Guid moduleId, string reason) => new(
        Success: false,
        UnsupportedReason: reason,
        ModuleId: moduleId,
        ExerciseId: null,
        LearningActivityId: null,
        Title: null,
        Instructions: null,
        RendererType: null,
        FormSchemaJson: null,
        EstimatedMinutes: null,
        Skill: null,
        Subskill: null,
        CefrLevel: null,
        CanSubmit: false,
        Lesson: null);

    /// <summary>The Domain <see cref="ActivityType"/> enum predates H4 and has no gap-fill/
    /// multiple-choice member — it's a coarse reporting category, not a renderer selector (Form.io
    /// rendering is driven by <see cref="Domain.Entities.LearningActivity.FormIoSchemaJson"/>
    /// alone, per <c>ActivitySubmitHandler</c>). A skill-based best-effort mapping is enough;
    /// exactly matches the imprecision the existing ActivityTemplate Form.io pilot already
    /// accepts for its own shared marker pattern.</summary>
    private static ActivityType MapToLearningActivityType(Exercise exercise) =>
        exercise.Skill?.Contains("reading", StringComparison.OrdinalIgnoreCase) == true
            ? ActivityType.ReadingTask
            : ActivityType.VocabularyPractice;

    private static PracticeGymModuleLessonSummary ToLessonSummary(Lesson item) => new(
        LessonId: item.Id,
        Title: item.Title,
        Body: item.Body,
        Examples: [],
        CommonMistakes: [],
        UsageNotes: item.UsageNotes);
}
