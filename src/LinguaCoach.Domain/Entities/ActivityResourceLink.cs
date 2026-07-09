using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase H4 — traces an <see cref="ActivityDefinition"/> back to the published Resource Bank
/// row(s) it was generated from or is otherwise about. Structurally identical to
/// <see cref="LearnItemResourceLink"/> (same "typed discriminator + id" shape, same
/// <see cref="PublishedResourceType"/> and <see cref="LearnItemResourceRole"/> enums reused
/// rather than duplicated) — kept as a separate table/entity so an Activity's and a Learn Item's
/// resource links can be queried, indexed, and cascade-deleted independently.
/// </summary>
public sealed class ActivityResourceLink : BaseEntity
{
    public Guid ActivityDefinitionId { get; private set; }
    public PublishedResourceType ResourceType { get; private set; }
    public Guid ResourceId { get; private set; }
    public LearnItemResourceRole Role { get; private set; }
    public string? SnapshotTitle { get; private set; }
    public string? ContentFingerprint { get; private set; }

    private ActivityResourceLink() { }

    public ActivityResourceLink(
        Guid activityDefinitionId,
        PublishedResourceType resourceType,
        Guid resourceId,
        LearnItemResourceRole role,
        string? snapshotTitle = null,
        string? contentFingerprint = null)
    {
        if (activityDefinitionId == Guid.Empty)
            throw new ArgumentException("ActivityDefinitionId must not be empty.", nameof(activityDefinitionId));
        if (resourceId == Guid.Empty)
            throw new ArgumentException("ResourceId must not be empty.", nameof(resourceId));

        ActivityDefinitionId = activityDefinitionId;
        ResourceType = resourceType;
        ResourceId = resourceId;
        Role = role;
        SnapshotTitle = snapshotTitle?.Trim();
        ContentFingerprint = contentFingerprint?.Trim();
    }
}
