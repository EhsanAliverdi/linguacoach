namespace LinguaCoach.Application.LearnItems;

// ── Phase H3 — Learn Item foundation. A reviewable teaching/explanation block generated from
// (or authored about) one or more published Resource Bank rows — the "Learn" half of a future
// Module (Resource Bank Item → Learn Item/Activity → Module, see
// docs/architecture/product-model-realignment-h0.md). Nothing here creates an Activity or Module
// row, assigns anything to a student, or auto-publishes — every Learn Item starts pending review
// and only an explicit admin approve/reject changes that. ──

public sealed record LearnItemResourceLinkDto(
    Guid LinkId,
    string ResourceType,
    Guid ResourceId,
    string Role,
    string? SnapshotTitle,
    string? ContentFingerprint
);

public sealed record LearnItemDto(
    Guid Id,
    string Title,
    string Body,
    string ExamplesJson,
    string CommonMistakesJson,
    string? UsageNotes,
    string? CefrLevel,
    string? Skill,
    string? Subskill,
    string ContextTagsJson,
    string FocusTagsJson,
    int? DifficultyBand,
    int? EstimatedMinutes,
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
    IReadOnlyList<LearnItemResourceLinkDto> Links
);

public sealed record ListLearnItemsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? CefrLevel = null,
    string? Skill = null,
    string? Subskill = null,
    string? ContextTag = null,
    string? FocusTag = null,
    int? DifficultyBand = null,
    string? Search = null,
    string? ResourceType = null,
    Guid? ResourceId = null
);

public sealed record LearnItemListResult(IReadOnlyList<LearnItemDto> Items, int TotalCount);

public interface IAdminLearnItemListQuery
{
    Task<LearnItemListResult> HandleAsync(ListLearnItemsQuery query, CancellationToken ct = default);
}

public sealed record GetLearnItemQuery(Guid Id);

public interface IAdminLearnItemGetQuery
{
    Task<LearnItemDto?> HandleAsync(GetLearnItemQuery query, CancellationToken ct = default);
}

/// <summary>One resource to link a Learn Item to. <paramref name="ResourceType"/> is one of
/// "Vocabulary"/"Grammar"/"ReadingReference"/"ReadingPassage" (matches
/// <c>UnifiedResourceBankItemType</c>'s member names 1:1).</summary>
public sealed record LearnItemResourceLinkInput(string ResourceType, Guid ResourceId, string Role);

public sealed record CreateLearnItemCommand(
    string Title,
    string Body,
    string? CefrLevel,
    string? Skill,
    string? Subskill,
    IReadOnlyList<string>? ContextTags,
    IReadOnlyList<string>? FocusTags,
    IReadOnlyList<string>? Examples,
    IReadOnlyList<string>? CommonMistakes,
    string? UsageNotes,
    int? DifficultyBand,
    int? EstimatedMinutes,
    IReadOnlyList<LearnItemResourceLinkInput>? Links,
    Guid? CreatedByUserId
);

public interface IAdminCreateLearnItemHandler
{
    Task<LearnItemDto> HandleAsync(CreateLearnItemCommand command, CancellationToken ct = default);
}

public sealed record UpdateLearnItemCommand(
    Guid Id,
    string Title,
    string Body,
    IReadOnlyList<string>? Examples,
    IReadOnlyList<string>? CommonMistakes,
    string? UsageNotes,
    string? CefrLevel,
    string? Skill,
    string? Subskill,
    IReadOnlyList<string>? ContextTags,
    IReadOnlyList<string>? FocusTags,
    int? DifficultyBand,
    int? EstimatedMinutes
);

public interface IAdminUpdateLearnItemHandler
{
    Task<LearnItemDto> HandleAsync(UpdateLearnItemCommand command, CancellationToken ct = default);
}

public sealed record ApproveLearnItemCommand(Guid Id, Guid? ReviewedByUserId, string? Notes = null);

public interface IAdminApproveLearnItemHandler
{
    Task<LearnItemDto> HandleAsync(ApproveLearnItemCommand command, CancellationToken ct = default);
}

public sealed record RejectLearnItemCommand(Guid Id, string Reason, Guid? ReviewedByUserId);

public interface IAdminRejectLearnItemHandler
{
    Task<LearnItemDto> HandleAsync(RejectLearnItemCommand command, CancellationToken ct = default);
}

public sealed class LearnItemValidationException : Exception
{
    public LearnItemValidationException(string message) : base(message) { }
}
