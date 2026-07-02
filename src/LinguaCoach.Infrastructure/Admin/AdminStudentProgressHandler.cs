using LinguaCoach.Application.Admin;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AdminStudentProgressHandler : IAdminStudentProgressQuery
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILearningPlanService _learningPlan;
    private readonly ILogger<AdminStudentProgressHandler> _logger;

    public AdminStudentProgressHandler(
        LinguaCoachDbContext db,
        ILearningPlanService learningPlan,
        ILogger<AdminStudentProgressHandler> logger)
    {
        _db = db;
        _learningPlan = learningPlan;
        _logger = logger;
    }

    public async Task<AdminStudentProgressResult?> HandleAsync(
        AdminStudentProgressQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.Id == query.StudentProfileId, ct);
        if (profile == null) return null;

        // Sequential, not parallel: all loaders below share the single scoped
        // DbContext (including transitively via ILearningPlanService), which EF
        // Core does not allow to run more than one operation on concurrently.
        // Running these via Task.WhenAll intermittently threw
        // "A second operation was started on this context instance before a
        // previous operation completed" and surfaced as a 500 in admin.
        var plan = await LoadPlanProgressAsync(query.StudentProfileId, ct);
        var (placementDate, placementCefr) = await LoadPlacementAsync(query.StudentProfileId, ct);
        var (strongest, weakest, weakCount) = await LoadSkillSummaryAsync(query.StudentProfileId, ct);
        var lastActivity = await LoadLastActivityAsync(query.StudentProfileId, ct);

        return new AdminStudentProgressResult(
            CurrentCefrLevel: profile.CefrLevel,
            PlacementCefrLevel: placementCefr,
            PlacementCompletedAt: placementDate,
            MasteredObjectivesCount: plan?.ObjectivesMastered ?? 0,
            InProgressObjectivesCount: plan?.ObjectivesInProgress ?? 0,
            ReviewQueueCount: plan?.ReviewObjectives ?? 0,
            TotalObjectives: plan?.TotalObjectives ?? 0,
            CompletionPercentage: plan?.CompletionPercentage ?? 0,
            StrongestSkill: strongest,
            WeakestSkill: weakest,
            WeakSkillsCount: weakCount,
            LastLearningActivityAt: lastActivity,
            CurrentLearningPhase: plan?.CurrentLearningPhase ?? "Not started");
    }

    private async Task<LearningPlanProgressSummary?> LoadPlanProgressAsync(
        Guid studentProfileId, CancellationToken ct)
    {
        try { return await _learningPlan.GetProgressAsync(studentProfileId, ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin: could not load plan progress for {Id}", studentProfileId);
            return null;
        }
    }

    private async Task<(DateTime? Date, string? Cefr)> LoadPlacementAsync(
        Guid studentProfileId, CancellationToken ct)
    {
        var p = await _db.PlacementAssessments
            .Where(a => a.StudentProfileId == studentProfileId
                     && a.Status == PlacementStatus.Completed)
            .OrderByDescending(a => a.CompletedAtUtc)
            .Select(a => new { a.CompletedAtUtc, a.OverallEstimatedLevel })
            .FirstOrDefaultAsync(ct);
        return (p?.CompletedAtUtc, p?.OverallEstimatedLevel);
    }

    private async Task<(string? Strongest, string? Weakest, int WeakCount)> LoadSkillSummaryAsync(
        Guid studentProfileId, CancellationToken ct)
    {
        var skills = await _db.StudentSkillProfiles
            .Where(s => s.StudentProfileId == studentProfileId)
            .Select(s => new { s.SkillLabel, s.IsWeak, s.ScorePercent })
            .ToListAsync(ct);

        if (skills.Count == 0) return (null, null, 0);

        var strongest = skills.Where(s => !s.IsWeak)
            .OrderByDescending(s => s.ScorePercent)
            .Select(s => s.SkillLabel)
            .FirstOrDefault();

        var weakest = skills.Where(s => s.IsWeak)
            .OrderBy(s => s.ScorePercent)
            .Select(s => s.SkillLabel)
            .FirstOrDefault();

        var weakCount = skills.Count(s => s.IsWeak);
        return (strongest, weakest, weakCount);
    }

    private async Task<DateTime?> LoadLastActivityAsync(
        Guid studentProfileId, CancellationToken ct)
    {
        return await _db.StudentLearningEvents
            .Where(e => e.StudentProfileId == studentProfileId)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Select(e => (DateTime?)e.OccurredAtUtc)
            .FirstOrDefaultAsync(ct);
    }
}
