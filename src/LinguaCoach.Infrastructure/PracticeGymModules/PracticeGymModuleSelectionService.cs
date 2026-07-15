using System.Text.Json;
using LinguaCoach.Application.Modules;
using LinguaCoach.Application.PracticeGymModules;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.PracticeGymModules;

/// <summary>
/// Phase H7 — deterministic (no AI) Practice Gym module selector. Pure/read-only: never writes to
/// the database, never mutates a <see cref="Module"/>/<see cref="Lesson"/>/
/// <see cref="Exercise"/>, never creates Module attempts or mastery updates. Never
/// throws for "no suitable content" — degrades to
/// <see cref="PracticeGymModuleSelectionResult.FallbackRequired"/> instead, and the outer
/// try/catch guarantees the same for any unexpected error, so a caller can always safely fall
/// back to the existing readiness-pool-backed Practice Gym suggestions. Mirrors H6's
/// <c>TodayPlanModuleSelectionService</c>, extended for Practice Gym's self-directed
/// skill/subskill/objective request and weakness-signal soft preferences.
/// </summary>
public sealed class PracticeGymModuleSelectionService : IPracticeGymModuleSelectionService
{
    private static readonly TimeSpan ReuseCooldown = TimeSpan.FromDays(14);

    private readonly LinguaCoachDbContext _db;

    public PracticeGymModuleSelectionService(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<PracticeGymModuleSelectionResult> SelectAsync(
        PracticeGymModuleSelectionRequest request, CancellationToken ct = default)
    {
        var warnings = new List<string>();

        try
        {
            var targetCefr = NormalizeCefr(request.CefrLevel);
            var now = DateTimeOffset.UtcNow;

            var candidateModules = await _db.Modules
                .AsNoTracking()
                .Where(ModuleEligibility.AvailableForNewStudentDeliveryExpr)
                .ToListAsync(ct);

            if (candidateModules.Count == 0)
                return Fallback(targetCefr, warnings, "No approved Modules exist yet.");

            var moduleIds = candidateModules.Select(m => m.Id).ToList();

            var lessonLinks = await _db.ModuleLessonLinks
                .AsNoTracking()
                .Where(l => moduleIds.Contains(l.ModuleId))
                .ToListAsync(ct);
            var exerciseLinks = await _db.ModuleExerciseLinks
                .AsNoTracking()
                .Where(l => moduleIds.Contains(l.ModuleId))
                .ToListAsync(ct);

            var lessonIds = lessonLinks.Select(l => l.LessonId).Distinct().ToList();
            var exerciseIds = exerciseLinks.Select(l => l.ExerciseId).Distinct().ToList();

            var lessonsById = await _db.Lessons
                .AsNoTracking()
                .Where(i => lessonIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, ct);
            var activityDefsById = await _db.Exercises
                .AsNoTracking()
                .Where(a => exerciseIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, ct);

            var lessonLinksByModule = lessonLinks.ToLookup(l => l.ModuleId);
            var exerciseLinksByModule = exerciseLinks.ToLookup(l => l.ModuleId);

            var cooldownStart = now.Subtract(ReuseCooldown);
            // SQLite's EF provider cannot translate DateTimeOffset comparisons server-side, so
            // this table's rows for the student (a small, naturally student-scoped set) are
            // fetched first and the cooldown window applied client-side.
            var studentAssignments = await _db.StudentPracticeGymModuleAssignments
                .AsNoTracking()
                .Where(a => a.StudentId == request.StudentId && a.ModuleId != null)
                .ToListAsync(ct);
            var recentModuleIds = new HashSet<Guid>(
                studentAssignments
                    .Where(a => a.SuggestedAt >= cooldownStart)
                    .Select(a => a.ModuleId!.Value));

            if (request.RecentSuggestedModuleIds is not null)
                foreach (var id in request.RecentSuggestedModuleIds)
                    recentModuleIds.Add(id);

            var eligible = new List<(Module Module, List<Lesson> Learns, List<Exercise> Activities)>();

            foreach (var module in candidateModules)
            {
                if (recentModuleIds.Contains(module.Id))
                    continue;

                var approvedLessons = lessonLinksByModule[module.Id]
                    .Select(l => lessonsById.GetValueOrDefault(l.LessonId))
                    .Where(i => i is not null && i.ReviewStatus == AdminReviewStatus.Approved)
                    .Select(i => i!)
                    .ToList();

                var approvedActivities = exerciseLinksByModule[module.Id]
                    .Select(l => activityDefsById.GetValueOrDefault(l.ExerciseId))
                    .Where(a => a is not null && a.ReviewStatus == AdminReviewStatus.Approved)
                    .Select(a => a!)
                    .ToList();

                if (approvedLessons.Count == 0 || approvedActivities.Count == 0)
                    continue;

                eligible.Add((module, approvedLessons, approvedActivities));
            }

            if (eligible.Count == 0)
                return Fallback(targetCefr, warnings,
                    "No approved Module has both an approved Lesson and an approved Exercise available (or every compatible Module was recently suggested).");

            // Requested skill/subskill/objective narrow the pool when the student picked one
            // explicitly (self-directed Practice Gym) — but only when doing so still leaves at
            // least one candidate; an over-narrow request degrades to the broader eligible pool
            // rather than a hard failure, since Practice Gym selection is a soft preference here.
            var pool = eligible;

            if (!string.IsNullOrWhiteSpace(request.RequestedObjectiveKey))
            {
                var objectiveMatches = eligible
                    .Where(e => string.Equals(e.Module.ObjectiveKey, request.RequestedObjectiveKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (objectiveMatches.Count > 0)
                    pool = objectiveMatches;
            }

            if (!string.IsNullOrWhiteSpace(request.RequestedSkill))
            {
                var skillMatches = pool
                    .Where(e => string.Equals(e.Module.Skill, request.RequestedSkill, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (skillMatches.Count > 0)
                    pool = skillMatches;
            }

            if (!string.IsNullOrWhiteSpace(request.RequestedSubskill))
            {
                var subskillMatches = pool
                    .Where(e => string.Equals(e.Module.Subskill, request.RequestedSubskill, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (subskillMatches.Count > 0)
                    pool = subskillMatches;
            }

            var usedBroadenedCefr = false;

            if (targetCefr is not null)
            {
                var exactMatches = pool
                    .Where(e => e.Module.CefrLevel is null || string.Equals(e.Module.CefrLevel, targetCefr, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (exactMatches.Count > 0)
                {
                    pool = exactMatches;
                }
                else if (request.AllowFallback)
                {
                    usedBroadenedCefr = true;
                    warnings.Add($"No Module matched CEFR level '{targetCefr}'; broadened to all eligible Modules as a review/scaffold/remediation selection.");
                }
                else
                {
                    return Fallback(targetCefr, warnings, $"No Module matched CEFR level '{targetCefr}' and fallback broadening is disabled.");
                }
            }

            if (pool.Count == 0)
                return Fallback(targetCefr, warnings, "No eligible Module remained after skill/subskill/objective/CEFR filtering.");

            var scored = pool
                .Select(e => (Entry: e, Score: ScoreModule(e.Module, request)))
                .OrderByDescending(x => x.Score)
                .ToList();

            var suggestions = new List<PracticeGymModuleSuggestion>();
            var usedSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var maxSuggestions = Math.Max(1, request.MaxSuggestions);

            foreach (var (entry, _) in scored)
            {
                if (suggestions.Count >= maxSuggestions)
                    break;

                // Balanced-coverage guard only applies when the student didn't request a specific
                // skill — a self-directed request should be honored, not diversified away from.
                if (string.IsNullOrWhiteSpace(request.RequestedSkill)
                    && suggestions.Count > 0 && entry.Module.Skill is not null && usedSkills.Contains(entry.Module.Skill))
                {
                    var moreDiverseOptionRemains = scored.Any(s =>
                        s.Entry.Module.Id != entry.Module.Id
                        && !suggestions.Exists(sug => sug.ModuleId == s.Entry.Module.Id)
                        && (s.Entry.Module.Skill is null || !usedSkills.Contains(s.Entry.Module.Skill)));

                    if (moreDiverseOptionRemains)
                        continue;
                }

                var isLowerLevel = usedBroadenedCefr && IsLowerCefrLevel(entry.Module.CefrLevel, targetCefr);
                var isRemediation = request.WeaknessSignals is { Count: > 0 }
                    && entry.Module.Skill is not null
                    && request.WeaknessSignals.Contains(entry.Module.Skill, StringComparer.OrdinalIgnoreCase);

                // Phase H10 — precompute launch eligibility so the client can show Start/disabled
                // without an extra round trip. Re-validated fresh at actual launch time too.
                var launchEligibility = entry.Activities
                    .Select(Application.ExerciseLaunch.ExerciseLaunchEligibility.Evaluate)
                    .FirstOrDefault(e => e.CanLaunch)
                    ?? entry.Activities.Select(Application.ExerciseLaunch.ExerciseLaunchEligibility.Evaluate).FirstOrDefault()
                    ?? new Application.ExerciseLaunch.ExerciseLaunchEligibilityResult(false, "This module has no launchable practice activity.");

                suggestions.Add(new PracticeGymModuleSuggestion(
                    ModuleId: entry.Module.Id,
                    Title: entry.Module.Title,
                    Description: entry.Module.Description,
                    CefrLevel: entry.Module.CefrLevel,
                    Skill: entry.Module.Skill,
                    Subskill: entry.Module.Subskill,
                    DifficultyBand: entry.Module.DifficultyBand,
                    EstimatedMinutes: entry.Module.EstimatedMinutes,
                    ContextTags: SafeParseStringArray(entry.Module.ContextTagsJson),
                    FocusTags: SafeParseStringArray(entry.Module.FocusTagsJson),
                    Reason: BuildReason(entry.Module, request, usedBroadenedCefr, isRemediation),
                    IsReview: isLowerLevel,
                    IsScaffold: usedBroadenedCefr && entry.Module.CefrLevel is null,
                    IsRemediation: isRemediation,
                    LinkedLessonSummaries: entry.Learns.Select(ToLessonSummary).ToList(),
                    LinkedActivitySummaries: entry.Activities.Select(ToActivitySummary).ToList(),
                    CanLaunch: launchEligibility.CanLaunch,
                    UnsupportedReason: launchEligibility.UnsupportedReason));

                if (entry.Module.Skill is not null)
                    usedSkills.Add(entry.Module.Skill);
            }

            if (suggestions.Count == 0)
                return Fallback(targetCefr, warnings, "No Module could be selected after scoring.");

            return new PracticeGymModuleSelectionResult(
                Suggestions: suggestions,
                FallbackRequired: false,
                FallbackReason: null,
                SelectionReason: suggestions[0].Reason,
                TargetCefrLevel: targetCefr,
                Warnings: warnings);
        }
        catch (Exception ex)
        {
            warnings.Add($"Module selection failed safely and fell back to legacy Practice Gym suggestions: {ex.Message}");
            return Fallback(NormalizeCefr(request.CefrLevel), warnings,
                "Module selection encountered an unexpected error; using legacy Practice Gym fallback.");
        }
    }

    private static PracticeGymModuleSelectionResult Fallback(string? targetCefr, List<string> warnings, string reason) => new(
        Suggestions: [],
        FallbackRequired: true,
        FallbackReason: reason,
        SelectionReason: null,
        TargetCefrLevel: targetCefr,
        Warnings: warnings);

    private static double ScoreModule(Module module, PracticeGymModuleSelectionRequest request)
    {
        double score = 0;

        if (!string.IsNullOrWhiteSpace(request.RequestedSkill)
            && string.Equals(module.Skill, request.RequestedSkill, StringComparison.OrdinalIgnoreCase))
            score += 20;

        if (!string.IsNullOrWhiteSpace(request.RequestedSubskill)
            && string.Equals(module.Subskill, request.RequestedSubskill, StringComparison.OrdinalIgnoreCase))
            score += 12;

        if (!string.IsNullOrWhiteSpace(request.RequestedObjectiveKey)
            && string.Equals(module.ObjectiveKey, request.RequestedObjectiveKey, StringComparison.OrdinalIgnoreCase))
            score += 15;

        if (request.WeaknessSignals is { Count: > 0 } && module.Skill is not null
            && request.WeaknessSignals.Contains(module.Skill, StringComparer.OrdinalIgnoreCase))
            score += 8;

        if (request.FocusAreas is { Count: > 0 })
        {
            var focusTags = SafeParseStringArray(module.FocusTagsJson);
            if (focusTags.Any(t => request.FocusAreas.Contains(t, StringComparer.OrdinalIgnoreCase)))
                score += 6;
        }

        if (request.ContextTags is { Count: > 0 })
        {
            var contextTags = SafeParseStringArray(module.ContextTagsJson);
            var overlap = contextTags.Count(t => request.ContextTags.Contains(t, StringComparer.OrdinalIgnoreCase));
            score += overlap * 2;
        }

        if (request.RequestedDifficulty is { } preferredDifficulty && module.DifficultyBand is { } band)
        {
            var diff = Math.Abs(preferredDifficulty - band);
            score += Math.Max(0, 3 - diff);
        }

        return score;
    }

    private static string BuildReason(
        Module module, PracticeGymModuleSelectionRequest request, bool usedBroadenedCefr, bool isRemediation)
    {
        var parts = new List<string>();

        if (isRemediation)
            parts.Add("remediation: matches a recent weakness signal");

        if (usedBroadenedCefr)
        {
            parts.Add("review/scaffold selection: broadened beyond an exact CEFR match as a fallback because no compatible Module matched the student's level");
        }
        else if (module.CefrLevel is not null && request.CefrLevel is not null
            && string.Equals(module.CefrLevel, request.CefrLevel, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"matches student CEFR level {module.CefrLevel}");
        }

        if (!string.IsNullOrWhiteSpace(request.RequestedSkill)
            && string.Equals(module.Skill, request.RequestedSkill, StringComparison.OrdinalIgnoreCase))
            parts.Add($"matches requested skill {module.Skill}");

        if (!string.IsNullOrWhiteSpace(request.RequestedSubskill)
            && string.Equals(module.Subskill, request.RequestedSubskill, StringComparison.OrdinalIgnoreCase))
            parts.Add($"matches requested subskill {module.Subskill}");

        if (parts.Count == 0)
            parts.Add("selected by deterministic Practice Gym module selector");

        return string.Join("; ", parts);
    }

    private static bool IsLowerCefrLevel(string? moduleCefr, string? targetCefr)
    {
        if (moduleCefr is null || targetCefr is null)
            return false;

        var moduleIndex = CefrLevelConstants.All.ToList().FindIndex(l => string.Equals(l, moduleCefr, StringComparison.OrdinalIgnoreCase));
        var targetIndex = CefrLevelConstants.All.ToList().FindIndex(l => string.Equals(l, targetCefr, StringComparison.OrdinalIgnoreCase));
        return moduleIndex >= 0 && targetIndex >= 0 && moduleIndex < targetIndex;
    }

    private static string? NormalizeCefr(string? cefr) =>
        string.IsNullOrWhiteSpace(cefr) ? null : cefr.Trim().ToUpperInvariant();

    private static List<string> SafeParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static PracticeGymModuleLessonSummary ToLessonSummary(Lesson item) => new(
        LessonId: item.Id,
        Title: item.Title,
        Body: item.Body,
        Examples: SafeParseStringArray(item.ExamplesJson),
        CommonMistakes: SafeParseStringArray(item.CommonMistakesJson),
        UsageNotes: item.UsageNotes);

    private static PracticeGymModuleActivitySummary ToActivitySummary(Exercise activity) => new(
        ExerciseId: activity.Id,
        Title: activity.Title,
        Description: activity.Description,
        Instructions: activity.Instructions,
        ActivityType: activity.ActivityType,
        FormSchemaJson: activity.FormSchemaJson,
        EstimatedMinutes: activity.EstimatedMinutes);
}
