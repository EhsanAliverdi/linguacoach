using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class SkillGraphPrerequisiteEdgeTests
{
    [Fact]
    public void Constructor_ValidInput_CreatesEdge()
    {
        var nodeId = Guid.NewGuid();
        var prereqId = Guid.NewGuid();

        var edge = new SkillGraphPrerequisiteEdge(nodeId, prereqId);

        Assert.Equal(nodeId, edge.NodeId);
        Assert.Equal(prereqId, edge.PrerequisiteNodeId);
    }

    [Fact]
    public void Constructor_EmptyNodeId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SkillGraphPrerequisiteEdge(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_EmptyPrerequisiteNodeId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SkillGraphPrerequisiteEdge(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void Constructor_SelfReference_Throws()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new SkillGraphPrerequisiteEdge(id, id));
    }
}
