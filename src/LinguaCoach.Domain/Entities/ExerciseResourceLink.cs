using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase H4 — traces an <see cref="Exercise"/> back to the published Resource Bank
/// row(s) it was generated from or is otherwise about. Structurally identical to
/// <see cref="LessonResourceLink"/> (same "typed discriminator + id" shape, same
/// <see cref="PublishedResourceType"/> and <see cref="LessonResourceRole"/> enums reused
/// rather than duplicated) — kept as a separate table/entity so an Activity's and a Lesson's
/// resource links can be queried, indexed, and cascade-deleted independently.
/// </summary>
public sealed class ExerciseResourceLink : BaseEntity
{
    public Guid ExerciseId { get; private set; }
    public PublishedResourceType ResourceType { get; private set; }
    public Guid ResourceId { get; private set; }
    public LessonResourceRole Role { get; private set; }
    public string? SnapshotTitle { get; private set; }
    public string? ContentFingerprint { get; private set; }

    private ExerciseResourceLink() { }

    public ExerciseResourceLink(
        Guid exerciseId,
        PublishedResourceType resourceType,
        Guid resourceId,
        LessonResourceRole role,
        string? snapshotTitle = null,
        string? contentFingerprint = null)
    {
        if (exerciseId == Guid.Empty)
            throw new ArgumentException("ExerciseId must not be empty.", nameof(exerciseId));
        if (resourceId == Guid.Empty)
            throw new ArgumentException("ResourceId must not be empty.", nameof(resourceId));

        ExerciseId = exerciseId;
        ResourceType = resourceType;
        ResourceId = resourceId;
        Role = role;
        SnapshotTitle = snapshotTitle?.Trim();
        ContentFingerprint = contentFingerprint?.Trim();
    }
}
