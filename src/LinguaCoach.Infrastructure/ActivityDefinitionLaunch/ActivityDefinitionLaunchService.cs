using LinguaCoach.Application.ActivityDefinitionLaunch;
using LinguaCoach.Application.PracticeGymModules;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.ActivityDefinitionLaunch;

/// <summary>
/// Phase H10 — the first real runtime launch path for an approved <see cref="ActivityDefinition"/>,
/// reached through an approved <see cref="ModuleDefinition"/> suggestion. Deliberately a
/// <b>bridge</b>, not a new parallel attempt/scoring runtime (Option B/C from the H10 decision
/// review): it materializes the eligible Activity Definition into a real
/// <see cref="LearningActivity"/> using <see cref="LearningActivity.SetFormIoContent"/> — the
/// exact same mechanism the existing <c>ActivityTemplate</c> Form.io pilot
/// (<c>PracticeGymGenerationJob.TryMaterializeFromTemplateAsync</c>) already uses — so every
/// downstream piece (Form.io rendering, <c>POST api/activity/{id}/attempt</c>,
/// <c>ComponentAnswerScorer</c>/<c>FormIoPatternEvaluator</c> scoring, <c>ActivityAttempt</c>,
/// the learning ledger, multi-skill progress) works unchanged with zero new code. Only a small
/// traceability bridge row (<see cref="StudentActivityDefinitionLaunch"/>) is new.
/// </summary>
public sealed class ActivityDefinitionLaunchService : IActivityDefinitionLaunchService
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<ActivityDefinitionLaunchService> _logger;

    public ActivityDefinitionLaunchService(LinguaCoachDbContext db, ILogger<ActivityDefinitionLaunchService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ActivityDefinitionLaunchResult> LaunchAsync(
        ActivityDefinitionLaunchRequest request, CancellationToken ct = default)
    {
        try
        {
            var module = await _db.ModuleDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == request.ModuleDefinitionId, ct);

            if (module is null || module.ReviewStatus != AdminReviewStatus.Approved)
                return Unsupported(request.ModuleDefinitionId, "This module is not available for practice right now.");

            var activityLinks = await _db.ModuleDefinitionActivityLinks
                .AsNoTracking()
                .Where(l => l.ModuleDefinitionId == request.ModuleDefinitionId)
                .OrderBy(l => l.SortOrder)
                .ToListAsync(ct);

            if (activityLinks.Count == 0)
                return Unsupported(request.ModuleDefinitionId, "This module has no launchable practice activity.");

            var activityDefIds = activityLinks.Select(l => l.ActivityDefinitionId).ToList();
            var activityDefsById = await _db.ActivityDefinitions
                .AsNoTracking()
                .Where(a => activityDefIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, ct);

            ActivityDefinition? eligibleActivity = null;
            string? unsupportedReason = null;

            foreach (var link in activityLinks)
            {
                if (!activityDefsById.TryGetValue(link.ActivityDefinitionId, out var candidate))
                    continue;

                var eligibility = ActivityDefinitionLaunchEligibility.Evaluate(candidate);
                if (eligibility.CanLaunch)
                {
                    eligibleActivity = candidate;
                    break;
                }

                unsupportedReason ??= eligibility.UnsupportedReason;
            }

            if (eligibleActivity is null)
                return Unsupported(request.ModuleDefinitionId,
                    unsupportedReason ?? "This module contains an activity type that is not launchable yet.");

            // Optional traceability/display: an Approved linked Learn Item, if any.
            var learnItemLink = await _db.ModuleDefinitionLearnItemLinks
                .AsNoTracking()
                .Where(l => l.ModuleDefinitionId == request.ModuleDefinitionId)
                .OrderBy(l => l.SortOrder)
                .FirstOrDefaultAsync(ct);

            LearnItem? approvedLearnItem = null;
            if (learnItemLink is not null)
            {
                var learnItem = await _db.LearnItems
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == learnItemLink.LearnItemId, ct);
                if (learnItem is not null && learnItem.ReviewStatus == AdminReviewStatus.Approved)
                    approvedLearnItem = learnItem;
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

            var launch = new StudentActivityDefinitionLaunch(
                studentId: request.StudentId,
                moduleDefinitionId: request.ModuleDefinitionId,
                activityDefinitionId: eligibleActivity.Id,
                learningActivityId: learningActivity.Id,
                source: request.Source,
                launchedAt: DateTimeOffset.UtcNow,
                learnItemId: approvedLearnItem?.Id);
            _db.StudentActivityDefinitionLaunches.Add(launch);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "ActivityDefinitionLaunch: student={StudentId} module={ModuleDefinitionId} activity={ActivityDefinitionId} learningActivity={LearningActivityId} source={Source}",
                request.StudentId, request.ModuleDefinitionId, eligibleActivity.Id, learningActivity.Id, request.Source);

            return new ActivityDefinitionLaunchResult(
                Success: true,
                UnsupportedReason: null,
                ModuleDefinitionId: request.ModuleDefinitionId,
                ActivityDefinitionId: eligibleActivity.Id,
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
                LearnItem: approvedLearnItem is null ? null : ToLearnItemSummary(approvedLearnItem));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ActivityDefinitionLaunch failed safely for student {StudentId} module {ModuleDefinitionId} — existing Practice Gym fallback remains available.",
                request.StudentId, request.ModuleDefinitionId);
            return Unsupported(request.ModuleDefinitionId, "This activity could not be started right now. Please try again shortly.");
        }
    }

    private static ActivityDefinitionLaunchResult Unsupported(Guid moduleDefinitionId, string reason) => new(
        Success: false,
        UnsupportedReason: reason,
        ModuleDefinitionId: moduleDefinitionId,
        ActivityDefinitionId: null,
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
        LearnItem: null);

    /// <summary>The Domain <see cref="ActivityType"/> enum predates H4 and has no gap-fill/
    /// multiple-choice member — it's a coarse reporting category, not a renderer selector (Form.io
    /// rendering is driven by <see cref="Domain.Entities.LearningActivity.FormIoSchemaJson"/>
    /// alone, per <c>ActivitySubmitHandler</c>). A skill-based best-effort mapping is enough;
    /// exactly matches the imprecision the existing ActivityTemplate Form.io pilot already
    /// accepts for its own shared marker pattern.</summary>
    private static ActivityType MapToLearningActivityType(ActivityDefinition activityDefinition) =>
        activityDefinition.Skill?.Contains("reading", StringComparison.OrdinalIgnoreCase) == true
            ? ActivityType.ReadingTask
            : ActivityType.VocabularyPractice;

    private static PracticeGymModuleLearnItemSummary ToLearnItemSummary(LearnItem item) => new(
        LearnItemId: item.Id,
        Title: item.Title,
        Body: item.Body,
        Examples: [],
        CommonMistakes: [],
        UsageNotes: item.UsageNotes);
}
