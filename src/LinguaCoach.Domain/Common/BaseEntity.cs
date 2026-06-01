namespace LinguaCoach.Domain.Common;

/// <summary>
/// Base class for all domain entities. No EF Core dependency.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
}
