using LearningPathEntity = LinguaCoach.Domain.Entities.LearningPath;

namespace LinguaCoach.UnitTests.Domain;

public sealed class LearningPathTests
{
    private static readonly Guid ValidProfileId = Guid.NewGuid();

    [Fact]
    public void Constructor_WithValidArgs_SetsPropertiesAndIsActiveTrue()
    {
        var path = new LearningPathEntity(ValidProfileId, "Workplace English — B1", "Summary");

        Assert.Equal(ValidProfileId, path.StudentProfileId);
        Assert.Equal("Workplace English — B1", path.Title);
        Assert.Equal("Summary", path.LearnerContextSummary);
        Assert.True(path.IsActive);
        Assert.Empty(path.Modules);
    }

    [Fact]
    public void Constructor_EmptyStudentProfileId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new LearningPathEntity(Guid.Empty, "Title", "Summary"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyTitle_Throws(string title)
    {
        Assert.Throws<ArgumentException>(() =>
            new LearningPathEntity(ValidProfileId, title, "Summary"));
    }

    [Fact]
    public void Constructor_NullContextSummary_DefaultsToEmpty()
    {
        var path = new LearningPathEntity(ValidProfileId, "Title", null!);
        Assert.Equal(string.Empty, path.LearnerContextSummary);
    }

    [Fact]
    public void Constructor_TrimsTitle()
    {
        var path = new LearningPathEntity(ValidProfileId, "  Trimmed  ", "Summary");
        Assert.Equal("Trimmed", path.Title);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var path = new LearningPathEntity(ValidProfileId, "Title", "Summary");
        path.Deactivate();
        Assert.False(path.IsActive);
    }
}
