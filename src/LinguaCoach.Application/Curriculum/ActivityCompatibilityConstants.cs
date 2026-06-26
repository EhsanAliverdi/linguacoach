using LinguaCoach.Domain.Constants;

namespace LinguaCoach.Application.Curriculum;

/// <summary>
/// Maps primary curriculum skills to compatible activity types and exercise patterns.
/// Used by validation to detect objectives with no runnable exercise format.
/// </summary>
public static class ActivityCompatibilityConstants
{
    /// <summary>Skills with at least one runnable activity type in the current platform.</summary>
    public static readonly IReadOnlySet<string> RunnableSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CurriculumSkillConstants.Writing,
        CurriculumSkillConstants.Listening,
        CurriculumSkillConstants.Speaking,
        CurriculumSkillConstants.Vocabulary,
        CurriculumSkillConstants.Reading,
    };

    /// <summary>Skills with planned but not yet implemented exercise formats.</summary>
    public static readonly IReadOnlySet<string> PlannedSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CurriculumSkillConstants.Grammar,
        CurriculumSkillConstants.Pronunciation,
        CurriculumSkillConstants.Fluency,
        CurriculumSkillConstants.Confidence,
    };

    public static bool IsRunnable(string skill) => RunnableSkills.Contains(skill);
    public static bool IsPlanned(string skill) => PlannedSkills.Contains(skill);
    public static bool IsSupported(string skill) => IsRunnable(skill) || IsPlanned(skill);
}
