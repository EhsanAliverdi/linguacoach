namespace LinguaCoach.Application.Placement;

/// <summary>
/// Compact summary of a single completed section, passed to the evaluator.
/// Contains deterministic per-section scores and (for productive sections) the
/// student's text — but never raw correct answers.
/// </summary>
public sealed record PlacementSectionSummary(
    string SectionKey,
    bool Scored,
    /// <summary>0-100 deterministic score where applicable (MCQ/reading/listening), else null.</summary>
    double? Score,
    int AnsweredCount,
    int CorrectCount,
    /// <summary>Free-text response for productive sections (writing/speaking), truncated.</summary>
    string? ResponseText,
    /// <summary>Self-check ratings/notes (self_check only).</summary>
    IReadOnlyList<string>? Notes);

/// <summary>Input passed to the placement evaluator.</summary>
public sealed record PlacementEvaluationInput(
    Guid StudentProfileId,
    string CareerContext,
    string SourceLanguageName,
    string TargetLanguageName,
    string? SelfReportedLevel,
    string ProfessionalExperienceLevel,
    string RoleFamiliarity,
    string DomainComplexity,
    IReadOnlyList<PlacementSectionSummary> Sections);

/// <summary>Structured placement result produced by an evaluator.</summary>
public sealed record PlacementEvaluationResult(
    string EstimatedOverallLevel,
    IReadOnlyDictionary<string, string> SkillLevels,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    string? RecommendedStartingCourse,
    int RecommendedSessionDuration,
    string? PlacementNotes);

/// <summary>
/// Evaluates a completed (or partially completed) placement assessment and produces
/// a holistic result. Implementations: FakePlacementEvaluator (deterministic, no AI),
/// AiPlacementEvaluator (calls the placement_assessment_evaluate prompt).
/// </summary>
public interface IPlacementEvaluator
{
    Task<PlacementEvaluationResult> EvaluateAsync(PlacementEvaluationInput input, CancellationToken ct = default);
}
