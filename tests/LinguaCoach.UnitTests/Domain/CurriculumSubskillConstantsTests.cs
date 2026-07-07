using LinguaCoach.Domain.Constants;

namespace LinguaCoach.UnitTests.Domain;

public sealed class CurriculumSubskillConstantsTests
{
    [Fact]
    public void All_ContainsNoDuplicates()
    {
        var distinctCount = CurriculumSubskillConstants.All
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.Equal(CurriculumSubskillConstants.All.Count, distinctCount);
    }

    [Fact]
    public void All_ValuesAreAllValid()
    {
        foreach (var subskill in CurriculumSubskillConstants.All)
            Assert.True(CurriculumSubskillConstants.IsValid(subskill), $"'{subskill}' should be valid.");
    }

    [Theory]
    [InlineData(CurriculumSubskillConstants.ReadingGist, true)]
    [InlineData(CurriculumSubskillConstants.SpeakingRoleplay, true)]
    [InlineData("not_a_real_subskill", false)]
    [InlineData(null, false)]
    public void IsValid_ReturnsExpected(string? subskill, bool expected)
    {
        Assert.Equal(expected, CurriculumSubskillConstants.IsValid(subskill));
    }

    [Fact]
    public void ForSkill_ReturnsOnlySubskillsBelongingToThatSkill()
    {
        var readingSubskills = CurriculumSubskillConstants.ForSkill(CurriculumSkillConstants.Reading);

        Assert.NotEmpty(readingSubskills);
        Assert.All(readingSubskills, s => Assert.StartsWith("reading.", s));
    }

    [Fact]
    public void ForSkill_UnknownSkill_ReturnsEmpty()
    {
        Assert.Empty(CurriculumSubskillConstants.ForSkill("not_a_real_skill"));
    }

    [Theory]
    [InlineData(CurriculumSkillConstants.Speaking, CurriculumSubskillConstants.SpeakingFluency, true)]
    [InlineData(CurriculumSkillConstants.Speaking, CurriculumSubskillConstants.WritingEmailMessage, false)]
    [InlineData(CurriculumSkillConstants.Speaking, null, true)] // null always allowed
    public void IsValidForSkill_RejectsMismatchedSkillSubskillPairs(string skill, string? subskill, bool expected)
    {
        Assert.Equal(expected, CurriculumSubskillConstants.IsValidForSkill(skill, subskill));
    }
}
