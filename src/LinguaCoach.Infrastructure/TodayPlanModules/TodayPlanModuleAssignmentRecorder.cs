using LinguaCoach.Application.TodayPlanModules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.TodayPlanModules;

/// <summary>
/// Phase H6 (renamed I4 Pass 3) — the one write path for the Today Plan module pipeline:
/// idempotent per-day upsert of <see cref="StudentTodayPlanModuleAssignment"/> bookkeeping rows.
/// A Module was actually selected that day gets one row per selected Module
/// (<see cref="TodayPlanModuleAssignmentStatus.Selected"/>); when the selector required a
/// fallback, no row is written — the schema has no reliable standalone key without a Module to
/// record, so "no row for this student/date" is itself the fallback signal for admin diagnostics.
/// </summary>
public sealed class TodayPlanModuleAssignmentRecorder : ITodayPlanModuleAssignmentRecorder
{
    private readonly LinguaCoachDbContext _db;

    public TodayPlanModuleAssignmentRecorder(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(
        Guid studentId,
        DateTime targetDate,
        TodayPlanModuleSelectionResult selectionResult,
        CancellationToken ct = default)
    {
        var date = targetDate.Date;

        var alreadyRecorded = await _db.StudentTodayPlanModuleAssignments
            .AnyAsync(a => a.StudentId == studentId && a.AssignedForDate == date, ct);
        if (alreadyRecorded)
            return;

        if (selectionResult.FallbackRequired || selectionResult.SelectedModules.Count == 0)
            return;

        foreach (var module in selectionResult.SelectedModules)
        {
            _db.StudentTodayPlanModuleAssignments.Add(new StudentTodayPlanModuleAssignment(
                studentId,
                module.ModuleId,
                date,
                TodayPlanModuleAssignmentStatus.Selected,
                selectionReason: module.Reason,
                fallbackReason: null,
                estimatedMinutes: module.EstimatedMinutes));
        }

        await _db.SaveChangesAsync(ct);
    }
}
