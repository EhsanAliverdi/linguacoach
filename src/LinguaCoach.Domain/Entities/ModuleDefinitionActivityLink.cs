using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>Phase H5 — links a <see cref="ModuleDefinition"/> to one of the
/// <see cref="ActivityDefinition"/>s it's built from.</summary>
public sealed class ModuleDefinitionActivityLink : BaseEntity
{
    public Guid ModuleDefinitionId { get; private set; }
    public Guid ActivityDefinitionId { get; private set; }
    public ModuleActivityRole Role { get; private set; }
    public int SortOrder { get; private set; }
    public bool Required { get; private set; }
    public string? SnapshotTitle { get; private set; }

    private ModuleDefinitionActivityLink() { }

    public ModuleDefinitionActivityLink(
        Guid moduleDefinitionId, Guid activityDefinitionId, ModuleActivityRole role,
        int sortOrder, bool required = true, string? snapshotTitle = null)
    {
        if (moduleDefinitionId == Guid.Empty)
            throw new ArgumentException("ModuleDefinitionId must not be empty.", nameof(moduleDefinitionId));
        if (activityDefinitionId == Guid.Empty)
            throw new ArgumentException("ActivityDefinitionId must not be empty.", nameof(activityDefinitionId));

        ModuleDefinitionId = moduleDefinitionId;
        ActivityDefinitionId = activityDefinitionId;
        Role = role;
        SortOrder = sortOrder;
        Required = required;
        SnapshotTitle = snapshotTitle?.Trim();
    }
}
