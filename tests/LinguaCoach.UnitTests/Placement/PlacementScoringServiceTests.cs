using System.Text.Json;
using LinguaCoach.Infrastructure.Placement;

namespace LinguaCoach.UnitTests.Placement;

/// <summary>
/// Unit tests for PlacementScoringService.ScoreSubmission (Form.io-native migration).
/// </summary>
public sealed class PlacementScoringServiceTests
{
    private readonly PlacementScoringService _sut = new();

    private static IReadOnlyDictionary<string, JsonElement> Submission(object data)
    {
        var json = JsonSerializer.Serialize(data);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
    }

    private static string SingleComponentRules(string kind, string correctAnswer) =>
        JsonSerializer.Serialize(new
        {
            components = new Dictionary<string, object>
            {
                ["answer"] = new { kind, correctAnswer, points = 1.0 }
            }
        });

    [Fact]
    public void ScoreSubmission_ExactMatch_ReturnsCorrect()
    {
        var rules = SingleComponentRules("single_choice", "A");
        var result = _sut.ScoreSubmission(rules, Submission(new { answer = "A" }));

        Assert.True(result.IsCorrect);
        Assert.Equal(1.0, result.Score);
    }

    [Fact]
    public void ScoreSubmission_CaseInsensitiveMatch_ReturnsCorrect()
    {
        var rules = SingleComponentRules("text_normalized", "Best");
        var result = _sut.ScoreSubmission(rules, Submission(new { answer = "best" }));

        Assert.True(result.IsCorrect);
    }

    [Fact]
    public void ScoreSubmission_WhitespaceNormalized_ReturnsCorrect()
    {
        var rules = SingleComponentRules("single_choice", "B");
        var result = _sut.ScoreSubmission(rules, Submission(new { answer = "  B  " }));

        Assert.True(result.IsCorrect);
    }

    [Fact]
    public void ScoreSubmission_WrongAnswer_ReturnsIncorrect()
    {
        var rules = SingleComponentRules("single_choice", "A");
        var result = _sut.ScoreSubmission(rules, Submission(new { answer = "C" }));

        Assert.False(result.IsCorrect);
        Assert.Equal(0.0, result.Score);
    }

    [Fact]
    public void ScoreSubmission_EmptyResponse_ReturnsIncorrect()
    {
        var rules = SingleComponentRules("single_choice", "A");
        var result = _sut.ScoreSubmission(rules, Submission(new { answer = "" }));

        Assert.False(result.IsCorrect);
    }

    [Fact]
    public void ScoreSubmission_MissingComponentInSubmission_ReturnsIncorrect()
    {
        var rules = SingleComponentRules("single_choice", "A");
        var result = _sut.ScoreSubmission(rules, Submission(new { }));

        Assert.False(result.IsCorrect);
    }

    [Fact]
    public void ScoreSubmission_NullScoringRules_ReturnsIncorrect()
    {
        var result = _sut.ScoreSubmission(null, Submission(new { answer = "A" }));

        Assert.False(result.IsCorrect);
        Assert.Contains("No scoring rules", result.EvaluationNotes);
    }

    [Fact]
    public void ScoreSubmission_GapFill_CorrectAnswer_ReturnsCorrect()
    {
        var rules = SingleComponentRules("text_normalized", "are");
        var result = _sut.ScoreSubmission(rules, Submission(new { answer = "are" }));

        Assert.True(result.IsCorrect);
    }

    [Fact]
    public void ScoreSubmission_MultiWord_ExactMatch_ReturnsCorrect()
    {
        var rules = SingleComponentRules("text_normalized", "build on");
        var result = _sut.ScoreSubmission(rules, Submission(new { answer = "build on" }));

        Assert.True(result.IsCorrect);
    }

    // ── Multi-component group scoring (proves every component scored independently) ──────

    [Fact]
    public void ScoreSubmission_MultiComponentGroup_ScoresEachComponentIndependently()
    {
        var rules = JsonSerializer.Serialize(new
        {
            components = new Dictionary<string, object>
            {
                ["q1"] = new { kind = "single_choice", correctAnswer = "A", points = 1.0 },
                ["q2"] = new { kind = "text_normalized", correctAnswer = "cat", points = 1.0 },
            }
        });

        // q1 correct, q2 wrong — must not just look at the first component.
        var partial = _sut.ScoreSubmission(rules, Submission(new { q1 = "A", q2 = "dog" }));
        Assert.False(partial.IsCorrect);
        Assert.Equal(0.5, partial.Score);
        Assert.Equal(2, partial.Components.Count);
        Assert.True(partial.Components.Single(c => c.ComponentKey == "q1").IsCorrect);
        Assert.False(partial.Components.Single(c => c.ComponentKey == "q2").IsCorrect);

        // Both correct.
        var full = _sut.ScoreSubmission(rules, Submission(new { q1 = "A", q2 = "cat" }));
        Assert.True(full.IsCorrect);
        Assert.Equal(1.0, full.Score);

        // Both wrong.
        var none = _sut.ScoreSubmission(rules, Submission(new { q1 = "B", q2 = "dog" }));
        Assert.False(none.IsCorrect);
        Assert.Equal(0.0, none.Score);
    }

    [Fact]
    public void ScoreSubmission_ManualEvaluationComponent_ExcludedFromScoring()
    {
        var rules = JsonSerializer.Serialize(new
        {
            components = new Dictionary<string, object>
            {
                ["q1"] = new { kind = "single_choice", correctAnswer = "A", points = 1.0 },
                ["essay"] = new { kind = "text_exact", points = 1.0, requiresManualOrAiEvaluation = true },
            }
        });

        var result = _sut.ScoreSubmission(rules, Submission(new { q1 = "A", essay = "some long response" }));

        // Only q1 counts toward scoring — the manual component is excluded entirely.
        Assert.True(result.IsCorrect);
        Assert.Equal(1.0, result.Score);
    }
}
