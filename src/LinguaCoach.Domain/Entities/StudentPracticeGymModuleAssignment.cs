using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase H7 — a lightweight, additive bookkeeping record of which <see cref="Module"/>
/// (if any) the deterministic Practice Gym module selector suggested to a student, or that no
/// suitable Module existed and legacy Practice Gym suggestions were used instead
/// (<see cref="PracticeGymModuleAssignmentStatus.FallbackOnly"/>). Exists purely for admin
/// diagnostics and a 14-day reuse-guard lookup — it is <b>not</b> a student attempt/score record:
/// no answer, score, or mastery state is ever stored here. Mirrors
/// <see cref="StudentDailyModuleAssignment"/>'s (H6) convention, adapted for Practice Gym's
/// suggest-then-optionally-select flow — unlike Today's per-date assignment, a Practice Gym
/// suggestion is timestamped (<see cref="SuggestedAt"/>) rather than bound to a calendar date.
/// </summary>
public sealed class StudentPracticeGymModuleAssignment : BaseEntity
{
    public Guid StudentId { get; private set; }

    /// <summary>Null only when <see cref="Status"/> is
    /// <see cref="PracticeGymModuleAssignmentStatus.FallbackOnly"/> — no Module was suggested,
    /// so there is nothing to link to.</summary>
    public Guid? ModuleId { get; private set; }

    public DateTimeOffset SuggestedAt { get; private set; }

    public PracticeGymModuleAssignmentStatus Status { get; private set; }
    public string? SelectionReason { get; private set; }
    public string? FallbackReason { get; private set; }

    public DateTimeOffset? SelectedAt { get; private set; }
    public DateTimeOffset? DismissedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    private StudentPracticeGymModuleAssignment() { }

    public StudentPracticeGymModuleAssignment(
        Guid studentId,
        Guid? moduleId,
        DateTimeOffset suggestedAt,
        PracticeGymModuleAssignmentStatus status,
        string? selectionReason = null,
        string? fallbackReason = null)
    {
        if (studentId == Guid.Empty)
            throw new ArgumentException("StudentId must not be empty.", nameof(studentId));
        if (moduleId == Guid.Empty)
            throw new ArgumentException("ModuleId must not be empty when provided.", nameof(moduleId));
        if (moduleId is null && status != PracticeGymModuleAssignmentStatus.FallbackOnly)
            throw new ArgumentException("ModuleId is required unless status is FallbackOnly.", nameof(moduleId));

        StudentId = studentId;
        ModuleId = moduleId;
        SuggestedAt = suggestedAt;
        Status = status;
        SelectionReason = selectionReason?.Trim();
        FallbackReason = fallbackReason?.Trim();
    }

    public void MarkPresented() => Status = PracticeGymModuleAssignmentStatus.Presented;

    public void MarkExpired() => Status = PracticeGymModuleAssignmentStatus.Expired;
}
