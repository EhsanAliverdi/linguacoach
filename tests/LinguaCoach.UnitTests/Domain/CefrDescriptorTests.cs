using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class CefrDescriptorTests
{
    private static readonly Guid ValidSourceId = Guid.NewGuid();

    [Fact]
    public void Constructor_ValidInput_CreatesDescriptor()
    {
        var descriptor = new CefrDescriptor(
            ValidSourceId, "B1", "speaking", "Can describe experiences and events.");

        Assert.Equal(ValidSourceId, descriptor.SourceId);
        Assert.Equal("B1", descriptor.CefrLevel);
        Assert.Equal("speaking", descriptor.Skill);
        Assert.Null(descriptor.Subskill);
    }

    [Fact]
    public void Constructor_EmptySourceId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CefrDescriptor(Guid.Empty, "B1", "speaking", "Statement"));
    }

    [Fact]
    public void Constructor_InvalidCefrLevel_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CefrDescriptor(ValidSourceId, "X1", "speaking", "Statement"));
    }

    [Fact]
    public void Constructor_InvalidSkill_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CefrDescriptor(ValidSourceId, "B1", "not_a_skill", "Statement"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyCanDoStatement_Throws(string statement)
    {
        Assert.Throws<ArgumentException>(() =>
            new CefrDescriptor(ValidSourceId, "B1", "speaking", statement));
    }

    [Fact]
    public void Constructor_SubskillMatchingSkill_Accepted()
    {
        var descriptor = new CefrDescriptor(
            ValidSourceId, "B1", "speaking", "Statement",
            subskill: CurriculumSubskillConstants.SpeakingRoleplay);

        Assert.Equal(CurriculumSubskillConstants.SpeakingRoleplay, descriptor.Subskill);
    }

    [Fact]
    public void Constructor_SubskillNotMatchingSkill_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new CefrDescriptor(ValidSourceId, "B1", "speaking", "Statement",
                subskill: CurriculumSubskillConstants.WritingEmailMessage));
    }
}
