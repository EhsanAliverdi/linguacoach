using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// One ordered step (exercise) within a LearningSession.
///
/// LearningActivityId is null for micro-lesson steps (read-only content that requires
/// no student submission). It is populated when the activity is generated on demand.
/// </summary>
public sealed class SessionExercise : BaseEntity
{
    public Guid LearningSessionId { get; private set; }

    /// <summary>Step number within the session. 1-based display; 0-based storage.</summary>
    public int Order { get; private set; }

    /// <summary>Identifies the exercise pattern (e.g. "listen_and_gap_fill", "micro_lesson_phrases").</summary>
    public string ExercisePatternKey { get; private set; }

    public string PrimarySkill { get; private set; }
    public string? SecondarySkillsJson { get; private set; }

    public int EstimatedMinutes { get; private set; }

    /// <summary>Student-facing instructions for this step.</summary>
    public string Instructions { get; private set; }

    /// <summary>
    /// Null until the LearningActivity is generated for this step.
    /// Always null for micro-lesson steps (no submission required).
    /// </summary>
    public Guid? LearningActivityId { get; private set; }

    public ExerciseStatus Status { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private SessionExercise()
    {
        ExercisePatternKey = string.Empty;
        PrimarySkill = string.Empty;
        Instructions = string.Empty;
    }

    public SessionExercise(
        Guid learningSessionId,
        int order,
        string exercisePatternKey,
        string primarySkill,
        string instructions,
        int estimatedMinutes,
        string? secondarySkillsJson = null)
    {
        if (learningSessionId == Guid.Empty)
            throw new ArgumentException("LearningSessionId must not be empty.", nameof(learningSessionId));
        if (string.IsNullOrWhiteSpace(exercisePatternKey))
            throw new ArgumentException("ExercisePatternKey is required.", nameof(exercisePatternKey));
        if (string.IsNullOrWhiteSpace(primarySkill))
            throw new ArgumentException("PrimarySkill is required.", nameof(primarySkill));
        if (string.IsNullOrWhiteSpace(instructions))
            throw new ArgumentException("Instructions are required.", nameof(instructions));
        if (estimatedMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedMinutes), "EstimatedMinutes must be positive.");
        if (order < 0)
            throw new ArgumentOutOfRangeException(nameof(order), "Order must be non-negative.");

        LearningSessionId = learningSessionId;
        Order = order;
        ExercisePatternKey = exercisePatternKey.Trim();
        PrimarySkill = primarySkill.Trim();
        Instructions = instructions.Trim();
        EstimatedMinutes = estimatedMinutes;
        SecondarySkillsJson = string.IsNullOrWhiteSpace(secondarySkillsJson) ? null : secondarySkillsJson.Trim();
        Status = ExerciseStatus.NotStarted;
    }

    /// <summary>Links the generated LearningActivity to this exercise step.</summary>
    public void AssignActivity(Guid learningActivityId)
    {
        if (learningActivityId == Guid.Empty)
            throw new ArgumentException("LearningActivityId must not be empty.", nameof(learningActivityId));
        LearningActivityId = learningActivityId;
    }

    public void Start()
    {
        if (Status != ExerciseStatus.NotStarted)
            throw new InvalidOperationException($"Cannot start an exercise with status {Status}.");
        Status = ExerciseStatus.InProgress;
    }

    public void Complete()
    {
        if (Status is not (ExerciseStatus.NotStarted or ExerciseStatus.InProgress))
            throw new InvalidOperationException($"Cannot complete an exercise with status {Status}.");
        Status = ExerciseStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Skip()
    {
        if (Status == ExerciseStatus.Completed)
            throw new InvalidOperationException("Cannot skip an already completed exercise.");
        Status = ExerciseStatus.Skipped;
        CompletedAtUtc = DateTime.UtcNow;
    }
}
