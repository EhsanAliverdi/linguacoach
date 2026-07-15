using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase 4 (Part D — asset grouping) — links one <see cref="ResourceCandidate"/> to one
/// <see cref="ImportAsset"/> with a role in that candidate's context (an asset's own
/// <see cref="ImportAsset.Role"/> is package-wide; this link's <see cref="Role"/> is
/// candidate-specific and may differ — e.g. a shared licence asset's own role is
/// <see cref="ImportAssetRole.Licence"/>, and every candidate links to it with the same role).
/// Many-to-many by design: one candidate can link several assets (a Listening candidate's audio +
/// transcript + licence), and one asset can be linked by several candidates (a shared licence or
/// source-reference file).
/// </summary>
public sealed class ImportCandidateAssetLink : BaseEntity
{
    public Guid ResourceCandidateId { get; private set; }
    public Guid ImportAssetId { get; private set; }
    public ImportAssetRole Role { get; private set; }

    private ImportCandidateAssetLink() { }

    public ImportCandidateAssetLink(Guid resourceCandidateId, Guid importAssetId, ImportAssetRole role)
    {
        if (resourceCandidateId == Guid.Empty)
            throw new ArgumentException("ResourceCandidateId must not be empty.", nameof(resourceCandidateId));
        if (importAssetId == Guid.Empty)
            throw new ArgumentException("ImportAssetId must not be empty.", nameof(importAssetId));

        ResourceCandidateId = resourceCandidateId;
        ImportAssetId = importAssetId;
        Role = role;
    }
}
