using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class StudentGoalWeightTests
{
    [Fact]
    public void Constructor_ValidInput_CreatesWeight()
    {
        var studentId = Guid.NewGuid();
        var weight = new StudentGoalWeight(studentId, CurriculumContextTagConstants.Travel, 0.5, StudentGoalWeightSource.Explicit);

        Assert.Equal(studentId, weight.StudentId);
        Assert.Equal(CurriculumContextTagConstants.Travel, weight.GoalTag);
        Assert.Equal(0.5, weight.Weight);
        Assert.Equal(StudentGoalWeightSource.Explicit, weight.Source);
    }

    [Fact]
    public void Constructor_EmptyStudentId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StudentGoalWeight(Guid.Empty, CurriculumContextTagConstants.Travel, 0.5, StudentGoalWeightSource.Explicit));
    }

    [Fact]
    public void Constructor_NonGoalTag_Throws()
    {
        // Pronunciation is a real CurriculumContextTagConstants value but NOT a goal tag —
        // it's a skill/format descriptor, deliberately excluded from GoalTags.
        Assert.Throws<ArgumentException>(() =>
            new StudentGoalWeight(Guid.NewGuid(), CurriculumContextTagConstants.Pronunciation, 0.5, StudentGoalWeightSource.Explicit));
    }

    [Fact]
    public void Constructor_UnrecognizedTag_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StudentGoalWeight(Guid.NewGuid(), "not_a_real_tag", 0.5, StudentGoalWeightSource.Explicit));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Constructor_WeightOutOfRange_Throws(double weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StudentGoalWeight(Guid.NewGuid(), CurriculumContextTagConstants.Travel, weight, StudentGoalWeightSource.Explicit));
    }

    [Fact]
    public void SetExplicitWeight_OverwritesValueAndSource()
    {
        var weight = new StudentGoalWeight(Guid.NewGuid(), CurriculumContextTagConstants.Travel, 0.9, StudentGoalWeightSource.Implicit);
        weight.SetExplicitWeight(0.2);

        Assert.Equal(0.2, weight.Weight);
        Assert.Equal(StudentGoalWeightSource.Explicit, weight.Source);
    }

    [Fact]
    public void ApplyImplicitEngagement_NudgesTowardOne()
    {
        var weight = new StudentGoalWeight(Guid.NewGuid(), CurriculumContextTagConstants.Travel, 0, StudentGoalWeightSource.Implicit);
        weight.ApplyImplicitEngagement(0.1);

        Assert.Equal(0.1, weight.Weight, precision: 10);
        Assert.Equal(StudentGoalWeightSource.Implicit, weight.Source);
    }

    [Fact]
    public void ApplyImplicitEngagement_RepeatedCalls_NeverExceedsOne()
    {
        var weight = new StudentGoalWeight(Guid.NewGuid(), CurriculumContextTagConstants.Travel, 0, StudentGoalWeightSource.Implicit);
        for (var i = 0; i < 1000; i++)
            weight.ApplyImplicitEngagement(0.1);

        Assert.True(weight.Weight < 1.0);
        Assert.True(weight.Weight > 0.99);
    }

    [Fact]
    public void ApplyImplicitEngagement_InvalidAlpha_Throws()
    {
        var weight = new StudentGoalWeight(Guid.NewGuid(), CurriculumContextTagConstants.Travel, 0, StudentGoalWeightSource.Implicit);
        Assert.Throws<ArgumentOutOfRangeException>(() => weight.ApplyImplicitEngagement(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => weight.ApplyImplicitEngagement(1.1));
    }
}
