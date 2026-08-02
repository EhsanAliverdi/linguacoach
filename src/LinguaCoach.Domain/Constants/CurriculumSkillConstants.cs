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

    /// <summary>2026-07-31 container/leaf redesign — collocation promoted to its own top-level
    /// measurable skill, a peer of Vocabulary/Grammar/Pronunciation (see
    /// docs/reviews/2026-07-31-skill-graph-content-rebuild-implementation.md).</summary>
    public const string Collocation = "collocation";

    public static readonly IReadOnlyList<string> All =
    [
        Writing, Reading, Listening, Speaking,
        Vocabulary, Grammar, Pronunciation, Fluency, Confidence, Collocation
    ];

    public static bool IsValid(string? skill) =>
        skill is not null && All.Contains(skill, StringComparer.OrdinalIgnoreCase);
}
