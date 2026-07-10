using System.Text.Json;
using LinguaCoach.Application.DailyLessonModules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.DailyLessonModules;

/// <summary>
/// Phase H6 — deterministic (no AI) Daily Lesson module selector. Pure/read-only: never writes to
/// the database, never mutates a <see cref="Module"/>/<see cref="Lesson"/>/
/// <see cref="Exercise"/>, never creates Practice Gym or attempt records. Never throws
/// for "no suitable content" — degrades to <see cref="DailyLessonModuleSelectionResult.FallbackRequired"/>
/// instead, and the outer try/catch guarantees the same for any unexpected error, so a caller can
/// always safely fall back to legacy Today content.
/// </summary>
public sealed class DailyLessonModuleSelectionService : IDailyLessonModuleSelectionService
{
    private static readonly TimeSpan ReuseCooldown = TimeSpan.FromDays(14);

    private readonly LinguaCoachDbContext _db;

    public DailyLessonModuleSelectionService(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<DailyLessonModuleSelectionResult> SelectAsync(
        DailyLessonModuleSelectionRequest request, CancellationToken ct = default)
    {
        var warnings = new List<string>();

        try
        {
            var targetCefr = NormalizeCefr(request.CefrLevel);
            var targetDate = request.TargetDate.Date;

            var candidateModules = await _db.Modules
                .AsNoTracking()
                .Where(m => m.ReviewStatus == AdminReviewStatus.Approved)
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
            var recentModuleIds = new HashSet<Guid>(
                await _db.StudentDailyModuleAssignments
                    .AsNoTracking()
                    .Where(a => a.StudentId == request.StudentId
                        && a.ModuleId != null
                        && a.AssignedForDate >= cooldownStart
                        && a.AssignedForDate < targetDate)
                    .Select(a => a.ModuleId!.Value)
                    .ToListAsync(ct));

            if (request.RecentAssignedModuleIds is not null)
                foreach (var id in request.RecentAssignedModuleIds)
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

            var scored = pool
                .Select(e => (Entry: e, Score: ScoreModule(e.Module, request)))
                .OrderByDescending(x => x.Score)
                .ToList();

            var selected = new List<SelectedModuleResult>();
            var usedSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var totalMinutes = 0;
            var maxModules = Math.Max(1, request.MaxModules);

            foreach (var (entry, _) in scored)
            {
                if (selected.Count >= maxModules)
                    break;

                if (selected.Count > 0 && entry.Module.Skill is not null && usedSkills.Contains(entry.Module.Skill))
                {
                    var moreDiverseOptionRemains = scored.Any(s =>
                        s.Entry.Module.Id != entry.Module.Id
                        && !selected.Exists(sel => sel.ModuleId == s.Entry.Module.Id)
                        && (s.Entry.Module.Skill is null || !usedSkills.Contains(s.Entry.Module.Skill)));

                    if (moreDiverseOptionRemains)
                        continue;
                }

                selected.Add(new SelectedModuleResult(
                    ModuleId: entry.Module.Id,
                    Title: entry.Module.Title,
                    Description: entry.Module.Description,
                    CefrLevel: entry.Module.CefrLevel,
                    Skill: entry.Module.Skill,
                    Subskill: entry.Module.Subskill,
                    DifficultyBand: entry.Module.DifficultyBand,
                    EstimatedMinutes: entry.Module.EstimatedMinutes,
                    Reason: BuildReason(entry.Module, request, usedBroadenedCefr),
                    LinkedLessons: entry.Learns.Select(ToLessonView).ToList(),
                    LinkedExercises: entry.Activities.Select(ToActivityView).ToList()));

                if (entry.Module.Skill is not null)
                    usedSkills.Add(entry.Module.Skill);
                totalMinutes += entry.Module.EstimatedMinutes ?? 0;
            }

            if (selected.Count == 0)
                return Fallback(targetCefr, warnings, "No Module could be selected after scoring.");

            return new DailyLessonModuleSelectionResult(
                SelectedModules: selected,
                FallbackRequired: false,
                FallbackReason: null,
                SelectionReason: selected[0].Reason,
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

    private static DailyLessonModuleSelectionResult Fallback(string? targetCefr, List<string> warnings, string reason) => new(
        SelectedModules: [],
        FallbackRequired: true,
        FallbackReason: reason,
        SelectionReason: null,
        TargetCefrLevel: targetCefr,
        TotalEstimatedMinutes: 0,
        Warnings: warnings);

    private static double ScoreModule(Module module, DailyLessonModuleSelectionRequest request)
    {
        double score = 0;

        if (!string.IsNullOrWhiteSpace(request.RequestedSkill)
            && string.Equals(module.Skill, request.RequestedSkill, StringComparison.OrdinalIgnoreCase))
            score += 10;

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

        if (request.PreferredSessionLengthMinutes is { } preferred && module.EstimatedMinutes is { } minutes)
        {
            var diff = Math.Abs(preferred - minutes);
            score += Math.Max(0, 5 - diff / 5.0);
        }

        return score;
    }

    private static string BuildReason(Module module, DailyLessonModuleSelectionRequest request, bool usedBroadenedCefr)
    {
        var parts = new List<string>();

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
            parts.Add("selected by deterministic Daily Lesson module selector");

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

    private static DailyLessonLessonView ToLessonView(Lesson item) => new(
        LessonId: item.Id,
        Title: item.Title,
        Body: item.Body,
        Examples: SafeParseStringArray(item.ExamplesJson),
        CommonMistakes: SafeParseStringArray(item.CommonMistakesJson),
        UsageNotes: item.UsageNotes);

    private static DailyLessonActivityView ToActivityView(Exercise activity) => new(
        ExerciseId: activity.Id,
        Title: activity.Title,
        Description: activity.Description,
        Instructions: activity.Instructions,
        ActivityType: activity.ActivityType,
        FormSchemaJson: activity.FormSchemaJson,
        EstimatedMinutes: activity.EstimatedMinutes);
}
