using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Assigns a specific usage policy to a student, overriding the global default.
/// </summary>
public sealed class StudentPolicyAssignment : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public StudentProfile? StudentProfile { get; private set; }

    public Guid UsagePolicyId { get; private set; }
    public UsagePolicy? UsagePolicy { get; private set; }

    public Guid AssignedByAdminUserId { get; private set; }
    public string? Reason { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private StudentPolicyAssignment()
    {
    }

    public StudentPolicyAssignment(
        Guid studentProfileId,
        Guid usagePolicyId,
        Guid assignedByAdminUserId,
        string? reason)
    {
        StudentProfileId = studentProfileId;
        UsagePolicyId = usagePolicyId;
        AssignedByAdminUserId = assignedByAdminUserId;
        Reason = reason?.Trim();
        IsActive = true;
        UpdatedAt = CreatedAt;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
