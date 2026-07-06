using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>A named, versioned Form.io-driven student flow (onboarding today; FlowKind.Placement
/// reserved but unused — placement stays on its own adaptive PlacementItemDefinition model).</summary>
public sealed class StudentFlowTemplate : BaseEntity
{
    public StudentFlowKind FlowKind { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public StudentFlowTemplateStatus Status { get; private set; }
    public Guid? ActiveVersionId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<StudentFlowTemplateVersion> _versions = new();
    public IReadOnlyList<StudentFlowTemplateVersion> Versions => _versions.AsReadOnly();

    private StudentFlowTemplate() { }

    public StudentFlowTemplate(StudentFlowKind flowKind, string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

        FlowKind = flowKind;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Status = StudentFlowTemplateStatus.Draft;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddVersion(StudentFlowTemplateVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        _versions.Add(version);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActiveVersion(Guid versionId)
    {
        ActiveVersionId = versionId;
        Status = StudentFlowTemplateStatus.Published;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Archive()
    {
        Status = StudentFlowTemplateStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
