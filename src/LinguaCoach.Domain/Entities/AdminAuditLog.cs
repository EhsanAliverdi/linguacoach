using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Append-only audit trail for admin governance actions.
/// </summary>
public sealed class AdminAuditLog : BaseEntity
{
    public Guid ActorAdminUserId { get; private set; }
    public Guid? TargetStudentId { get; private set; }
    public string Action { get; private set; }
    public string EntityType { get; private set; }
    public string? EntityId { get; private set; }
    public string? OldValueJson { get; private set; }
    public string? NewValueJson { get; private set; }
    public string? Reason { get; private set; }
    public string? CorrelationId { get; private set; }

    private AdminAuditLog()
    {
        Action = string.Empty;
        EntityType = string.Empty;
    }

    public AdminAuditLog(
        Guid actorAdminUserId,
        string action,
        string entityType,
        string? entityId = null,
        Guid? targetStudentId = null,
        string? oldValueJson = null,
        string? newValueJson = null,
        string? reason = null,
        string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("Action is required.", nameof(action));
        if (string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("EntityType is required.", nameof(entityType));

        ActorAdminUserId = actorAdminUserId;
        Action = action.Trim();
        EntityType = entityType.Trim();
        EntityId = entityId?.Trim();
        TargetStudentId = targetStudentId;
        OldValueJson = oldValueJson;
        NewValueJson = newValueJson;
        Reason = reason?.Trim();
        CorrelationId = correlationId?.Trim();
    }
}
