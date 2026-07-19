using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class ModuleSkillGraphNodeLinkTests
{
    [Fact]
    public void Constructor_ValidInput_CreatesLink()
    {
        var moduleId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();

        var link = new ModuleSkillGraphNodeLink(moduleId, nodeId, 0.8);

        Assert.Equal(moduleId, link.ModuleId);
        Assert.Equal(nodeId, link.SkillGraphNodeId);
        Assert.Equal(0.8, link.Confidence);
    }

    [Fact]
    public void Constructor_NullConfidence_IsAllowed()
    {
        var link = new ModuleSkillGraphNodeLink(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(link.Confidence);
    }

    [Fact]
    public void Constructor_EmptyModuleId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ModuleSkillGraphNodeLink(Guid.Empty, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_EmptySkillGraphNodeId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ModuleSkillGraphNodeLink(Guid.NewGuid(), Guid.Empty));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Constructor_ConfidenceOutOfRange_Throws(double confidence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModuleSkillGraphNodeLink(Guid.NewGuid(), Guid.NewGuid(), confidence));
    }
}
