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
}
