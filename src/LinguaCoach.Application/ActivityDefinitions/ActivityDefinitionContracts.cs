namespace LinguaCoach.Application.ActivityDefinitions;

// ── Phase H4 — Activity foundation. A reviewable, editable practice task design generated from
// (or authored about) one or more published Resource Bank rows, optionally linked to a Learn
// Item — the "Practice" half of a future Module (Resource Bank Item → Learn Item/Activity →
// Module, see docs/architecture/product-model-realignment-h0.md). Distinct from the existing
// runtime LearningActivity (per-student delivery record) and ActivityTemplate (already wired into
// the live Practice Gym Form.io pilot) — see ActivityDefinition's doc comment. Nothing here
// creates a Module row, assigns anything to a student, or auto-publishes — every Activity starts
// pending review and only an explicit admin approve/reject changes that. ──

public sealed record ActivityResourceLinkDto(
    Guid LinkId,
    string ResourceType,
    Guid ResourceId,
    string Role,
    string? SnapshotTitle,
    string? ContentFingerprint
);

public sealed record ActivityDefinitionDto(
    Guid Id,
    string Title,
    string? Description,
    string Instructions,
    string ActivityType,
    string? PatternKey,
    string RendererType,
    string? FormSchemaJson,
    string? AnswerKeyJson,
    string? ScoringRulesJson,
    string? FeedbackPlanJson,
    string? CefrLevel,
    string? Skill,
    string? Subskill,
    string ContextTagsJson,
    string FocusTagsJson,
    int? DifficultyBand,
    int? EstimatedMinutes,
    Guid? LearnItemId,
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
    IReadOnlyList<ActivityResourceLinkDto> Links
);

public sealed record ListActivityDefinitionsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? ActivityType = null,
    string? RendererType = null,
    string? CefrLevel = null,
    string? Skill = null,
    string? Subskill = null,
    string? ContextTag = null,
    string? FocusTag = null,
    int? DifficultyBand = null,
    Guid? LearnItemId = null,
    string? Search = null
);

public sealed record ActivityDefinitionListResult(IReadOnlyList<ActivityDefinitionDto> Items, int TotalCount);

public interface IAdminActivityDefinitionListQuery
{
    Task<ActivityDefinitionListResult> HandleAsync(ListActivityDefinitionsQuery query, CancellationToken ct = default);
}

public sealed record GetActivityDefinitionQuery(Guid Id);

public interface IAdminActivityDefinitionGetQuery
{
    Task<ActivityDefinitionDto?> HandleAsync(GetActivityDefinitionQuery query, CancellationToken ct = default);
}

/// <summary>One resource to link an Activity to. <paramref name="ResourceType"/> is one of
/// "Vocabulary"/"Grammar"/"ReadingReference"/"ReadingPassage" (matches
/// <c>UnifiedResourceBankItemType</c>'s member names 1:1, same convention as
/// <c>LearnItemResourceLinkInput</c>).</summary>
public sealed record ActivityResourceLinkInput(string ResourceType, Guid ResourceId, string Role);

public sealed record CreateActivityDefinitionCommand(
    string Title,
    string Instructions,
    string ActivityType,
    string RendererType,
    string? Description,
    string? PatternKey,
    string? FormSchemaJson,
    string? AnswerKeyJson,
    string? ScoringRulesJson,
    string? FeedbackPlanJson,
    string? CefrLevel,
    string? Skill,
    string? Subskill,
    IReadOnlyList<string>? ContextTags,
    IReadOnlyList<string>? FocusTags,
    int? DifficultyBand,
    int? EstimatedMinutes,
    Guid? LearnItemId,
    IReadOnlyList<ActivityResourceLinkInput>? Links,
    Guid? CreatedByUserId
);

public interface IAdminCreateActivityDefinitionHandler
{
    Task<ActivityDefinitionDto> HandleAsync(CreateActivityDefinitionCommand command, CancellationToken ct = default);
}

public sealed record UpdateActivityDefinitionCommand(
    Guid Id,
    string Title,
    string Instructions,
    string? Description,
    string? FormSchemaJson,
    string? AnswerKeyJson,
    string? ScoringRulesJson,
    string? FeedbackPlanJson,
    string? CefrLevel,
    string? Skill,
    string? Subskill,
    IReadOnlyList<string>? ContextTags,
    IReadOnlyList<string>? FocusTags,
    int? DifficultyBand,
    int? EstimatedMinutes
);

public interface IAdminUpdateActivityDefinitionHandler
{
    Task<ActivityDefinitionDto> HandleAsync(UpdateActivityDefinitionCommand command, CancellationToken ct = default);
}

public sealed record ApproveActivityDefinitionCommand(Guid Id, Guid? ReviewedByUserId, string? Notes = null);

public interface IAdminApproveActivityDefinitionHandler
{
    Task<ActivityDefinitionDto> HandleAsync(ApproveActivityDefinitionCommand command, CancellationToken ct = default);
}

public sealed record RejectActivityDefinitionCommand(Guid Id, string Reason, Guid? ReviewedByUserId);

public interface IAdminRejectActivityDefinitionHandler
{
    Task<ActivityDefinitionDto> HandleAsync(RejectActivityDefinitionCommand command, CancellationToken ct = default);
}

public sealed class ActivityDefinitionValidationException : Exception
{
    public ActivityDefinitionValidationException(string message) : base(message) { }
}
