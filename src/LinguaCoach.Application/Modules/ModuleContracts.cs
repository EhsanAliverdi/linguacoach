namespace LinguaCoach.Application.Modules;

// ── Phase H5 — Module foundation. A reusable, reviewable learning unit combining one
// or more Lessons and Exercises plus a module-level feedback plan — the top of the
// content-studio hierarchy (Resource Bank Item → Lesson/Exercise → Module
// Definition, see docs/architecture/product-model-realignment-h0.md). Distinct from the existing
// runtime LearningModule (a per-student thematic group within a LearningPath) — see
// Module's doc comment. Nothing here is delivered to a student, assigns anything, or
// auto-publishes — every Module starts pending review and only an explicit admin approve/reject
// changes that. Not wired into Today/Practice Gym runtime selection this phase. ──

public sealed record ModuleLessonLinkDto(
    Guid LinkId, Guid LessonId, string Role, int SortOrder, string? SnapshotTitle
);

public sealed record ModuleExerciseLinkDto(
    Guid LinkId, Guid ExerciseId, string Role, int SortOrder, bool Required, string? SnapshotTitle
);

public sealed record ModuleDto(
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
    IReadOnlyList<ModuleLessonLinkDto> LessonLinks,
    IReadOnlyList<ModuleExerciseLinkDto> ExerciseLinks
);

public sealed record ListModulesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? CefrLevel = null,
    string? Skill = null,
    string? Subskill = null,
    string? ContextTag = null,
    string? FocusTag = null,
    int? DifficultyBand = null,
    Guid? LessonId = null,
    Guid? ExerciseId = null,
    string? Search = null
);

public sealed record ModuleListResult(IReadOnlyList<ModuleDto> Items, int TotalCount);

public interface IAdminModuleListQuery
{
    Task<ModuleListResult> HandleAsync(ListModulesQuery query, CancellationToken ct = default);
}

public sealed record GetModuleQuery(Guid Id);

public interface IAdminModuleGetQuery
{
    Task<ModuleDto?> HandleAsync(GetModuleQuery query, CancellationToken ct = default);
}

public sealed record ModuleLessonLinkInput(Guid LessonId, string Role);
public sealed record ModuleExerciseLinkInput(Guid ExerciseId, string Role, bool Required = true);

public sealed record CreateModuleCommand(
    string Title,
    IReadOnlyList<ModuleLessonLinkInput> LessonLinks,
    IReadOnlyList<ModuleExerciseLinkInput> ExerciseLinks,
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

public interface IAdminCreateModuleHandler
{
    Task<ModuleDto> HandleAsync(CreateModuleCommand command, CancellationToken ct = default);
}

public sealed record UpdateModuleCommand(
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

public interface IAdminUpdateModuleHandler
{
    Task<ModuleDto> HandleAsync(UpdateModuleCommand command, CancellationToken ct = default);
}

public sealed record ApproveModuleCommand(Guid Id, Guid? ReviewedByUserId, string? Notes = null);

public interface IAdminApproveModuleHandler
{
    Task<ModuleDto> HandleAsync(ApproveModuleCommand command, CancellationToken ct = default);
}

public sealed record RejectModuleCommand(Guid Id, string Reason, Guid? ReviewedByUserId);

public interface IAdminRejectModuleHandler
{
    Task<ModuleDto> HandleAsync(RejectModuleCommand command, CancellationToken ct = default);
}

public sealed class ModuleValidationException : Exception
{
    public ModuleValidationException(string message) : base(message) { }
}
