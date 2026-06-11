using FluentAssertions;
using LinguaCoach.Infrastructure.Jobs;

namespace LinguaCoach.UnitTests.Jobs;

public sealed class LessonBatchPlanValidationTests
{
    [Fact]
    public void ParseAndValidatePlans_ValidArray_ReturnsPlans()
    {
        var json = """
        [
          {
            "title": "Following up on a delay",
            "topic": "project delay follow-up",
            "sessionGoal": "Write a polite follow-up about a delay",
            "focusSkill": "writing",
            "durationMinutes": 15,
            "exercises": [
              { "exercisePatternKey": "email_reply", "primarySkill": "writing", "instructions": "Reply to the email.", "estimatedMinutes": 8 }
            ]
          }
        ]
        """;

        var plans = LessonBatchGenerationJob.ParseAndValidatePlans(json, 1);

        plans.Should().HaveCount(1);
        plans[0].Title.Should().Be("Following up on a delay");
        plans[0].Exercises.Should().ContainSingle();
        plans[0].Exercises[0].ExercisePatternKey.Should().Be("email_reply");
    }

    [Fact]
    public void ParseAndValidatePlans_StripsCodeFences()
    {
        var json = "```json\n[{\"title\":\"T\",\"exercises\":[{\"instructions\":\"x\"}]}]\n```";
        var plans = LessonBatchGenerationJob.ParseAndValidatePlans(json, 1);
        plans.Should().ContainSingle();
        plans[0].Exercises.Should().ContainSingle();
    }

    [Fact]
    public void ParseAndValidatePlans_NotAnArray_Throws()
    {
        var act = () => LessonBatchGenerationJob.ParseAndValidatePlans("{\"title\":\"x\"}", 1);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseAndValidatePlans_EmptyArray_Throws()
    {
        var act = () => LessonBatchGenerationJob.ParseAndValidatePlans("[]", 1);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseAndValidatePlans_SessionWithNoExercises_Throws()
    {
        var act = () => LessonBatchGenerationJob.ParseAndValidatePlans(
            "[{\"title\":\"T\",\"exercises\":[]}]", 1);
        act.Should().Throw<InvalidOperationException>();
    }
}
