using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Structured record of a single learning event for a student.
/// Written after each activity attempt from Today lessons or Practice Gym.
/// Append-only — never mutated after creation.
/// </summary>
public sealed class StudentLearningEvent : BaseEntity
{
    public Guid StudentProfileId { get; private set; }

    public LearningEventSource Source { get; private set; }

    // References — nullable because not all sources have all references
    public Guid? ActivityId { get; private set; }
    public Guid? SessionId { get; private set; }
    public Guid? SessionExerciseId { get; private set; }
    public Guid? ActivityAttemptId { get; private set; }

    // Exercise identity
    public string? ExerciseType { get; private set; }
    public string? PatternKey { get; private set; }

    // Skills
    public string? PrimarySkill { get; private set; }
    public string? SecondarySkillsJson { get; private set; }

    /// <summary>Optional finer-grained classification beneath PrimarySkill. Null means unclassified.
    /// When PrimarySkill is known, must be a CurriculumSubskillConstants value belonging to it.</summary>
    public string? Subskill { get; private set; }

    /// <summary>CurriculumObjective.Key this event provides evidence for, when known. Null for
    /// events not routed through curriculum-objective selection (mastery grouping falls back to
    /// PatternKey/PrimarySkill in that case — see LearningEventExtensions.CurriculumObjectiveKey()
    /// in StudentMasteryEvaluationService.cs). See
    /// docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md, Phase 8.</summary>
    public string? CurriculumObjectiveKey { get; private set; }

    // Context — no workplace default; null means unknown/not collected yet
    public string? LearningGoalContext { get; private set; }
    public string? CefrLevelAtEvent { get; private set; }

    // What was covered
    public string? ConceptsTaughtJson { get; private set; }
    public string? ConceptsPractisedJson { get; private set; }
    public string? MistakeTagsJson { get; private set; }

    // Score
    public double? Score { get; private set; }
    public double? NormalizedScore { get; private set; }

    public LearningEventOutcome Outcome { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    // Format-specific overflow — bounded JSON blob, never the primary query surface
    public string? MetadataJson { get; private set; }

    private StudentLearningEvent() { }

    public StudentLearningEvent(
        Guid studentProfileId,
        LearningEventSource source,
        LearningEventOutcome outcome,
        Guid? activityId = null,
        Guid? sessionId = null,
        Guid? sessionExerciseId = null,
        Guid? activityAttemptId = null,
        string? exerciseType = null,
        string? patternKey = null,
        string? primarySkill = null,
        string? secondarySkillsJson = null,
        string? learningGoalContext = null,
        string? cefrLevelAtEvent = null,
        string? conceptsTaughtJson = null,
        string? conceptsPractisedJson = null,
        string? mistakeTagsJson = null,
        double? score = null,
        double? normalizedScore = null,
        string? metadataJson = null,
        string? subskill = null,
        string? curriculumObjectiveKey = null)
    {
        if (studentProfileId == Guid.Empty)
            throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (score is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be 0-100.");
        if (normalizedScore is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(normalizedScore), "NormalizedScore must be 0-1.");
        var subskillValid = primarySkill is not null
            ? CurriculumSubskillConstants.IsValidForSkill(primarySkill, subskill)
            : subskill is null || CurriculumSubskillConstants.IsValid(subskill);
        if (!subskillValid)
            throw new ArgumentException($"Subskill '{subskill}' does not belong to skill '{primarySkill}'.", nameof(subskill));

        StudentProfileId = studentProfileId;
        Source = source;
        Outcome = outcome;
        ActivityId = activityId;
        SessionId = sessionId;
        SessionExerciseId = sessionExerciseId;
        ActivityAttemptId = activityAttemptId;
        ExerciseType = exerciseType?.Trim();
        PatternKey = patternKey?.Trim();
        PrimarySkill = primarySkill?.Trim();
        SecondarySkillsJson = secondarySkillsJson;
        Subskill = subskill?.Trim().ToLowerInvariant();
        CurriculumObjectiveKey = curriculumObjectiveKey?.Trim();
        LearningGoalContext = learningGoalContext?.Trim();
        CefrLevelAtEvent = cefrLevelAtEvent?.Trim();
        ConceptsTaughtJson = conceptsTaughtJson;
        ConceptsPractisedJson = conceptsPractisedJson;
        MistakeTagsJson = mistakeTagsJson;
        Score = score;
        NormalizedScore = normalizedScore;
        MetadataJson = metadataJson;
        OccurredAtUtc = DateTime.UtcNow;
    }
}
