using LinguaCoach.Domain.Constants;

namespace LinguaCoach.Application.Curriculum;

/// <summary>
/// Maps a resolved learning goal context to curriculum context tags.
/// Pure static function — no dependencies. Always returns at least one tag.
///
/// Rules:
///   - Null input → [general_english] (safe generic fallback)
///   - WorkplaceSpecific=true → adds "workplace"
///   - WorkplaceSpecific=false or unset → adds "general_english" (never defaults to workplace)
///   - PrimaryGoalKey "travel" → adds "travel"
///   - PrimaryGoalKey "job_interviews"/"interviews" → adds "job_interviews"
///   - PrimaryGoalKey "social_conversation" → adds "social_conversation"
///   - PrimaryGoalKey "migration"/"settlement" → adds "migration_settlement"
///   - PrimaryGoalKey "study"/"academic" → adds "study_academic"
///   - FocusAreaKeys containing "pronunciation" → adds "pronunciation"
///   - FocusAreaKeys containing "listening" → adds "listening_confidence"
///   - FocusAreaKeys containing "writing" → adds "writing_confidence"
/// </summary>
public static class CurriculumContextMapper
{
    public static IReadOnlyList<string> MapFromResolvedContext(Learning.ResolvedLearningGoalContext? context)
    {
        if (context is null)
            return [CurriculumContextTagConstants.GeneralEnglish];

        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Workplace flag — only set when learner explicitly chose workplace context.
        if (context.WorkplaceSpecific)
            tags.Add(CurriculumContextTagConstants.Workplace);
        else
            tags.Add(CurriculumContextTagConstants.GeneralEnglish);

        // Map primary goal key to curriculum context tag.
        var goalKey = context.PrimaryGoalKey?.ToLowerInvariant() ?? string.Empty;
        if (goalKey.Contains("travel"))
            tags.Add(CurriculumContextTagConstants.Travel);
        if (goalKey.Contains("interview"))
            tags.Add(CurriculumContextTagConstants.JobInterviews);
        if (goalKey.Contains("social") || goalKey.Contains("conversation"))
            tags.Add(CurriculumContextTagConstants.SocialConversation);
        if (goalKey.Contains("migrat") || goalKey.Contains("settlement"))
            tags.Add(CurriculumContextTagConstants.MigrationSettlement);
        if (goalKey.Contains("study") || goalKey.Contains("academ"))
            tags.Add(CurriculumContextTagConstants.StudyAcademic);
        if (goalKey.Contains("exam"))
            tags.Add(CurriculumContextTagConstants.ExamInspired);
        if (goalKey.Contains("day") || goalKey.Contains("daily"))
            tags.Add(CurriculumContextTagConstants.DayToDay);

        // Map focus area keys to additional curriculum tags.
        var focusKeys = context.FocusAreaKeys?.ToLowerInvariant() ?? string.Empty;
        if (focusKeys.Contains("pronunciation"))
            tags.Add(CurriculumContextTagConstants.Pronunciation);
        if (focusKeys.Contains("listen"))
            tags.Add(CurriculumContextTagConstants.ListeningConfidence);
        if (focusKeys.Contains("writ"))
            tags.Add(CurriculumContextTagConstants.WritingConfidence);

        // Always include day_to_day for non-workplace contexts as a broad safe fallback.
        if (!context.WorkplaceSpecific && tags.Count == 1)
            tags.Add(CurriculumContextTagConstants.DayToDay);

        return tags.ToList();
    }
}
