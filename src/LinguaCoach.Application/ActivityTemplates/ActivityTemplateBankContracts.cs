namespace LinguaCoach.Application.ActivityTemplates;

// ── Admin-facing activity template DTO — Form.io-native authoring. FormIoBaseSchemaJson is
// student-safe; ScoringModelJson/ValidationRulesJson/GenerationInstructions are backend-only
// and never appear in any student-facing DTO. ──

public sealed record AdminActivityTemplateDto(
    Guid TemplateId,
    string Key,
    int VersionNumber,
    Guid? PreviousVersionId,
    string Skill,
    string? Subskill,
    string CefrLevel,
    string ContextTagsJson,
    string FocusTagsJson,
    string? CurriculumObjectiveKey,
    string ActivityType,
    string? PatternKey,
    string? FormIoBaseSchemaJson,
    string? GenerationInstructions,
    string? ScoringModelJson,
    string? ValidationRulesJson,
    string ReviewStatus,
    bool IsPublished,
    int? EstimatedDurationSeconds,
    string? AssetRequirementsJson
);

// ── List templates (server-side paged, optionally filtered) ───────────────────

public sealed record ListAdminActivityTemplatesQuery(
    int Page = 1, int PageSize = 20, string? Skill = null, string? CefrLevel = null,
    string? ReviewStatus = null, string? Search = null);

/// <summary>Items is the current page only. TotalCount reflects filters (drives pagination);
/// OverallTotalCount/PublishedCount/SkillCount are always unfiltered, global bank stats for the
/// admin list's KPI strip.</summary>
public sealed record AdminActivityTemplateListResult(
    IReadOnlyList<AdminActivityTemplateDto> Items,
    int TotalCount,
    int OverallTotalCount,
    int PublishedCount,
    int SkillCount
);

public interface IAdminActivityTemplateListQuery
{
    Task<AdminActivityTemplateListResult> HandleAsync(ListAdminActivityTemplatesQuery query, CancellationToken ct = default);
}

// ── Get a single template ──────────────────────────────────────────────────────

public sealed record GetAdminActivityTemplateQuery(Guid TemplateId);

public interface IAdminActivityTemplateGetQuery
{
    Task<AdminActivityTemplateDto?> HandleAsync(GetAdminActivityTemplateQuery query, CancellationToken ct = default);
}

// ── Add template ───────────────────────────────────────────────────────────────

public sealed record AddActivityTemplateCommand(
    string Key,
    string Skill,
    string CefrLevel,
    string ActivityType,
    string? Subskill = null,
    string? PatternKey = null,
    string ContextTagsJson = "[]",
    string FocusTagsJson = "[]",
    string? CurriculumObjectiveKey = null,
    string? FormIoBaseSchemaJson = null,
    string? GenerationInstructions = null,
    string? ScoringModelJson = null,
    string? ValidationRulesJson = null,
    int? EstimatedDurationSeconds = null,
    string? AssetRequirementsJson = null
);

public interface IAdminAddActivityTemplateHandler
{
    Task<AdminActivityTemplateDto> HandleAsync(AddActivityTemplateCommand command, CancellationToken ct = default);
}

// ── Update template ─────────────────────────────────────────────────────────────

public sealed record UpdateActivityTemplateCommand(
    Guid TemplateId,
    string Skill,
    string CefrLevel,
    string ActivityType,
    string? Subskill,
    string? PatternKey,
    string ContextTagsJson,
    string FocusTagsJson,
    string? CurriculumObjectiveKey,
    string? FormIoBaseSchemaJson,
    string? GenerationInstructions,
    string? ScoringModelJson,
    string? ValidationRulesJson,
    int? EstimatedDurationSeconds,
    string? AssetRequirementsJson
);

public interface IAdminUpdateActivityTemplateHandler
{
    Task<AdminActivityTemplateDto> HandleAsync(UpdateActivityTemplateCommand command, CancellationToken ct = default);
}

// ── Remove template ─────────────────────────────────────────────────────────────

public sealed record RemoveActivityTemplateCommand(Guid TemplateId);

public interface IAdminRemoveActivityTemplateHandler
{
    Task HandleAsync(RemoveActivityTemplateCommand command, CancellationToken ct = default);
}

// ── Review status transitions ───────────────────────────────────────────────────

public sealed record SetActivityTemplateReviewStatusCommand(Guid TemplateId, string Action, string? Reason = null);

public interface IAdminActivityTemplateReviewHandler
{
    Task<AdminActivityTemplateDto> HandleAsync(SetActivityTemplateReviewStatusCommand command, CancellationToken ct = default);
}

// ── Publish / unpublish ─────────────────────────────────────────────────────────

public sealed record SetActivityTemplatePublishedCommand(Guid TemplateId, bool Publish);

public interface IAdminActivityTemplatePublishHandler
{
    Task<AdminActivityTemplateDto> HandleAsync(SetActivityTemplatePublishedCommand command, CancellationToken ct = default);
}

// ── Validation error ────────────────────────────────────────────────────────────

public sealed class ActivityTemplateValidationException : Exception
{
    public ActivityTemplateValidationException(string message) : base(message) { }
}
