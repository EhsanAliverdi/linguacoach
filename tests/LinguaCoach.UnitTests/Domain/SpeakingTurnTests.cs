using FluentAssertions;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class SpeakingTurnTests
{
    private static SpeakingTurn NewTurn() =>
        new(Guid.NewGuid(), 1, "What would you say to request a document status update?");

    [Fact]
    public void Constructor_WithEmptySessionId_Throws()
    {
        var act = () => new SpeakingTurn(Guid.Empty, 1, "question");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithZeroTurnNumber_Throws()
    {
        var act = () => new SpeakingTurn(Guid.NewGuid(), 0, "question");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithBlankQuestion_Throws()
    {
        var act = () => new SpeakingTurn(Guid.NewGuid(), 1, "  ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NewTurn_HasEmptyDefaultJsonFields()
    {
        var turn = NewTurn();
        turn.FeedbackJson.Should().Be("{}");
        turn.MistakesJson.Should().Be("[]");
        turn.UserTranscript.Should().BeNull();
        turn.UserAudioUrl.Should().BeNull();
    }

    [Fact]
    public void RecordResponse_SetsAllFields()
    {
        var turn = NewTurn();
        turn.RecordResponse(
            userTranscript: "Could you please update me on the status?",
            aiReply: "Great use of polite register. Let's try the next scenario.",
            feedbackJson: "{\"overallComment\":\"Good\"}",
            mistakesJson: "[]",
            pronunciationScore: 80,
            grammarScore: 75,
            vocabularyScore: 90,
            fluencyScore: 70,
            turnSummary: "Used polite request form correctly.");

        turn.UserTranscript.Should().Be("Could you please update me on the status?");
        turn.AiReply.Should().Contain("polite register");
        turn.GrammarScore.Should().Be(75);
        turn.TurnSummary.Should().Be("Used polite request form correctly.");
        turn.FeedbackJson.Should().Contain("Good");
    }

    [Fact]
    public void RecordResponse_WithBlankAiReply_Throws()
    {
        var turn = NewTurn();
        var act = () => turn.RecordResponse("transcript", "  ", "{}", "[]", null, null, null, null, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordResponse_NullTranscript_IsAllowed()
    {
        var turn = NewTurn();
        var act = () => turn.RecordResponse(null, "AI reply", "{}", "[]", null, null, null, null, null);
        act.Should().NotThrow();
        turn.UserTranscript.Should().BeNull();
    }
}
