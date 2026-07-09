using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>Phase H5 — links a <see cref="ModuleDefinition"/> to one of the <see cref="LearnItem"/>s
/// it's built from. Reuses <see cref="LearnItemResourceRole"/> (Primary/Supporting) — the same
/// "what part does this play" concept H3/H4's resource links already use.</summary>
public sealed class ModuleDefinitionLearnItemLink : BaseEntity
{
    public Guid ModuleDefinitionId { get; private set; }
    public Guid LearnItemId { get; private set; }
    public LearnItemResourceRole Role { get; private set; }
    public int SortOrder { get; private set; }
    public string? SnapshotTitle { get; private set; }

    private ModuleDefinitionLearnItemLink() { }

    public ModuleDefinitionLearnItemLink(
        Guid moduleDefinitionId, Guid learnItemId, LearnItemResourceRole role, int sortOrder, string? snapshotTitle = null)
    {
        if (moduleDefinitionId == Guid.Empty)
            throw new ArgumentException("ModuleDefinitionId must not be empty.", nameof(moduleDefinitionId));
        if (learnItemId == Guid.Empty)
            throw new ArgumentException("LearnItemId must not be empty.", nameof(learnItemId));

        ModuleDefinitionId = moduleDefinitionId;
        LearnItemId = learnItemId;
        Role = role;
        SortOrder = sortOrder;
        SnapshotTitle = snapshotTitle?.Trim();
    }
}
