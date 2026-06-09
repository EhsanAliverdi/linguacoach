using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// One complete guided lesson within a LearningModule.
/// Represents the equivalent of one English class — ordered, time-bounded,
/// and sequenced using a teaching flow.
///
/// LearningPath → LearningModule → LearningSession → SessionExercise → LearningActivity
/// </summary>
public sealed class LearningSession : BaseEntity
{
    public Guid LearningModuleId { get; private set; }

    public string Title { get; private set; }
    public string Topic { get; private set; }
    public string SessionGoal { get; private set; }

    /// <summary>10 / 15 / 20 / 30 minutes. Controlled by student's preference set during onboarding.</summary>
    public int DurationMinutes { get; private set; }

    public string FocusSkill { get; private set; }
    public string? SecondarySkillsJson { get; private set; }

    /// <summary>Display order within the parent LearningModule.</summary>
    public int Order { get; private set; }

    public SessionStatus Status { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>
    /// Compact snapshot of the student's learning memory state at the time this
    /// session was generated. Used for auditability and replay.
    /// </summary>
    public string? GeneratedFromMemorySnapshotJson { get; private set; }

    public IReadOnlyList<SessionExercise> Exercises => _exercises.AsReadOnly();
    private readonly List<SessionExercise> _exercises = [];

    private LearningSession()
    {
        Title = string.Empty;
        Topic = string.Empty;
        SessionGoal = string.Empty;
        FocusSkill = string.Empty;
    }

    public LearningSession(
        Guid learningModuleId,
        string title,
        string topic,
        string sessionGoal,
        int durationMinutes,
        string focusSkill,
        int order,
        string? secondarySkillsJson = null,
        string? generatedFromMemorySnapshotJson = null)
    {
        if (learningModuleId == Guid.Empty)
            throw new ArgumentException("LearningModuleId must not be empty.", nameof(learningModuleId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Topic is required.", nameof(topic));
        if (string.IsNullOrWhiteSpace(sessionGoal))
            throw new ArgumentException("SessionGoal is required.", nameof(sessionGoal));
        if (durationMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "DurationMinutes must be positive.");
        if (string.IsNullOrWhiteSpace(focusSkill))
            throw new ArgumentException("FocusSkill is required.", nameof(focusSkill));
        if (order < 0)
            throw new ArgumentOutOfRangeException(nameof(order), "Order must be non-negative.");

        LearningModuleId = learningModuleId;
        Title = title.Trim();
        Topic = topic.Trim();
        SessionGoal = sessionGoal.Trim();
        DurationMinutes = durationMinutes;
        FocusSkill = focusSkill.Trim();
        SecondarySkillsJson = string.IsNullOrWhiteSpace(secondarySkillsJson) ? null : secondarySkillsJson.Trim();
        Order = order;
        GeneratedFromMemorySnapshotJson = string.IsNullOrWhiteSpace(generatedFromMemorySnapshotJson)
            ? null
            : generatedFromMemorySnapshotJson.Trim();
        Status = SessionStatus.NotStarted;
    }

    public void Start()
    {
        if (Status != SessionStatus.NotStarted)
            throw new InvalidOperationException($"Cannot start a session with status {Status}.");
        Status = SessionStatus.InProgress;
        StartedAtUtc = DateTime.UtcNow;
    }

    public void Complete()
    {
        if (Status != SessionStatus.InProgress)
            throw new InvalidOperationException($"Cannot complete a session with status {Status}.");
        Status = SessionStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
    }
}
