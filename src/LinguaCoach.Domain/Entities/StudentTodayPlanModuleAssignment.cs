using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase H6 (renamed I4 Pass 3) — a lightweight, additive bookkeeping record of which
/// <see cref="Module"/> (if any) the deterministic Today Plan module selector chose for a student
/// on a given date, or that no suitable Module existed and legacy Today content was used instead
/// (<see cref="TodayPlanModuleAssignmentStatus.FallbackOnly"/>). Exists purely for admin
/// diagnostics and same-day idempotency/reuse-guard lookups — it is <b>not</b> a student
/// attempt/score record: no answer, score, or mastery state is ever stored here. Mirrors
/// <see cref="StudentActivityReadinessItem"/>'s "routing/selection snapshot" convention at a much
/// smaller scale.
/// </summary>
public sealed class StudentTodayPlanModuleAssignment : BaseEntity
{
    public Guid StudentId { get; private set; }

    /// <summary>Null only when <see cref="Status"/> is <see cref="TodayPlanModuleAssignmentStatus.FallbackOnly"/>
    /// — no Module was selected that day, so there is nothing to link to.</summary>
    public Guid? ModuleId { get; private set; }

    /// <summary>UTC date (time component zeroed) this assignment is for.</summary>
    public DateTime AssignedForDate { get; private set; }

    public TodayPlanModuleAssignmentStatus Status { get; private set; }
    public string? SelectionReason { get; private set; }
    public string? FallbackReason { get; private set; }
    public int? EstimatedMinutes { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    private StudentTodayPlanModuleAssignment() { }

    public StudentTodayPlanModuleAssignment(
        Guid studentId,
        Guid? moduleId,
        DateTime assignedForDate,
        TodayPlanModuleAssignmentStatus status,
        string? selectionReason = null,
        string? fallbackReason = null,
        int? estimatedMinutes = null)
    {
        if (studentId == Guid.Empty)
            throw new ArgumentException("StudentId must not be empty.", nameof(studentId));
        if (moduleId == Guid.Empty)
            throw new ArgumentException("ModuleId must not be empty when provided.", nameof(moduleId));
        if (moduleId is null && status != TodayPlanModuleAssignmentStatus.FallbackOnly)
            throw new ArgumentException("ModuleId is required unless status is FallbackOnly.", nameof(moduleId));

        StudentId = studentId;
        ModuleId = moduleId;
        AssignedForDate = assignedForDate.Date;
        Status = status;
        SelectionReason = selectionReason?.Trim();
        FallbackReason = fallbackReason?.Trim();
        EstimatedMinutes = estimatedMinutes;
    }

    public void MarkPresented() => Status = TodayPlanModuleAssignmentStatus.Presented;

    public void MarkExpired() => Status = TodayPlanModuleAssignmentStatus.Expired;
}
