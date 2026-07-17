using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Sprint 1 of the Adaptive Curriculum initiative — a directed prerequisite edge in the skill
/// graph: <see cref="PrerequisiteNodeId"/> must be mastered before <see cref="NodeId"/> is
/// considered ready. Self-referencing join table, following <see cref="ModuleLessonLink"/>'s
/// convention (constructor-validated Guids, private setters, no domain-layer cycle check — cycle
/// detection is a batch-level DFS check, mirroring <c>CurriculumValidationService</c>, run before a
/// pending edge can be approved).
/// </summary>
public sealed class SkillGraphPrerequisiteEdge : BaseEntity
{
    public Guid NodeId { get; private set; }
    public Guid PrerequisiteNodeId { get; private set; }

    private SkillGraphPrerequisiteEdge() { }

    public SkillGraphPrerequisiteEdge(Guid nodeId, Guid prerequisiteNodeId)
    {
        if (nodeId == Guid.Empty)
            throw new ArgumentException("NodeId must not be empty.", nameof(nodeId));
        if (prerequisiteNodeId == Guid.Empty)
            throw new ArgumentException("PrerequisiteNodeId must not be empty.", nameof(prerequisiteNodeId));
        if (nodeId == prerequisiteNodeId)
            throw new ArgumentException("A skill-graph node cannot be its own prerequisite.", nameof(prerequisiteNodeId));

        NodeId = nodeId;
        PrerequisiteNodeId = prerequisiteNodeId;
    }
}
