using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Application.Curriculum;

// ── Result types ─────────────────────────────────────────────────────────────

public sealed record CurriculumValidationResult
{
    public IReadOnlyList<CurriculumValidationIssue> Errors { get; init; } = [];
    public IReadOnlyList<CurriculumValidationIssue> Warnings { get; init; } = [];
    public IReadOnlyList<CurriculumCoverageGap> CoverageGaps { get; init; } = [];
    public bool IsValid => Errors.Count == 0;
    public int TotalObjectivesChecked { get; init; }
}

public sealed record CurriculumValidationIssue(
    string ObjectiveKey,
    string Code,
    string Message);

public sealed record CurriculumCoverageGap(
    string CefrLevel,
    string Skill,
    string Message);

// ── Issue codes ───────────────────────────────────────────────────────────────

public static class CurriculumValidationCodes
{
    public const string DuplicateKey       = "DUPLICATE_KEY";
    public const string InvalidCefr        = "INVALID_CEFR";
    public const string InvalidSkill       = "INVALID_SKILL";
    public const string MissingTitle       = "MISSING_TITLE";
    public const string MissingDescription = "MISSING_DESCRIPTION";
    public const string PrereqNotFound     = "PREREQ_NOT_FOUND";
    public const string PrereqCircular     = "PREREQ_CIRCULAR";
    public const string PrereqDisabled     = "PREREQ_DISABLED";
    public const string InvalidContextTag  = "INVALID_CONTEXT_TAG";
    public const string InvalidFocusTag    = "INVALID_FOCUS_TAG";
    public const string CoverageGap        = "COVERAGE_GAP";
    public const string SkillNotRunnable   = "SKILL_NOT_RUNNABLE";
}

// ── Interface ─────────────────────────────────────────────────────────────────

public interface ICurriculumValidationService
{
    /// <summary>Validates all currently active objectives from the database.</summary>
    Task<CurriculumValidationResult> ValidateAllActiveAsync(CancellationToken ct = default);

    /// <summary>Pure/sync validation on a provided set of objectives.</summary>
    CurriculumValidationResult ValidateSet(IReadOnlyList<CurriculumObjective> objectives);
}
