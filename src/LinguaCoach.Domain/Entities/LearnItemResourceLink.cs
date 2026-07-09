using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase H3 — traces a <see cref="LearnItem"/> back to the published Resource Bank row(s)
/// (<c>CefrVocabularyEntry</c>/<c>CefrGrammarProfileEntry</c>/<c>CefrReadingReference</c>/
/// <c>CefrReadingPassage</c>) it was generated from or is otherwise about. Deliberately a plain
/// link row keyed by (<see cref="ResourceType"/>, <see cref="ResourceId"/>) rather than four
/// separate nullable FK columns — mirrors the same "typed discriminator + id" shape
/// <c>ResourceCandidate.PublishedEntityType</c>/<c>PublishedEntityId</c> already uses for the same
/// kind of cross-table reference, so no physical FK to four different tables is needed.
/// </summary>
public sealed class LearnItemResourceLink : BaseEntity
{
    public Guid LearnItemId { get; private set; }
    public PublishedResourceType ResourceType { get; private set; }
    public Guid ResourceId { get; private set; }
    public LearnItemResourceRole Role { get; private set; }

    /// <summary>Denormalized snapshot of the resource's title/word/grammar-point at link time, so
    /// the link stays readable even if the underlying typed row is later edited or deleted.</summary>
    public string? SnapshotTitle { get; private set; }

    /// <summary>Copied from the resource at link time where available (only <c>CefrReadingPassage</c>
    /// carries a <c>ContentFingerprint</c> column today) — null for the other three types, not
    /// recomputed.</summary>
    public string? ContentFingerprint { get; private set; }

    private LearnItemResourceLink() { }

    public LearnItemResourceLink(
        Guid learnItemId,
        PublishedResourceType resourceType,
        Guid resourceId,
        LearnItemResourceRole role,
        string? snapshotTitle = null,
        string? contentFingerprint = null)
    {
        if (learnItemId == Guid.Empty)
            throw new ArgumentException("LearnItemId must not be empty.", nameof(learnItemId));
        if (resourceId == Guid.Empty)
            throw new ArgumentException("ResourceId must not be empty.", nameof(resourceId));

        LearnItemId = learnItemId;
        ResourceType = resourceType;
        ResourceId = resourceId;
        Role = role;
        SnapshotTitle = snapshotTitle?.Trim();
        ContentFingerprint = contentFingerprint?.Trim();
    }
}
