using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Curriculum;

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>Full admin view of a curriculum objective (includes inactive).</summary>
public sealed record AdminCurriculumObjectiveDto(
    Guid Id,
    string Key,
    string Title,
    string Description,
    string CefrLevel,
    string PrimarySkill,
    string SecondarySkillsJson,
    string ContextTagsJson,
    string FocusTagsJson,
    string PrerequisiteKeysJson,
    int RecommendedOrder,
    int DifficultyBand,
    bool IsActive,
    bool IsReviewable,
    bool IsExamInspired,
    string? TeachingNotes,
    string? ExamplePrompts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AdminUpdatedAt)
{
    public static AdminCurriculumObjectiveDto From(CurriculumObjective o) =>
        new(o.Id, o.Key, o.Title, o.Description, o.CefrLevel, o.PrimarySkill,
            o.SecondarySkillsJson, o.ContextTagsJson, o.FocusTagsJson,
            o.PrerequisiteKeysJson, o.RecommendedOrder, o.DifficultyBand,
            o.IsActive, o.IsReviewable, o.IsExamInspired,
            o.TeachingNotes, o.ExamplePrompts,
            o.CreatedAt, o.AdminUpdatedAt);
}

/// <summary>Request body for create or update.</summary>
public sealed record AdminCurriculumObjectiveUpsertRequest(
    string Key,
    string Title,
    string Description,
    string CefrLevel,
    string PrimarySkill,
    IReadOnlyList<string> SecondarySkills,
    IReadOnlyList<string> ContextTags,
    IReadOnlyList<string> FocusTags,
    IReadOnlyList<string> PrerequisiteObjectiveKeys,
    int RecommendedOrder,
    int DifficultyBand,
    bool IsActive,
    bool IsReviewable,
    bool IsExamInspired,
    string? TeachingNotes,
    string? ExamplePrompts);

/// <summary>Taxonomy response: known CEFR levels, skills, context tags.</summary>
public sealed record CurriculumTaxonomyDto(
    IReadOnlyList<string> CefrLevels,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> ContextTags)
{
    public static CurriculumTaxonomyDto Build() => new(
        CefrLevels: CefrLevelConstants.All,
        Skills: CurriculumSkillConstants.All,
        ContextTags: CurriculumContextTagConstants.All);
}

// ── Routing preview ───────────────────────────────────────────────────────────

/// <summary>Admin routing preview request. No student data is mutated.</summary>
public sealed record AdminRoutingPreviewRequest(
    Guid? StudentId,
    string? CefrLevelOverride,
    IReadOnlyList<string>? LearningGoals,
    IReadOnlyList<string>? FocusAreas,
    string? PrimarySkill,
    string? Source,
    string? DifficultyPreference,
    bool AllowReviewOrScaffold,
    RoutingMode Mode = RoutingMode.NewLearning,
    /// <summary>Optional learning-plan objective key to test preferred routing.</summary>
    string? PreferredObjectiveKey = null);

/// <summary>Admin routing preview response.</summary>
public sealed record AdminRoutingPreviewResult(
    string TargetCefrLevel,
    string? CurriculumObjectiveKey,
    string? CurriculumObjectiveTitle,
    IReadOnlyList<string> ContextTags,
    IReadOnlyList<string> FocusTags,
    int DifficultyBand,
    string RoutingReason,
    bool IsLowerLevelContent,
    string? Explanation,
    bool FallbackUsed,
    bool NoExactObjectiveFound,
    IReadOnlyList<string> Warnings,
    /// <summary>
    /// Indicates the disposition of the PreferredObjectiveKey hint, when supplied.
    /// null = no hint supplied. "accepted" | "rejected" | "fallback_used".
    /// </summary>
    string? PreferredObjectiveDisposition = null);

// ── Write service interface ───────────────────────────────────────────────────

public interface ICurriculumObjectiveWriteService
{
    /// <summary>Creates a new objective. Fails if key already exists.</summary>
    Task<AdminCurriculumObjectiveDto> CreateAsync(AdminCurriculumObjectiveUpsertRequest request, CancellationToken ct = default);

    /// <summary>Updates all mutable fields of an existing objective by key.</summary>
    Task<AdminCurriculumObjectiveDto> UpdateAsync(string key, AdminCurriculumObjectiveUpsertRequest request, CancellationToken ct = default);

    /// <summary>Activates a previously deactivated objective.</summary>
    Task<AdminCurriculumObjectiveDto> ActivateAsync(string key, CancellationToken ct = default);

    /// <summary>Deactivates an objective without deleting historical records.</summary>
    Task<AdminCurriculumObjectiveDto> DeactivateAsync(string key, CancellationToken ct = default);

    /// <summary>Runs routing recommendation without mutating any student state.</summary>
    Task<AdminRoutingPreviewResult> PreviewRoutingAsync(AdminRoutingPreviewRequest request, CancellationToken ct = default);
}

// ── Validation summary DTOs (Phase 11B) ──────────────────────────────────────

public sealed record CurriculumValidationSummaryDto(
    bool IsValid,
    int TotalObjectivesChecked,
    int ErrorCount,
    int WarningCount,
    int CoverageGapCount,
    IReadOnlyList<CurriculumValidationIssueDto> Errors,
    IReadOnlyList<CurriculumValidationIssueDto> Warnings,
    IReadOnlyList<CurriculumCoverageGapDto> CoverageGaps);

public sealed record CurriculumValidationIssueDto(
    string ObjectiveKey,
    string Code,
    string Message);

public sealed record CurriculumCoverageGapDto(
    string CefrLevel,
    string Skill,
    string Message);

public sealed record CurriculumCoverageMatrixDto(
    IReadOnlyList<string> CefrLevels,
    IReadOnlyList<string> Skills,
    IReadOnlyList<CurriculumCoverageMatrixCellDto> Cells);

public sealed record CurriculumCoverageMatrixCellDto(
    string CefrLevel,
    string Skill,
    int ActiveCount,
    bool HasCoverage);

// ── Admin query extension ─────────────────────────────────────────────────────

/// <summary>Admin-only query extensions that include inactive objectives.</summary>
public interface IAdminCurriculumSyllabusQuery
{
    /// <summary>Returns all objectives (active and inactive) for admin management.</summary>
    Task<IReadOnlyList<CurriculumObjective>> GetAllObjectivesForAdminAsync(
        string? cefrLevel, string? skill, bool? isActive, CancellationToken ct = default);
}
