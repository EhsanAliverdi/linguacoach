using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class LearningModuleTests
{
    private static readonly Guid ValidPathId = Guid.NewGuid();

    [Fact]
    public void Constructor_WithValidArgs_SetsProperties()
    {
        var module = new LearningModule(ValidPathId, "Email writing", "Practice emails", order: 1);

        Assert.Equal(ValidPathId, module.LearningPathId);
        Assert.Equal("Email writing", module.Title);
        Assert.Equal("Practice emails", module.Description);
        Assert.Equal(1, module.Order);
        Assert.Empty(module.Activities);
    }

    [Fact]
    public void Constructor_EmptyLearningPathId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new LearningModule(Guid.Empty, "Title", "Desc", 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyTitle_Throws(string title)
    {
        Assert.Throws<ArgumentException>(() =>
            new LearningModule(ValidPathId, title, "Desc", 1));
    }

    [Fact]
    public void Constructor_NegativeOrder_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LearningModule(ValidPathId, "Title", "Desc", order: -1));
    }

    [Fact]
    public void Constructor_ZeroOrder_IsAllowed()
    {
        var module = new LearningModule(ValidPathId, "Title", "Desc", order: 0);
        Assert.Equal(0, module.Order);
    }

    [Fact]
    public void Constructor_NullDescription_DefaultsToEmpty()
    {
        var module = new LearningModule(ValidPathId, "Title", null!, order: 1);
        Assert.Equal(string.Empty, module.Description);
    }

    [Fact]
    public void Constructor_TrimsTitle()
    {
        var module = new LearningModule(ValidPathId, "  Trimmed  ", "Desc", 1);
        Assert.Equal("Trimmed", module.Title);
    }
}
