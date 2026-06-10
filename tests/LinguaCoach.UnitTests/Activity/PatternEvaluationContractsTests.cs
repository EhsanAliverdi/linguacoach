using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.Activity;

namespace LinguaCoach.UnitTests.Activity;

public sealed class PatternEvaluationContractsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Create_CalculatesPercentageFromScoreAndMaxScore()
    {
        var result = PatternEvaluationResult.Create(
            score: 3,
            maxScore: 4,
            passed: true,
            completed: true);

        result.Percentage.Should().Be(75);
        result.Score.Should().Be(3);
        result.MaxScore.Should().Be(4);
    }

    [Fact]
    public void Create_WithZeroMaxScore_ReturnsZeroPercentage()
    {
        var result = PatternEvaluationResult.Create(
            score: 0,
            maxScore: 0,
            passed: true,
            completed: true);

        result.Percentage.Should().Be(0);
    }

    [Fact]
    public void CalculatePercentage_RoundsToTwoDecimals()
    {
        PatternEvaluationResult.CalculatePercentage(2, 3).Should().Be(66.67);
    }

    [Fact]
    public void Json_RoundTripsPatternEvaluationResult()
    {
        var result = PatternEvaluationResult.Create(
            score: 1,
            maxScore: 2,
            passed: false,
            completed: true,
            itemResults:
            [
                new PatternEvaluationItemResult(
                    ItemKey: "gap_1",
                    StudentAnswer: "send",
                    CorrectAnswer: "sent",
                    AcceptedAnswers: ["sent", "submitted"],
                    IsCorrect: false,
                    Score: 0,
                    MaxScore: 1,
                    Feedback: "Use past tense here.")
            ],
            coachSummary: "Review past tense for completed actions.",
            corrections:
            [
                new PatternEvaluationCorrection(
                    Category: "grammar",
                    Original: "I send it yesterday",
                    Suggestion: "I sent it yesterday",
                    Explanation: "Use past tense for yesterday.")
            ],
            suggestedImprovedAnswer: "I sent it yesterday.",
            skillImpacts:
            [
                new PatternEvaluationSkillImpact(
                    SkillKey: "past_tense",
                    Label: "Past tense",
                    Delta: -0.05,
                    Evidence: "Missed past-tense verb in a completed action.")
            ],
            memorySignals:
            [
                new PatternEvaluationMemorySignal(
                    Type: "recurring_mistake",
                    Key: "past_tense_completed_actions",
                    Summary: "Student may need past-tense reinforcement.",
                    Confidence: 0.8)
            ]);

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var loaded = JsonSerializer.Deserialize<PatternEvaluationResult>(json, JsonOptions);

        loaded.Should().NotBeNull();
        loaded!.Percentage.Should().Be(50);
        loaded.ItemResults.Should().ContainSingle();
        loaded.ItemResults[0].AcceptedAnswers.Should().Contain("submitted");
        loaded.Corrections.Should().ContainSingle(c => c.Category == "grammar");
        loaded.SkillImpacts.Should().ContainSingle(s => s.SkillKey == "past_tense");
        loaded.MemorySignals.Should().ContainSingle(m => m.Key == "past_tense_completed_actions");
    }
}
