namespace LinguaCoach.Domain.Constants;

/// <summary>
/// Canonical learner goal/context tags used by CurriculumObjective.
/// These map from ResolvedLearningGoalContext via CurriculumContextMapper.
/// workplace is one tag among many — not the default for all objectives.
/// </summary>
public static class CurriculumContextTagConstants
{
    public const string GeneralEnglish = "general_english";
    public const string DayToDay = "day_to_day";
    public const string Travel = "travel";
    public const string StudyAcademic = "study_academic";
    public const string MigrationSettlement = "migration_settlement";
    public const string JobInterviews = "job_interviews";
    public const string SocialConversation = "social_conversation";
    public const string Workplace = "workplace";
    public const string Pronunciation = "pronunciation";
    public const string ListeningConfidence = "listening_confidence";
    public const string WritingConfidence = "writing_confidence";
    public const string ExamInspired = "exam_inspired";
    public const string Custom = "custom";

    public static readonly IReadOnlyList<string> All =
    [
        GeneralEnglish, DayToDay, Travel, StudyAcademic,
        MigrationSettlement, JobInterviews, SocialConversation,
        Workplace, Pronunciation, ListeningConfidence,
        WritingConfidence, ExamInspired, Custom
    ];

    public static bool IsValid(string? tag) =>
        tag is not null && All.Contains(tag, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adaptive Curriculum Sprint 2 — curated subset of <see cref="All"/> that represents a genuine
    /// student life-goal/motivation ("why is this student learning English"), as opposed to a
    /// skill/format descriptor or admin catch-all. This is the taxonomy Sprint 3's per-student goal
    /// vector will draw from — see docs/architecture/adaptive-curriculum-skill-graph.md. Deliberately
    /// a curated subset of the existing tag list, not a new taxonomy: Pronunciation/
    /// ListeningConfidence/WritingConfidence describe a skill focus, not a motivation;
    /// ExamInspired describes content format; Custom is an admin escape hatch — none of these answer
    /// "why," so none belong in the goal vector.
    /// </summary>
    public static readonly IReadOnlyList<string> GoalTags =
    [
        GeneralEnglish, DayToDay, Travel, StudyAcademic,
        MigrationSettlement, JobInterviews, SocialConversation, Workplace
    ];

    public static bool IsGoalTag(string? tag) =>
        tag is not null && GoalTags.Contains(tag, StringComparer.OrdinalIgnoreCase);
}
