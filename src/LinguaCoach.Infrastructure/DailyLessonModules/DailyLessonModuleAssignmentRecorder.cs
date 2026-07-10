using LinguaCoach.Application.DailyLessonModules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.DailyLessonModules;

/// <summary>
/// Phase H6 — the one write path for the Daily Lesson module pipeline: idempotent per-day upsert
/// of <see cref="StudentDailyModuleAssignment"/> bookkeeping rows. A Module was actually selected
/// that day gets one row per selected Module (<see cref="DailyModuleAssignmentStatus.Selected"/>);
/// when the selector required a fallback, no row is written — the schema has no reliable
/// standalone key without a Module to record, so "no row for this student/date" is itself the
/// fallback signal for admin diagnostics.
/// </summary>
public sealed class DailyLessonModuleAssignmentRecorder : IDailyLessonModuleAssignmentRecorder
{
    private readonly LinguaCoachDbContext _db;

    public DailyLessonModuleAssignmentRecorder(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(
        Guid studentId,
        DateTime targetDate,
        DailyLessonModuleSelectionResult selectionResult,
        CancellationToken ct = default)
    {
        var date = targetDate.Date;

        var alreadyRecorded = await _db.StudentDailyModuleAssignments
            .AnyAsync(a => a.StudentId == studentId && a.AssignedForDate == date, ct);
        if (alreadyRecorded)
            return;

        if (selectionResult.FallbackRequired || selectionResult.SelectedModules.Count == 0)
            return;

        foreach (var module in selectionResult.SelectedModules)
        {
            _db.StudentDailyModuleAssignments.Add(new StudentDailyModuleAssignment(
                studentId,
                module.ModuleId,
                date,
                DailyModuleAssignmentStatus.Selected,
                selectionReason: module.Reason,
                fallbackReason: null,
                estimatedMinutes: module.EstimatedMinutes));
        }

        await _db.SaveChangesAsync(ct);
    }
}
