using System.Text.Json;
using LinguaCoach.Application.Composer;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.Modules;
using LinguaCoach.Application.TodayPlanModules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.TodayPlanModules;

/// <summary>
/// Phase H6 (renamed I4 Pass 3) — Today Plan module selector. Pure/read-only: never writes to the
/// database, never mutates a <see cref="Module"/>/<see cref="Lesson"/>/<see cref="Exercise"/>,
/// never creates Practice Gym or attempt records. Never throws for "no suitable content" —
/// degrades to <see cref="TodayPlanModuleSelectionResult.FallbackRequired"/> instead, and the outer
/// try/catch guarantees the same for any unexpected error, so a caller can always safely fall back
/// to legacy Today content.
///
/// Adaptive Curriculum Sprint 5 — eligibility (Approved/non-archived, has an approved Lesson+
/// Exercise, CEFR match, the 14-day reuse-cooldown) stays deterministic and unchanged; the
/// mechanical CEFR+tag <c>ScoreModule</c> heuristic that used to rank the eligible pool has been
/// replaced with <see cref="ICurriculumComposerService"/> — an AI composer that reasons over
/// goal-vector relevance, skill-graph mastery gaps, and a 3-day skill-novelty signal computed here
/// deterministically and handed to it as real per-candidate facts. Hard cutover: no fallback to the
/// old scoring heuristic — see docs/reviews/2026-07-20-adaptive-curriculum-sprint5-ai-composer-review.md.
/// </summary>
public sealed class TodayPlanModuleSelectionService : ITodayPlanModuleSelectionService
{
    private static readonly TimeSpan ReuseCooldown = TimeSpan.FromDays(14);
    private static readonly TimeSpan NoveltyWindow = TimeSpan.FromDays(3);
    private const double GoalMatchWeightThreshold = 0.5;

    private readonly LinguaCoachDbContext _db;
    private readonly ICurriculumComposerService _composer;
    private readonly IStudentMasteryEvaluationService _mastery;

    public TodayPlanModuleSelectionService(
        LinguaCoachDbContext db, ICurriculumComposerService composer, IStudentMasteryEvaluationService mastery)
    {
        _db = db;
        _composer = composer;
        _mastery = mastery;
    }

    public async Task<TodayPlanModuleSelectionResult> SelectAsync(
        TodayPlanModuleSelectionRequest request, CancellationToken ct = default)
    {
        var warnings = new List<string>();

        try
        {
            var targetCefr = NormalizeCefr(request.CefrLevel);
            var targetDate = request.TargetDate.Date;

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

            var cooldownStart = targetDate.Subtract(ReuseCooldown);
            var recentAssignments = await _db.StudentTodayPlanModuleAssignments
                .AsNoTracking()
                .Where(a => a.StudentId == request.StudentId
                    && a.ModuleId != null
                    && a.AssignedForDate >= cooldownStart
                    && a.AssignedForDate < targetDate)
                .Select(a => new { a.ModuleId, a.AssignedForDate })
                .ToListAsync(ct);

            var recentModuleIds = new HashSet<Guid>(recentAssignments.Select(a => a.ModuleId!.Value));

            if (request.RecentAssignedModuleIds is not null)
                foreach (var id in request.RecentAssignedModuleIds)
                    recentModuleIds.Add(id);

            // Adaptive Curriculum Sprint 5 — a tighter novelty band than the hard cooldown above:
            // skills (not specific Modules) practised in the last 3 days, handed to the composer as
            // a soft "you just did this" signal rather than a hard exclusion.
            var noveltyWindowStart = targetDate.Subtract(NoveltyWindow);
            var recentlyPractisedModuleIds = recentAssignments
                .Where(a => a.AssignedForDate >= noveltyWindowStart)
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
                    "No approved Module has both an approved Lesson and an approved Exercise available (or every compatible Module was recently used).");

            var pool = eligible;
            var usedBroadenedCefr = false;

            if (targetCefr is not null)
            {
                var exactMatches = eligible
                    .Where(e => e.Module.CefrLevel is null || string.Equals(e.Module.CefrLevel, targetCefr, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (exactMatches.Count > 0)
                {
                    pool = exactMatches;
                }
                else if (request.AllowFallback)
                {
                    usedBroadenedCefr = true;
                    warnings.Add($"No Module matched CEFR level '{targetCefr}'; broadened to all eligible Modules as a review/scaffold selection.");
                }
                else
                {
                    return Fallback(targetCefr, warnings, $"No Module matched CEFR level '{targetCefr}' and fallback broadening is disabled.");
                }
            }

            if (pool.Count == 0)
                return Fallback(targetCefr, warnings, "No eligible Module remained after CEFR filtering.");

            var maxModules = Math.Max(1, request.MaxModules);

            var weaknessModuleIds = await ResolveWeaknessMatchModuleIdsAsync(request.StudentId, pool, ct);
            var topGoalTags = await ResolveTopGoalTagsAsync(request.StudentId, ct);
            var primaryNodeKeyByModuleId = await ResolvePrimaryNodeKeysAsync(pool, ct);

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
                IsWeaknessMatch: weaknessModuleIds.Contains(e.Module.Id),
                IsGoalMatch: topGoalTags.Count > 0
                    && SafeParseStringArray(e.Module.ContextTagsJson).Any(t => topGoalTags.Contains(t)),
                RecentlyPractisedSameSkill: e.Module.Skill is not null && recentlyPractisedSkills.Contains(e.Module.Skill)))
                .ToList();

            var composerResult = await _composer.RankCandidatesAsync(new ComposerRankingRequest(
                StudentId: request.StudentId,
                SurfaceName: "Today",
                Candidates: composerCandidates,
                MaxResults: maxModules,
                RequestedSkill: request.RequestedSkill,
                PreferredSessionLengthMinutes: request.PreferredSessionLengthMinutes), ct);

            // Sprint 9 bugfix — RequestedSkill must be a soft preference the composer can ignore
            // when it leaves no candidates (this is the actual contract every caller/doc-comment
            // already claims), not a hard filter. Live-confirmed: the AI composer took the hint
            // literally and returned an empty ranking (with a real, sensible reason — "No content
            // matched the requested 'skill'" — that RankCandidatesAsync's own Success=false/empty
            // convention for "zero ranked ids" discards) when the pool had zero Modules of that
            // skill — which used to never happen because RequestedSkill was always null before
            // this sprint wired it in. Not gated on composerResult.Success: RankCandidatesAsync
            // returns Success=false for this exact "valid response, zero ids" case too, so a
            // Success==true check here would never fire. Degrade to the broad pool exactly once,
            // mirroring Practice Gym's own narrow-then-degrade pattern, before falling back to
            // legacy Today content.
            if (composerResult.RankedModuleIds.Count == 0 && request.RequestedSkill is not null)
            {
                warnings.Add($"No content matched the requested skill '{request.RequestedSkill}' — broadened to all skills.");
                composerResult = await _composer.RankCandidatesAsync(new ComposerRankingRequest(
                    StudentId: request.StudentId,
                    SurfaceName: "Today",
                    Candidates: composerCandidates,
                    MaxResults: maxModules,
                    RequestedSkill: null,
                    PreferredSessionLengthMinutes: request.PreferredSessionLengthMinutes), ct);
            }

            if (!composerResult.Success || composerResult.RankedModuleIds.Count == 0)
                return Fallback(targetCefr, warnings,
                    $"AI composer could not select content: {composerResult.FailureReason ?? composerResult.SelectionReason ?? "no candidate was ranked"}.");

            var byModuleId = pool.ToDictionary(e => e.Module.Id);
            var selected = new List<SelectedModuleResult>();
            var totalMinutes = 0;

            foreach (var moduleId in composerResult.RankedModuleIds)
            {
                if (!byModuleId.TryGetValue(moduleId, out var entry)) continue; // defensive; composer already validates ids

                selected.Add(new SelectedModuleResult(
                    ModuleId: entry.Module.Id,
                    Title: entry.Module.Title,
                    Description: entry.Module.Description,
                    CefrLevel: entry.Module.CefrLevel,
                    Skill: entry.Module.Skill,
                    Subskill: entry.Module.Subskill,
                    DifficultyBand: entry.Module.DifficultyBand,
                    EstimatedMinutes: entry.Module.EstimatedMinutes,
                    Reason: BuildReason(entry.Module, request, usedBroadenedCefr,
                        weaknessModuleIds.Contains(entry.Module.Id)),
                    LinkedLessons: entry.Learns.Select(ToLessonView).ToList(),
                    LinkedExercises: entry.Activities.Select(ToActivityView).ToList()));

                totalMinutes += entry.Module.EstimatedMinutes ?? 0;
            }

            if (selected.Count == 0)
                return Fallback(targetCefr, warnings, "AI composer's ranked ids did not match any eligible candidate.");

            return new TodayPlanModuleSelectionResult(
                SelectedModules: selected,
                FallbackRequired: false,
                FallbackReason: null,
                SelectionReason: composerResult.SelectionReason ?? selected[0].Reason,
                TargetCefrLevel: targetCefr,
                TotalEstimatedMinutes: totalMinutes,
                Warnings: warnings);
        }
        catch (Exception ex)
        {
            warnings.Add($"Module selection failed safely and fell back to legacy Today content: {ex.Message}");
            return Fallback(NormalizeCefr(request.CefrLevel), warnings,
                "Module selection encountered an unexpected error; using legacy Today fallback.");
        }
    }

    private static TodayPlanModuleSelectionResult Fallback(string? targetCefr, List<string> warnings, string reason) => new(
        SelectedModules: [],
        FallbackRequired: true,
        FallbackReason: reason,
        SelectionReason: null,
        TargetCefrLevel: targetCefr,
        TotalEstimatedMinutes: 0,
        Warnings: warnings);

    /// <summary>Adaptive Curriculum Sprint 5 — resolves which of the pool's Modules cover a
    /// skill-graph node the student is currently Weak/AtRisk on, via
    /// <see cref="IStudentMasteryEvaluationService.EvaluateStudentAsync"/> (Sprint 4's node-based
    /// grouping) joined through <c>ModuleSkillGraphNodeLink</c>. A real, deterministic fact handed
    /// to the composer — never inferred by the AI itself.</summary>
    private async Task<HashSet<Guid>> ResolveWeaknessMatchModuleIdsAsync(
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

    /// <summary>Adaptive Curriculum Sprint 5 — the student's top-weighted <c>StudentGoalWeight</c>
    /// tags (Sprint 3), a real, deterministic fact handed to the composer.</summary>
    private async Task<HashSet<string>> ResolveTopGoalTagsAsync(Guid studentId, CancellationToken ct) =>
        new(
            await _db.StudentGoalWeights.AsNoTracking()
                .Where(g => g.StudentId == studentId && g.Weight >= GoalMatchWeightThreshold)
                .Select(g => g.GoalTag)
                .ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

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

    private static string BuildReason(
        Module module, TodayPlanModuleSelectionRequest request, bool usedBroadenedCefr, bool isWeaknessMatch)
    {
        var parts = new List<string>();

        if (isWeaknessMatch)
            parts.Add("addresses a current skill-graph mastery gap");

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

        if (parts.Count == 0)
            parts.Add("selected by the AI curriculum composer");

        return string.Join("; ", parts);
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

    private static TodayPlanLessonView ToLessonView(Lesson item) => new(
        LessonId: item.Id,
        Title: item.Title,
        Body: item.Body,
        Examples: SafeParseStringArray(item.ExamplesJson),
        CommonMistakes: SafeParseStringArray(item.CommonMistakesJson),
        UsageNotes: item.UsageNotes);

    private static TodayPlanActivityView ToActivityView(Exercise activity) => new(
        ExerciseId: activity.Id,
        Title: activity.Title,
        Description: activity.Description,
        Instructions: activity.Instructions,
        ActivityType: activity.ActivityType,
        FormSchemaJson: activity.FormSchemaJson,
        EstimatedMinutes: activity.EstimatedMinutes);
}
