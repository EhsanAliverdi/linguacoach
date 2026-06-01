using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Exceptions;

namespace LinguaCoach.UnitTests.Domain;

public sealed class SpeakingSessionTests
{
    private static SpeakingSession NewSession(int maxTurns = 6) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "B1", "Document Controller", maxTurns);

    // ── Constructor guards ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithEmptyStudentId_Throws()
    {
        var act = () => new SpeakingSession(Guid.Empty, Guid.NewGuid(), "B1", "DC", 6);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithZeroMaxTurns_Throws()
    {
        var act = () => new SpeakingSession(Guid.NewGuid(), Guid.NewGuid(), "B1", "DC", 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithBlankCefrLevel_Throws()
    {
        var act = () => new SpeakingSession(Guid.NewGuid(), Guid.NewGuid(), "  ", "DC", 6);
        act.Should().Throw<ArgumentException>();
    }

    // ── State machine ─────────────────────────────────────────────────────────

    [Fact]
    public void NewSession_IsNotStarted()
    {
        var session = NewSession();
        session.Status.Should().Be(SpeakingSessionStatus.NotStarted);
        session.CurrentTurn.Should().Be(0);
    }

    [Fact]
    public void Start_TransitionsToInProgress()
    {
        var session = NewSession();
        session.Start();
        session.Status.Should().Be(SpeakingSessionStatus.InProgress);
        session.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public void Start_WhenAlreadyInProgress_Throws()
    {
        var session = NewSession();
        session.Start();
        var act = () => session.Start();
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AdvanceTurn_IncrementsCurrentTurn()
    {
        var session = NewSession(maxTurns: 3);
        session.Start();
        session.AdvanceTurn();
        session.CurrentTurn.Should().Be(1);
    }

    [Fact]
    public void AdvanceTurn_BeyondMaxTurns_Throws()
    {
        var session = NewSession(maxTurns: 1);
        session.Start();
        session.AdvanceTurn();
        var act = () => session.AdvanceTurn();
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AdvanceTurn_WhenNotStarted_Throws()
    {
        var session = NewSession();
        var act = () => session.AdvanceTurn();
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Complete_SetsScoreAndStatus()
    {
        var session = NewSession();
        session.Start();
        session.Complete(72.5, "Good use of formal register.");
        session.Status.Should().Be(SpeakingSessionStatus.Completed);
        session.OverallScore.Should().Be(72.5);
        session.SessionSummary.Should().Be("Good use of formal register.");
        session.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_WithScoreAbove100_Throws()
    {
        var session = NewSession();
        session.Start();
        var act = () => session.Complete(101);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Complete_WhenNotInProgress_Throws()
    {
        var session = NewSession();
        var act = () => session.Complete(80);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Abandon_SetsAbandonedStatus()
    {
        var session = NewSession();
        session.Start();
        session.Abandon();
        session.Status.Should().Be(SpeakingSessionStatus.Abandoned);
    }

    [Fact]
    public void Abandon_WhenCompleted_Throws()
    {
        var session = NewSession();
        session.Start();
        session.Complete(50);
        var act = () => session.Abandon();
        act.Should().Throw<DomainException>();
    }
}
