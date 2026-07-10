using LinguaCoach.Application.PracticeGymModules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.PracticeGymModules;

/// <summary>
/// Phase H7 — the one write path for the Practice Gym module pipeline: records a
/// <see cref="StudentPracticeGymModuleAssignment"/> row per suggested Module. Idempotent per
/// student per calendar day (same convention as H6's <c>TodayPlanModuleAssignmentRecorder</c>)
/// — Practice Gym suggestions are recomputed on every page load, so without this the table would
/// grow unbounded and a module suggested seconds ago would immediately look "recently suggested"
/// to the 14-day reuse guard.
/// </summary>
public sealed class PracticeGymModuleAssignmentRecorder : IPracticeGymModuleAssignmentRecorder
{
    private readonly LinguaCoachDbContext _db;

    public PracticeGymModuleAssignmentRecorder(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(
        Guid studentId,
        PracticeGymModuleSelectionResult selectionResult,
        CancellationToken ct = default)
    {
        if (selectionResult.FallbackRequired || selectionResult.Suggestions.Count == 0)
            return;

        var today = DateTimeOffset.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // SQLite's EF provider cannot translate DateTimeOffset comparisons server-side, so this
        // student's rows are fetched first and today's window applied client-side.
        var studentAssignments = await _db.StudentPracticeGymModuleAssignments
            .Where(a => a.StudentId == studentId && a.ModuleId != null)
            .ToListAsync(ct);
        var alreadyRecordedModuleIds = new HashSet<Guid>(
            studentAssignments
                .Where(a => a.SuggestedAt >= today && a.SuggestedAt < tomorrow)
                .Select(a => a.ModuleId!.Value));

        var now = DateTimeOffset.UtcNow;
        var anyNew = false;

        foreach (var suggestion in selectionResult.Suggestions)
        {
            if (alreadyRecordedModuleIds.Contains(suggestion.ModuleId))
                continue;

            _db.StudentPracticeGymModuleAssignments.Add(new StudentPracticeGymModuleAssignment(
                studentId,
                suggestion.ModuleId,
                now,
                PracticeGymModuleAssignmentStatus.Suggested,
                selectionReason: suggestion.Reason,
                fallbackReason: null));
            anyNew = true;
        }

        if (anyNew)
            await _db.SaveChangesAsync(ct);
    }
}
