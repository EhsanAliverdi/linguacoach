using LinguaCoach.Domain.Constants;

namespace LinguaCoach.UnitTests.Domain;

public sealed class CurriculumContextTagConstantsTests
{
    [Theory]
    [InlineData(CurriculumContextTagConstants.GeneralEnglish)]
    [InlineData(CurriculumContextTagConstants.DayToDay)]
    [InlineData(CurriculumContextTagConstants.Travel)]
    [InlineData(CurriculumContextTagConstants.StudyAcademic)]
    [InlineData(CurriculumContextTagConstants.MigrationSettlement)]
    [InlineData(CurriculumContextTagConstants.JobInterviews)]
    [InlineData(CurriculumContextTagConstants.SocialConversation)]
    [InlineData(CurriculumContextTagConstants.Workplace)]
    public void IsGoalTag_ReturnsTrueForMotivationTags(string tag)
    {
        Assert.True(CurriculumContextTagConstants.IsGoalTag(tag));
    }

    [Theory]
    [InlineData(CurriculumContextTagConstants.Pronunciation)]
    [InlineData(CurriculumContextTagConstants.ListeningConfidence)]
    [InlineData(CurriculumContextTagConstants.WritingConfidence)]
    [InlineData(CurriculumContextTagConstants.ExamInspired)]
    [InlineData(CurriculumContextTagConstants.Custom)]
    public void IsGoalTag_ReturnsFalseForNonMotivationTags(string tag)
    {
        Assert.False(CurriculumContextTagConstants.IsGoalTag(tag));
    }

    [Fact]
    public void IsGoalTag_ReturnsFalseForNullOrUnknown()
    {
        Assert.False(CurriculumContextTagConstants.IsGoalTag(null));
        Assert.False(CurriculumContextTagConstants.IsGoalTag("not_a_real_tag"));
    }

    [Fact]
    public void GoalTags_IsSubsetOfAll()
    {
        foreach (var tag in CurriculumContextTagConstants.GoalTags)
            Assert.Contains(tag, CurriculumContextTagConstants.All);
    }

    [Fact]
    public void GoalTags_HasEightEntries()
    {
        Assert.Equal(8, CurriculumContextTagConstants.GoalTags.Count);
    }
}
