using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase H6 — a lightweight, additive bookkeeping record of which <see cref="ModuleDefinition"/>
/// (if any) the deterministic Daily Lesson module selector chose for a student on a given date,
/// or that no suitable Module existed and legacy Today content was used instead
/// (<see cref="DailyModuleAssignmentStatus.FallbackOnly"/>). Exists purely for admin diagnostics
/// and same-day idempotency/reuse-guard lookups — it is <b>not</b> a student attempt/score record:
/// no answer, score, or mastery state is ever stored here. Mirrors
/// <see cref="StudentActivityReadinessItem"/>'s "routing/selection snapshot" convention at a much
/// smaller scale.
/// </summary>
public sealed class StudentDailyModuleAssignment : BaseEntity
{
    public Guid StudentId { get; private set; }

    /// <summary>Null only when <see cref="Status"/> is <see cref="DailyModuleAssignmentStatus.FallbackOnly"/>
    /// — no Module was selected that day, so there is nothing to link to.</summary>
    public Guid? ModuleDefinitionId { get; private set; }

    /// <summary>UTC date (time component zeroed) this assignment is for.</summary>
    public DateTime AssignedForDate { get; private set; }

    public DailyModuleAssignmentStatus Status { get; private set; }
    public string? SelectionReason { get; private set; }
    public string? FallbackReason { get; private set; }
    public int? EstimatedMinutes { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    private StudentDailyModuleAssignment() { }

    public StudentDailyModuleAssignment(
        Guid studentId,
        Guid? moduleDefinitionId,
        DateTime assignedForDate,
        DailyModuleAssignmentStatus status,
        string? selectionReason = null,
        string? fallbackReason = null,
        int? estimatedMinutes = null)
    {
        if (studentId == Guid.Empty)
            throw new ArgumentException("StudentId must not be empty.", nameof(studentId));
        if (moduleDefinitionId == Guid.Empty)
            throw new ArgumentException("ModuleDefinitionId must not be empty when provided.", nameof(moduleDefinitionId));
        if (moduleDefinitionId is null && status != DailyModuleAssignmentStatus.FallbackOnly)
            throw new ArgumentException("ModuleDefinitionId is required unless status is FallbackOnly.", nameof(moduleDefinitionId));

        StudentId = studentId;
        ModuleDefinitionId = moduleDefinitionId;
        AssignedForDate = assignedForDate.Date;
        Status = status;
        SelectionReason = selectionReason?.Trim();
        FallbackReason = fallbackReason?.Trim();
        EstimatedMinutes = estimatedMinutes;
    }

    public void MarkPresented() => Status = DailyModuleAssignmentStatus.Presented;

    public void MarkExpired() => Status = DailyModuleAssignmentStatus.Expired;
}
