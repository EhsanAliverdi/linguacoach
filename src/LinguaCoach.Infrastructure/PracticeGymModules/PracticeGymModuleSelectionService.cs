using System.Text.Json;
using LinguaCoach.Application.Composer;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.Modules;
using LinguaCoach.Application.PracticeGymModules;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.PracticeGymModules;

/// <summary>
/// Phase H7 — Practice Gym module selector. Pure/read-only: never writes to the database, never
/// mutates a <see cref="Module"/>/<see cref="Lesson"/>/<see cref="Exercise"/>, never creates
/// Module attempts or mastery updates. Never throws for "no suitable content" — degrades to
/// <see cref="PracticeGymModuleSelectionResult.FallbackRequired"/> instead, and the outer
/// try/catch guarantees the same for any unexpected error, so a caller can always safely fall
/// back to the existing readiness-pool-backed Practice Gym suggestions. Mirrors H6's
/// <c>TodayPlanModuleSelectionService</c>, extended for Practice Gym's self-directed
/// skill/subskill/objective request and weakness-signal soft preferences.
///
/// Adaptive Curriculum Sprint 5 — eligibility/narrowing (Approved/non-archived, has an approved
/// Lesson+Exercise, self-directed skill/subskill/objective narrowing, CEFR match, the 14-day
/// reuse-cooldown) stays deterministic and unchanged; the mechanical <c>ScoreModule</c> heuristic
/// that used to rank the eligible pool has been replaced with <see cref="ICurriculumComposerService"/>
/// — see <c>TodayPlanModuleSelectionService</c>'s doc comment for the full rationale. Hard cutover:
/// no fallback to the old scoring heuristic.
/// </summary>
public sealed class PracticeGymModuleSelectionService : IPracticeGymModuleSelectionService
{
    private static readonly TimeSpan ReuseCooldown = TimeSpan.FromDays(14);
    private static readonly TimeSpan NoveltyWindow = TimeSpan.FromDays(3);
    private const double GoalMatchWeightThreshold = 0.5;

    private readonly LinguaCoachDbContext _db;
    private readonly ICurriculumComposerService _composer;
    private readonly IStudentMasteryEvaluationService _mastery;

    public PracticeGymModuleSelectionService(
        LinguaCoachDbContext db, ICurriculumComposerService composer, IStudentMasteryEvaluationService mastery)
    {
        _db = db;
        _composer = composer;
        _mastery = mastery;
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

            // Adaptive Curriculum Sprint 5 — a tighter novelty band than the hard cooldown above:
            // skills (not specific Modules) practised in the last 3 days, handed to the composer as
            // a soft "you just did this" signal rather than a hard exclusion.
            var noveltyWindowStart = now.Subtract(NoveltyWindow);
            var recentlyPractisedModuleIds = studentAssignments
                .Where(a => a.SuggestedAt >= noveltyWindowStart)
                .Select(a => a.ModuleId!.Value)
                .ToHashSet();
            var recentlyPractisedSkills = recentlyPractisedModuleIds.Count == 0
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(
                    await _db.Modules.AsNoTracking()
                        .Where(m => recentlyPractisedModuleIds.Contains(m.Id) && m.Skill != null)
                        .Select(m => m.Skill!)
                        .ToListAsync(ct),
                    StringComparer.OrdinalIgnoreCase);

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
                // Adaptive Curriculum Sprint 7 — RequestedObjectiveKey now refers to a
                // SkillGraphNode key (Module.ObjectiveKey was retired this sprint); narrow via the
                // real ModuleSkillGraphNodeLink coverage instead of a free-text field match.
                var moduleIdsForNode = await _db.ModuleSkillGraphNodeLinks.AsNoTracking()
                    .Join(_db.SkillGraphNodes.AsNoTracking().Where(n => n.Key == request.RequestedObjectiveKey),
                        l => l.SkillGraphNodeId, n => n.Id, (l, n) => l.ModuleId)
                    .ToListAsync(ct);

                var objectiveMatches = eligible
                    .Where(e => moduleIdsForNode.Contains(e.Module.Id))
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

            var maxSuggestions = Math.Max(1, request.MaxSuggestions);

            var nodeMasteryWeaknessModuleIds = await ResolveNodeMasteryWeaknessMatchModuleIdsAsync(request.StudentId, pool, ct);
            var topGoalTags = await ResolveTopGoalTagsAsync(request.StudentId, ct);
            var primaryNodeKeyByModuleId = await ResolvePrimaryNodeKeysAsync(pool, ct);

            bool IsRemediation(Module module) =>
                request.WeaknessSignals is { Count: > 0 } && module.Skill is not null
                && request.WeaknessSignals.Contains(module.Skill, StringComparer.OrdinalIgnoreCase);

            var composerCandidates = pool.Select(e => new ComposerCandidate(
                ModuleId: e.Module.Id,
                Title: e.Module.Title,
                Skill: e.Module.Skill,
                Subskill: e.Module.Subskill,
                CefrLevel: e.Module.CefrLevel,
                DifficultyBand: e.Module.DifficultyBand,
                EstimatedMinutes: e.Module.EstimatedMinutes,
                ContextTags: SafeParseStringArray(e.Module.ContextTagsJson),
                FocusTags: SafeParseStringArray(e.Module.FocusTagsJson),
                ObjectiveKey: primaryNodeKeyByModuleId.GetValueOrDefault(e.Module.Id),
                IsWeaknessMatch: nodeMasteryWeaknessModuleIds.Contains(e.Module.Id) || IsRemediation(e.Module),
                IsGoalMatch: topGoalTags.Count > 0
                    && SafeParseStringArray(e.Module.ContextTagsJson).Any(t => topGoalTags.Contains(t)),
                RecentlyPractisedSameSkill: e.Module.Skill is not null && recentlyPractisedSkills.Contains(e.Module.Skill)))
                .ToList();

            var composerResult = await _composer.RankCandidatesAsync(new ComposerRankingRequest(
                StudentId: request.StudentId,
                SurfaceName: "PracticeGym",
                Candidates: composerCandidates,
                MaxResults: maxSuggestions,
                RequestedSkill: request.RequestedSkill,
                RequestedSubskill: request.RequestedSubskill,
                RequestedObjectiveKey: request.RequestedObjectiveKey,
                RequestedDifficulty: request.RequestedDifficulty), ct);

            if (!composerResult.Success || composerResult.RankedModuleIds.Count == 0)
                return Fallback(targetCefr, warnings,
                    $"AI composer could not select content: {composerResult.FailureReason ?? "no candidate was ranked"}.");

            var byModuleId = pool.ToDictionary(e => e.Module.Id);
            var suggestions = new List<PracticeGymModuleSuggestion>();

            foreach (var moduleId in composerResult.RankedModuleIds)
            {
                if (!byModuleId.TryGetValue(moduleId, out var entry)) continue; // defensive; composer already validates ids

                var isLowerLevel = usedBroadenedCefr && IsLowerCefrLevel(entry.Module.CefrLevel, targetCefr);
                var isRemediation = IsRemediation(entry.Module);

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
            }

            if (suggestions.Count == 0)
                return Fallback(targetCefr, warnings, "AI composer's ranked ids did not match any eligible candidate.");

            return new PracticeGymModuleSelectionResult(
                Suggestions: suggestions,
                FallbackRequired: false,
                FallbackReason: null,
                SelectionReason: composerResult.SelectionReason ?? suggestions[0].Reason,
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

    /// <summary>Adaptive Curriculum Sprint 5 — resolves which of the pool's Modules cover a
    /// skill-graph node the student is currently Weak/AtRisk on, via
    /// <see cref="IStudentMasteryEvaluationService.EvaluateStudentAsync"/> (Sprint 4's node-based
    /// grouping) joined through <c>ModuleSkillGraphNodeLink</c>. Distinct from (and combined with,
    /// by the caller) <c>request.WeaknessSignals</c>' ledger-derived weak-skill signal — both are
    /// real, deterministic facts handed to the composer, never inferred by the AI itself.</summary>
    private async Task<HashSet<Guid>> ResolveNodeMasteryWeaknessMatchModuleIdsAsync(
        Guid studentId, List<(Module Module, List<Lesson> Learns, List<Exercise> Activities)> pool, CancellationToken ct)
    {
        var report = await _mastery.EvaluateStudentAsync(studentId, MasteryEvaluationReason.ContentDelivery, ct);
        var gapNodeKeys = new HashSet<string>(
            report.WeakObjectiveKeys.Concat(report.AtRiskObjectiveKeys), StringComparer.OrdinalIgnoreCase);
        if (gapNodeKeys.Count == 0)
            return [];

        var poolModuleIds = pool.Select(e => e.Module.Id).ToList();
        var links = await _db.ModuleSkillGraphNodeLinks.AsNoTracking()
            .Where(l => poolModuleIds.Contains(l.ModuleId))
            .Join(_db.SkillGraphNodes.AsNoTracking(), l => l.SkillGraphNodeId, n => n.Id,
                (l, n) => new { l.ModuleId, n.Key })
            .ToListAsync(ct);

        return links.Where(x => gapNodeKeys.Contains(x.Key)).Select(x => x.ModuleId).ToHashSet();
    }

    /// <summary>Adaptive Curriculum Sprint 7 — one representative skill-graph node key per pool
    /// Module (its first linked node, if any), used only as descriptive context for the composer's
    /// prompt (<c>ComposerCandidate.ObjectiveKey</c>) — replaces the retired <c>Module.ObjectiveKey</c>
    /// free-text field with a real, validated relationship.</summary>
    private async Task<Dictionary<Guid, string>> ResolvePrimaryNodeKeysAsync(
        List<(Module Module, List<Lesson> Learns, List<Exercise> Activities)> pool, CancellationToken ct)
    {
        var poolModuleIds = pool.Select(e => e.Module.Id).ToList();
        var links = await _db.ModuleSkillGraphNodeLinks.AsNoTracking()
            .Where(l => poolModuleIds.Contains(l.ModuleId))
            .Join(_db.SkillGraphNodes.AsNoTracking(), l => l.SkillGraphNodeId, n => n.Id,
                (l, n) => new { l.ModuleId, n.Key })
            .ToListAsync(ct);

        return links
            .GroupBy(x => x.ModuleId)
            .ToDictionary(g => g.Key, g => g.First().Key);
    }

    /// <summary>Adaptive Curriculum Sprint 5 — the student's top-weighted <c>StudentGoalWeight</c>
    /// tags (Sprint 3), a real, deterministic fact handed to the composer.</summary>
    private async Task<HashSet<string>> ResolveTopGoalTagsAsync(Guid studentId, CancellationToken ct) =>
        new(
            await _db.StudentGoalWeights.AsNoTracking()
                .Where(g => g.StudentId == studentId && g.Weight >= GoalMatchWeightThreshold)
                .Select(g => g.GoalTag)
                .ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

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
