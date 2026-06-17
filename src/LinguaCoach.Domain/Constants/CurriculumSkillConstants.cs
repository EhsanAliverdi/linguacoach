namespace LinguaCoach.Domain.Constants;

/// <summary>
/// Canonical skill identifiers used by CurriculumObjective.
/// Aligned with StudentProfile FocusAreas and ExercisePatternDefinition.PrimarySkill.
/// </summary>
public static class CurriculumSkillConstants
{
    public const string Writing = "writing";
    public const string Reading = "reading";
    public const string Listening = "listening";
    public const string Speaking = "speaking";
    public const string Vocabulary = "vocabulary";
    public const string Grammar = "grammar";
    public const string Pronunciation = "pronunciation";
    public const string Fluency = "fluency";
    public const string Confidence = "confidence";

    public static readonly IReadOnlyList<string> All =
    [
        Writing, Reading, Listening, Speaking,
        Vocabulary, Grammar, Pronunciation, Fluency, Confidence
    ];

    public static bool IsValid(string? skill) =>
        skill is not null && All.Contains(skill, StringComparer.OrdinalIgnoreCase);
}
