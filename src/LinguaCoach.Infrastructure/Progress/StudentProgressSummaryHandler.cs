using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Progress;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Progress;

public sealed class StudentProgressSummaryHandler : IStudentProgressSummaryHandler
{
    private const int RecentActivityLimit = 8;
    private const int RecentSessionLimit = 5;

    private readonly LinguaCoachDbContext _db;
    private readonly ILearningPlanService _learningPlan;
    private readonly ILogger<StudentProgressSummaryHandler> _logger;

    public StudentProgressSummaryHandler(
        LinguaCoachDbContext db,
        ILearningPlanService learningPlan,
        ILogger<StudentProgressSummaryHandler> logger)
    {
        _db = db;
        _learningPlan = learningPlan;
        _logger = logger;
    }

    public async Task<StudentProgressSummaryDto> HandleAsync(
        GetStudentProgressSummaryQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var studentProfileId = profile.Id;

        // Sequential, not parallel: all loaders below share the single scoped
        // DbContext (including transitively via ILearningPlanService), which EF
        // Core does not allow to run more than one operation on concurrently.
        // Running these via Task.WhenAll intermittently threw
        // "A second operation was started on this context instance before a
        // previous operation completed" and surfaced as a raw 500 to students.
        var planProgress = await LoadPlanProgressAsync(studentProfileId, ct);
        var (placementDate, placementCefr) = await LoadPlacementAsync(studentProfileId, ct);
        var skills = await LoadSkillsAsync(studentProfileId, ct);
        var recentActivity = await BuildRecentActivityAsync(studentProfileId, ct);
        var focus = await LoadFocusAsync(studentProfileId, ct);

        var currentCefr = profile.CefrLevel;
        var cefrImproved = IsCefrHigher(placementCefr, currentCefr);

        var learning = BuildLearning(planProgress, currentCefr, placementDate);
        var cefrSection = new StudentProgressCefrDto(
            StartingCefrLevel: placementCefr,
            CurrentCefrLevel: currentCefr,
            CefrImproved: cefrImproved,
            PlacementDate: placementDate,
            Note: placementCefr == null
                ? "Complete a placement assessment to track your starting level."
                : null);

        var mastery = BuildMastery(planProgress, skills);

        return new StudentProgressSummaryDto(
            Learning: learning,
            Skills: skills,
            Cefr: cefrSection,
            Mastery: mastery,
            RecentActivity: recentActivity,
            Focus: focus);
    }

    // ── Loaders ───────────────────────────────────────────────────────────────

    private async Task<LearningPlanProgressSummary?> LoadPlanProgressAsync(
        Guid studentProfileId, CancellationToken ct)
    {
        try
        {
            return await _learningPlan.GetProgressAsync(studentProfileId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load learning plan progress for {Id}", studentProfileId);
            return null;
        }
    }

    private async Task<(DateTime? Date, string? Cefr)> LoadPlacementAsync(
        Guid studentProfileId, CancellationToken ct)
    {
        var placement = await _db.PlacementAssessments
            .Where(p => p.StudentProfileId == studentProfileId
                     && p.Status == PlacementStatus.Completed)
            .OrderByDescending(p => p.CompletedAtUtc)
            .Select(p => new { p.CompletedAtUtc, p.OverallEstimatedLevel })
            .FirstOrDefaultAsync(ct);

        return (placement?.CompletedAtUtc, placement?.OverallEstimatedLevel);
    }

    private async Task<IReadOnlyList<ProgressSkillDto>> LoadSkillsAsync(
        Guid studentProfileId, CancellationToken ct)
    {
        var profiles = await _db.StudentSkillProfiles
            .Where(s => s.StudentProfileId == studentProfileId)
            .OrderBy(s => s.SkillLabel)
            .ToListAsync(ct);

        return profiles
            .Select(s => new ProgressSkillDto(s.SkillKey, s.SkillLabel, s.IsWeak, s.ScorePercent))
            .ToList();
    }

    private async Task<IReadOnlyList<ProgressActivityEventDto>> BuildRecentActivityAsync(
        Guid studentProfileId, CancellationToken ct)
    {
        var events = new List<ProgressActivityEventDto>();

        // Placement completions
        var placements = await _db.PlacementAssessments
            .Where(p => p.StudentProfileId == studentProfileId
                     && p.Status == PlacementStatus.Completed
                     && p.CompletedAtUtc != null)
            .OrderByDescending(p => p.CompletedAtUtc)
            .Take(1)
            .Select(p => new { p.CompletedAtUtc, p.OverallEstimatedLevel })
            .ToListAsync(ct);

        foreach (var p in placements)
        {
            events.Add(new ProgressActivityEventDto(
                EventType: "PlacementCompleted",
                Description: "Placement assessment completed",
                Detail: p.OverallEstimatedLevel != null ? $"Level determined: {p.OverallEstimatedLevel}" : null,
                OccurredAt: p.CompletedAtUtc!.Value));
        }

        // Recent completed sessions (lessons)
        var sessions = await _db.LearningSessions
            .Where(s => s.StudentProfileId == studentProfileId
                     && s.Status == SessionStatus.Completed
                     && s.CompletedAtUtc != null)
            .OrderByDescending(s => s.CompletedAtUtc)
            .Take(RecentSessionLimit)
            .Select(s => new { s.CompletedAtUtc, s.Title })
            .ToListAsync(ct);

        foreach (var s in sessions)
        {
            events.Add(new ProgressActivityEventDto(
                EventType: "LessonCompleted",
                Description: "Lesson completed",
                Detail: s.Title,
                OccurredAt: s.CompletedAtUtc!.Value));
        }

        // Recent practice events from learning ledger (Practice Gym activities)
        var practiceEvents = await _db.StudentLearningEvents
            .Where(e => e.StudentProfileId == studentProfileId
                     && e.Source == LearningEventSource.PracticeGym)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(3)
            .Select(e => new { e.OccurredAtUtc, e.PrimarySkill })
            .ToListAsync(ct);

        foreach (var e in practiceEvents)
        {
            events.Add(new ProgressActivityEventDto(
                EventType: "PracticeCompleted",
                Description: "Practice activity completed",
                Detail: e.PrimarySkill != null ? $"Skill: {CapitalizeSkill(e.PrimarySkill)}" : null,
                OccurredAt: e.OccurredAtUtc));
        }

        // Sort by most recent and cap
        return events
            .OrderByDescending(e => e.OccurredAt)
            .Take(RecentActivityLimit)
            .ToList();
    }

    private async Task<StudentProgressFocusDto> LoadFocusAsync(
        Guid studentProfileId, CancellationToken ct)
    {
        var memory = await _db.UserLearningSummaries
            .FirstOrDefaultAsync(m => m.StudentProfileId == studentProfileId, ct);

        var recommendations = DeserializeList(memory?.NextFocusJson);
        var mistakes = DeserializeList(memory?.RecurringMistakesJson);
        var journey = memory?.JourneySummary;

        return new StudentProgressFocusDto(
            Recommendations: recommendations,
            RecurringMistakes: mistakes,
            JourneySummary: journey);
    }

    // ── Builders ──────────────────────────────────────────────────────────────

    private static StudentProgressLearningSummaryDto BuildLearning(
        LearningPlanProgressSummary? plan, string? currentCefr, DateTime? placementDate)
    {
        if (plan == null)
        {
            return new StudentProgressLearningSummaryDto(
                CurrentCefrLevel: currentCefr,
                PlacementCompletedAt: placementDate,
                CurrentLearningPhase: "Preparing",
                TotalObjectives: 0,
                ObjectivesCompleted: 0,
                ObjectivesMastered: 0,
                ObjectivesInProgress: 0,
                ObjectivesRemaining: 0,
                CompletionPercentage: 0,
                CurrentObjectiveKey: null,
                CurrentObjectiveSkill: null,
                ObjectivesCompletedToday: 0);
        }

        return new StudentProgressLearningSummaryDto(
            CurrentCefrLevel: currentCefr ?? plan.CurrentCefrLevel,
            PlacementCompletedAt: placementDate,
            CurrentLearningPhase: plan.CurrentLearningPhase,
            TotalObjectives: plan.TotalObjectives,
            ObjectivesCompleted: plan.ObjectivesCompleted,
            ObjectivesMastered: plan.ObjectivesMastered,
            ObjectivesInProgress: plan.ObjectivesInProgress,
            ObjectivesRemaining: plan.ObjectivesRemaining,
            CompletionPercentage: plan.CompletionPercentage,
            CurrentObjectiveKey: plan.CurrentObjectiveKey,
            CurrentObjectiveSkill: ExtractSkillFromObjectiveKey(plan.CurrentObjectiveKey),
            ObjectivesCompletedToday: plan.ObjectivesCompletedToday);
    }

    private static StudentProgressMasteryDto BuildMastery(
        LearningPlanProgressSummary? plan, IReadOnlyList<ProgressSkillDto> skills)
    {
        var weakSkills = skills.Where(s => s.IsWeak).ToList();

        return new StudentProgressMasteryDto(
            MasteredObjectivesCount: plan?.ObjectivesMastered ?? 0,
            InProgressObjectivesCount: plan?.ObjectivesInProgress ?? 0,
            ReviewQueueCount: plan?.ReviewObjectives ?? 0,
            WeakSkillsCount: weakSkills.Count,
            WeakSkillLabels: weakSkills.Select(s => s.SkillLabel).ToList());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsCefrHigher(string? baseline, string? current)
    {
        if (baseline == null || current == null) return false;
        var order = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };
        var baseIdx = Array.IndexOf(order, baseline.ToUpper());
        var currIdx = Array.IndexOf(order, current.ToUpper());
        return baseIdx >= 0 && currIdx > baseIdx;
    }

    private static string? ExtractSkillFromObjectiveKey(string? key)
    {
        if (key == null) return null;
        // Convention: "b1_speaking_professional_conversation" → "speaking"
        var parts = key.Split('_');
        return parts.Length >= 2 ? CapitalizeSkill(parts[1]) : null;
    }

    private static string CapitalizeSkill(string skill)
        => string.IsNullOrEmpty(skill) ? skill
            : char.ToUpperInvariant(skill[0]) + skill[1..];

    private static IReadOnlyList<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch { return []; }
    }
}
