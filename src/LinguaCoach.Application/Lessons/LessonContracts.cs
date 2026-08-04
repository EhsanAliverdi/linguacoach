using LinguaCoach.Application.AdminRepair;

namespace LinguaCoach.Application.Lessons;

// ── Phase H3 — Lesson foundation. A reviewable teaching/explanation block generated from
// (or authored about) one or more published Resource Bank rows — the "Learn" half of a future
// Module (Resource Bank Item → Lesson/Activity → Module, see
// docs/architecture/product-model-realignment-h0.md). Nothing here creates an Activity or Module
// row, assigns anything to a student, or auto-publishes — every Lesson starts pending review
// and only an explicit admin approve/reject changes that. ──

public sealed record LessonResourceLinkDto(
    Guid LinkId,
    string ResourceType,
    Guid ResourceId,
    string Role,
    string? SnapshotTitle,
    string? ContentFingerprint
);

public sealed record LessonDto(
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
    IReadOnlyList<LessonResourceLinkDto> Links,
    bool IsArchived = false
);

public sealed record ListLessonsQuery(
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

public sealed record LessonListResult(IReadOnlyList<LessonDto> Items, int TotalCount);

public interface IAdminLessonListQuery
{
    Task<LessonListResult> HandleAsync(ListLessonsQuery query, CancellationToken ct = default);
}

public sealed record GetLessonQuery(Guid Id);

public interface IAdminLessonGetQuery
{
    Task<LessonDto?> HandleAsync(GetLessonQuery query, CancellationToken ct = default);
}

/// <summary>One resource to link a Lesson to. <paramref name="ResourceType"/> is one of
/// "Vocabulary"/"Grammar"/"ReadingReference"/"ReadingPassage" (matches
/// <c>UnifiedResourceBankItemType</c>'s member names 1:1).</summary>
public sealed record LessonResourceLinkInput(string ResourceType, Guid ResourceId, string Role);

public sealed record CreateLessonCommand(
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
    IReadOnlyList<LessonResourceLinkInput>? Links,
    Guid? CreatedByUserId
);

public interface IAdminCreateLessonHandler
{
    Task<LessonDto> HandleAsync(CreateLessonCommand command, CancellationToken ct = default);
}

public sealed record UpdateLessonCommand(
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

public interface IAdminUpdateLessonHandler
{
    Task<LessonDto> HandleAsync(UpdateLessonCommand command, CancellationToken ct = default);
}

public sealed record ApproveLessonCommand(Guid Id, Guid? ReviewedByUserId, string? Notes = null);

public interface IAdminApproveLessonHandler
{
    Task<LessonDto> HandleAsync(ApproveLessonCommand command, CancellationToken ct = default);
}

public sealed record RejectLessonCommand(Guid Id, string Reason, Guid? ReviewedByUserId);

public interface IAdminRejectLessonHandler
{
    Task<LessonDto> HandleAsync(RejectLessonCommand command, CancellationToken ct = default);
}

// ── Phase K6 — admin archive/unarchive (soft-delete), mirroring ResourceBankItem's
// ArchiveResourceBankItemsCommand pattern. Bulk is continue-on-error per id. ──

public sealed record ArchiveLessonsCommand(IReadOnlyList<Guid> Ids);
public sealed record UnarchiveLessonsCommand(IReadOnlyList<Guid> Ids);

public sealed record LessonArchiveItemResult(Guid Id, bool Success, string? Error);

public sealed record LessonArchiveResult(
    int RequestedCount,
    int SucceededCount,
    int FailedCount,
    IReadOnlyList<LessonArchiveItemResult> Items);

public interface ILessonArchiveHandler
{
    Task<LessonArchiveResult> ArchiveAsync(ArchiveLessonsCommand command, CancellationToken ct = default);
    Task<LessonArchiveResult> UnarchiveAsync(UnarchiveLessonsCommand command, CancellationToken ct = default);
}

// ── Phase K8 — "diagnose then AI-repair" for a Lesson missing core teaching content. ──

public sealed record LessonRepairResult(
    LessonDto Item,
    IReadOnlyList<DiagnosticIssue> IssuesFixed,
    IReadOnlyList<DiagnosticIssue> IssuesRemaining,
    string? ProviderName,
    string? ModelName);

public interface ILessonRepairService
{
    Task<IReadOnlyList<DiagnosticIssue>> DiagnoseAsync(Guid id, CancellationToken ct = default);
    Task<LessonRepairResult> RepairAsync(Guid id, CancellationToken ct = default);
    Task<IssuesSummary> GetIssuesSummaryAsync(CancellationToken ct = default);
    Task<BulkRepairResult> RepairAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RepairableItemSummary>> ListWithIssuesAsync(CancellationToken ct = default);
}

public sealed class LessonValidationException : Exception
{
    public LessonValidationException(string message) : base(message) { }
}
