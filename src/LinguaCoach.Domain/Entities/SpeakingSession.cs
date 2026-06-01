using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Exceptions;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A single push-to-talk speaking practice session for a student.
/// One session = one scenario, bounded by MaxTurns.
/// </summary>
public sealed class SpeakingSession : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public Guid ScenarioId { get; private set; }
    public SpeakingSessionStatus Status { get; private set; }

    // Snapshot of student context at session creation — denormalised so
    // session history remains accurate even if the student's profile changes.
    public string CefrLevel { get; private set; }
    public string CareerContext { get; private set; }

    public int MaxTurns { get; private set; }
    public int CurrentTurn { get; private set; }

    public double? OverallScore { get; private set; }

    // Compact summary written by the backend after session completion (max 200 chars).
    public string? SessionSummary { get; private set; }

    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private SpeakingSession() { CefrLevel = string.Empty; CareerContext = string.Empty; }

    public SpeakingSession(
        Guid studentProfileId,
        Guid scenarioId,
        string cefrLevel,
        string careerContext,
        int maxTurns)
    {
        if (studentProfileId == Guid.Empty) throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (scenarioId == Guid.Empty) throw new ArgumentException("ScenarioId must not be empty.", nameof(scenarioId));
        if (string.IsNullOrWhiteSpace(cefrLevel)) throw new ArgumentException("CefrLevel is required.", nameof(cefrLevel));
        if (string.IsNullOrWhiteSpace(careerContext)) throw new ArgumentException("CareerContext is required.", nameof(careerContext));
        if (maxTurns < 1) throw new ArgumentOutOfRangeException(nameof(maxTurns), "MaxTurns must be at least 1.");

        StudentProfileId = studentProfileId;
        ScenarioId = scenarioId;
        CefrLevel = cefrLevel.Trim();
        CareerContext = careerContext.Trim();
        MaxTurns = maxTurns;
        Status = SpeakingSessionStatus.NotStarted;
    }

    public void Start()
    {
        if (Status != SpeakingSessionStatus.NotStarted)
            throw new DomainException("Session can only be started from NotStarted state.");
        Status = SpeakingSessionStatus.InProgress;
        StartedAt = DateTime.UtcNow;
    }

    public void AdvanceTurn()
    {
        if (Status != SpeakingSessionStatus.InProgress)
            throw new DomainException("Cannot advance turn: session is not in progress.");
        if (CurrentTurn >= MaxTurns)
            throw new DomainException("Session has already reached MaxTurns.");
        CurrentTurn++;
    }

    public void Complete(double overallScore, string? sessionSummary = null)
    {
        if (Status != SpeakingSessionStatus.InProgress)
            throw new DomainException("Session can only be completed from InProgress state.");
        if (overallScore < 0 || overallScore > 100)
            throw new ArgumentOutOfRangeException(nameof(overallScore), "Score must be between 0 and 100.");

        Status = SpeakingSessionStatus.Completed;
        OverallScore = overallScore;
        SessionSummary = sessionSummary?.Trim();
        CompletedAt = DateTime.UtcNow;
    }

    public void Abandon()
    {
        if (Status == SpeakingSessionStatus.Completed || Status == SpeakingSessionStatus.Abandoned)
            throw new DomainException("Session is already finalised.");
        Status = SpeakingSessionStatus.Abandoned;
        CompletedAt = DateTime.UtcNow;
    }
}
