namespace LinguaCoach.Application.ModuleDefinitions;

// ── Phase H5 — Module Definition foundation. A reusable, reviewable learning unit combining one
// or more Learn Items and Activity Definitions plus a module-level feedback plan — the top of the
// content-studio hierarchy (Resource Bank Item → Learn Item/Activity Definition → Module
// Definition, see docs/architecture/product-model-realignment-h0.md). Distinct from the existing
// runtime LearningModule (a per-student thematic group within a LearningPath) — see
// ModuleDefinition's doc comment. Nothing here is delivered to a student, assigns anything, or
// auto-publishes — every Module starts pending review and only an explicit admin approve/reject
// changes that. Not wired into Today/Practice Gym runtime selection this phase. ──

public sealed record ModuleLearnItemLinkDto(
    Guid LinkId, Guid LearnItemId, string Role, int SortOrder, string? SnapshotTitle
);

public sealed record ModuleActivityLinkDto(
    Guid LinkId, Guid ActivityDefinitionId, string Role, int SortOrder, bool Required, string? SnapshotTitle
);

public sealed record ModuleDefinitionDto(
    Guid Id,
    string Title,
    string? Description,
    string? ObjectiveKey,
    string? CefrLevel,
    string? Skill,
    string? Subskill,
    string ContextTagsJson,
    string FocusTagsJson,
    int? DifficultyBand,
    int? EstimatedMinutes,
    string? FeedbackPlanJson,
    string SourceMode,
    string? GenerationProvider,
    string? GenerationModel,
    string ReviewStatus,
    Guid? CreatedByUserId,
    Guid? ReviewedByUserId,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? RejectedAtUtc,
    string? RejectionReason,
    string? ReviewNotes,
    DateTime CreatedAt,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ModuleLearnItemLinkDto> LearnItemLinks,
    IReadOnlyList<ModuleActivityLinkDto> ActivityLinks
);

public sealed record ListModuleDefinitionsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? CefrLevel = null,
    string? Skill = null,
    string? Subskill = null,
    string? ContextTag = null,
    string? FocusTag = null,
    int? DifficultyBand = null,
    Guid? LearnItemId = null,
    Guid? ActivityDefinitionId = null,
    string? Search = null
);

public sealed record ModuleDefinitionListResult(IReadOnlyList<ModuleDefinitionDto> Items, int TotalCount);

public interface IAdminModuleDefinitionListQuery
{
    Task<ModuleDefinitionListResult> HandleAsync(ListModuleDefinitionsQuery query, CancellationToken ct = default);
}

public sealed record GetModuleDefinitionQuery(Guid Id);

public interface IAdminModuleDefinitionGetQuery
{
    Task<ModuleDefinitionDto?> HandleAsync(GetModuleDefinitionQuery query, CancellationToken ct = default);
}

public sealed record ModuleLearnItemLinkInput(Guid LearnItemId, string Role);
public sealed record ModuleActivityLinkInput(Guid ActivityDefinitionId, string Role, bool Required = true);

public sealed record CreateModuleDefinitionCommand(
    string Title,
    IReadOnlyList<ModuleLearnItemLinkInput> LearnItemLinks,
    IReadOnlyList<ModuleActivityLinkInput> ActivityLinks,
    string? Description = null,
    string? ObjectiveKey = null,
    string? CefrLevel = null,
    string? Skill = null,
    string? Subskill = null,
    IReadOnlyList<string>? ContextTags = null,
    IReadOnlyList<string>? FocusTags = null,
    int? DifficultyBand = null,
    int? EstimatedMinutes = null,
    string? FeedbackPlanJson = null,
    Guid? CreatedByUserId = null
);

public interface IAdminCreateModuleDefinitionHandler
{
    Task<ModuleDefinitionDto> HandleAsync(CreateModuleDefinitionCommand command, CancellationToken ct = default);
}

public sealed record UpdateModuleDefinitionCommand(
    Guid Id,
    string Title,
    string? Description,
    string? CefrLevel,
    string? Skill,
    string? Subskill,
    IReadOnlyList<string>? ContextTags,
    IReadOnlyList<string>? FocusTags,
    int? DifficultyBand,
    int? EstimatedMinutes,
    string? FeedbackPlanJson
);

public interface IAdminUpdateModuleDefinitionHandler
{
    Task<ModuleDefinitionDto> HandleAsync(UpdateModuleDefinitionCommand command, CancellationToken ct = default);
}

public sealed record ApproveModuleDefinitionCommand(Guid Id, Guid? ReviewedByUserId, string? Notes = null);

public interface IAdminApproveModuleDefinitionHandler
{
    Task<ModuleDefinitionDto> HandleAsync(ApproveModuleDefinitionCommand command, CancellationToken ct = default);
}

public sealed record RejectModuleDefinitionCommand(Guid Id, string Reason, Guid? ReviewedByUserId);

public interface IAdminRejectModuleDefinitionHandler
{
    Task<ModuleDefinitionDto> HandleAsync(RejectModuleDefinitionCommand command, CancellationToken ct = default);
}

public sealed class ModuleDefinitionValidationException : Exception
{
    public ModuleDefinitionValidationException(string message) : base(message) { }
}
