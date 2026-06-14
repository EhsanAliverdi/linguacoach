using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Append-only audit record of an admin-performed student lifecycle reset.
/// See: docs/architecture/student-lifecycle-reset-tools.md
/// </summary>
public sealed class StudentResetLog : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public Guid AdminUserId { get; private set; }
    public StudentLifecycleStage PreviousStage { get; private set; }
    public StudentLifecycleStage NewStage { get; private set; }

    /// <summary>Serialised ClearedItems response object.</summary>
    public string ClearedItemsJson { get; private set; }

    public string Reason { get; private set; }
    public string CorrelationId { get; private set; }
    public DateTime PerformedAtUtc { get; private set; }

    private StudentResetLog()
    {
        ClearedItemsJson = "{}";
        Reason = string.Empty;
        CorrelationId = string.Empty;
    }

    public StudentResetLog(
        Guid studentProfileId,
        Guid adminUserId,
        StudentLifecycleStage previousStage,
        StudentLifecycleStage newStage,
        string clearedItemsJson,
        string reason,
        string correlationId)
    {
        if (studentProfileId == Guid.Empty)
            throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (adminUserId == Guid.Empty)
            throw new ArgumentException("AdminUserId must not be empty.", nameof(adminUserId));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));
        if (string.IsNullOrWhiteSpace(clearedItemsJson))
            throw new ArgumentException("ClearedItemsJson is required.", nameof(clearedItemsJson));

        StudentProfileId = studentProfileId;
        AdminUserId = adminUserId;
        PreviousStage = previousStage;
        NewStage = newStage;
        ClearedItemsJson = clearedItemsJson;
        Reason = reason.Trim();
        CorrelationId = correlationId;
        PerformedAtUtc = DateTime.UtcNow;
    }
}
