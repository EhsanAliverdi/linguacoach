using System.Text.Json;
using LinguaCoach.Application.AdminRepair;

namespace LinguaCoach.Application.Exercises;

// ── Phase H4 — Activity foundation. A reviewable, editable practice task design generated from
// (or authored about) one or more published Resource Bank rows, optionally linked to a Learn
// Item — the "Practice" half of a future Module (Resource Bank Item → Lesson/Activity →
// Module, see docs/architecture/product-model-realignment-h0.md). Distinct from the existing
// runtime LearningActivity (per-student delivery record) — see Exercise's doc comment.
// The legacy ActivityTemplate Form.io pilot this used to be contrasted against was removed in
// Phase I2A; see docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.
// Nothing here creates a Module row, assigns anything to a student, or auto-publishes — every
// Activity starts pending review and only an explicit admin approve/reject changes that. ──

public sealed record ExerciseResourceLinkDto(
    Guid LinkId,
    string ResourceType,
    Guid ResourceId,
    string Role,
    string? SnapshotTitle,
    string? ContentFingerprint
);

public sealed record ExerciseDto(
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
    Guid? LessonId,
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
    IReadOnlyList<ExerciseResourceLinkDto> Links,
    /// <summary>Phase J4 — whether this activity type/content shape would be launchable to a
    /// student once approved, per <see cref="ExerciseLaunch.ExerciseLaunchEligibility.EvaluateContentSupport"/>.
    /// Independent of this Exercise's own current review status — a draft can be
    /// <c>CanLaunchOnceApproved = false</c> (e.g. "short_answer" has no auto-scoring path yet)
    /// while still being a legitimate, reviewable, approvable draft; this only tells the admin
    /// honestly whether it will ever reach a student, so they aren't surprised later.</summary>
    bool CanLaunchOnceApproved = true,
    string? LaunchUnsupportedReason = null,
    bool IsArchived = false
);

public sealed record ListExercisesQuery(
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
    Guid? LessonId = null,
    string? Search = null
);

public sealed record ExerciseListResult(IReadOnlyList<ExerciseDto> Items, int TotalCount);

public interface IAdminExerciseListQuery
{
    Task<ExerciseListResult> HandleAsync(ListExercisesQuery query, CancellationToken ct = default);
}

public sealed record GetExerciseQuery(Guid Id);

public interface IAdminExerciseGetQuery
{
    Task<ExerciseDto?> HandleAsync(GetExerciseQuery query, CancellationToken ct = default);
}

/// <summary>One resource to link an Activity to. <paramref name="ResourceType"/> is one of
/// "Vocabulary"/"Grammar"/"ReadingReference"/"ReadingPassage" (matches
/// <c>UnifiedResourceBankItemType</c>'s member names 1:1, same convention as
/// <c>LessonResourceLinkInput</c>).</summary>
public sealed record ExerciseResourceLinkInput(string ResourceType, Guid ResourceId, string Role);

public sealed record CreateExerciseCommand(
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
    Guid? LessonId,
    IReadOnlyList<ExerciseResourceLinkInput>? Links,
    Guid? CreatedByUserId
);

public interface IAdminCreateExerciseHandler
{
    Task<ExerciseDto> HandleAsync(CreateExerciseCommand command, CancellationToken ct = default);
}

public sealed record UpdateExerciseCommand(
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

public interface IAdminUpdateExerciseHandler
{
    Task<ExerciseDto> HandleAsync(UpdateExerciseCommand command, CancellationToken ct = default);
}

public sealed record ApproveExerciseCommand(Guid Id, Guid? ReviewedByUserId, string? Notes = null);

public interface IAdminApproveExerciseHandler
{
    Task<ExerciseDto> HandleAsync(ApproveExerciseCommand command, CancellationToken ct = default);
}

public sealed record RejectExerciseCommand(Guid Id, string Reason, Guid? ReviewedByUserId);

public interface IAdminRejectExerciseHandler
{
    Task<ExerciseDto> HandleAsync(RejectExerciseCommand command, CancellationToken ct = default);
}

// ── Phase K6 — admin archive/unarchive (soft-delete), mirroring ResourceBankItem's
// ArchiveResourceBankItemsCommand pattern. Bulk is continue-on-error per id. ──

public sealed record ArchiveExercisesCommand(IReadOnlyList<Guid> Ids);
public sealed record UnarchiveExercisesCommand(IReadOnlyList<Guid> Ids);

public sealed record ExerciseArchiveItemResult(Guid Id, bool Success, string? Error);

public sealed record ExerciseArchiveResult(
    int RequestedCount,
    int SucceededCount,
    int FailedCount,
    IReadOnlyList<ExerciseArchiveItemResult> Items);

public interface IExerciseArchiveHandler
{
    Task<ExerciseArchiveResult> ArchiveAsync(ArchiveExercisesCommand command, CancellationToken ct = default);
    Task<ExerciseArchiveResult> UnarchiveAsync(UnarchiveExercisesCommand command, CancellationToken ct = default);
}

// ── Phase K7 — admin "preview as a learner" for a standalone Exercise (not via a Module). Scores
// a preview submission using the exact same ComponentAnswerScorer logic the real student runtime
// and Module preview use. Never creates a LearningActivity/ActivityAttempt. ──

public sealed record ExercisePreviewComponentResult(string ComponentKey, bool IsCorrect, double PointsEarned, double MaxPoints);

public sealed record ExercisePreviewSubmitRequest(Guid ExerciseId, Dictionary<string, JsonElement> Answers);

public sealed record ExercisePreviewSubmitResult(
    bool Scored,
    string? UnscorableReason,
    double? ScorePercent,
    bool? AllCorrect,
    IReadOnlyList<ExercisePreviewComponentResult> Components,
    string? FeedbackMessage);

public interface IAdminExercisePreviewSubmitHandler
{
    Task<ExercisePreviewSubmitResult> HandleAsync(ExercisePreviewSubmitRequest request, CancellationToken ct = default);
}

// ── Phase K8 — "diagnose then AI-repair" for an Exercise. Deliberately narrow: only fills
// missing Description/Instructions (safe descriptive text). Never touches FormSchemaJson,
// AnswerKeyJson, or ScoringRulesJson — those are correctness-critical and flagged, not
// auto-fixed, mirroring AiExerciseGenerationService's "never AI-supplied answer" principle. ──

public sealed record ExerciseRepairResult(
    ExerciseDto Item,
    IReadOnlyList<DiagnosticIssue> IssuesFixed,
    IReadOnlyList<DiagnosticIssue> IssuesRemaining,
    string? ProviderName,
    string? ModelName);

public interface IExerciseRepairService
{
    Task<IReadOnlyList<DiagnosticIssue>> DiagnoseAsync(Guid id, CancellationToken ct = default);
    Task<ExerciseRepairResult> RepairAsync(Guid id, CancellationToken ct = default);
    Task<IssuesSummary> GetIssuesSummaryAsync(CancellationToken ct = default);
    Task<BulkRepairResult> RepairAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RepairableItemSummary>> ListWithIssuesAsync(CancellationToken ct = default);
}

public sealed class ExerciseValidationException : Exception
{
    public ExerciseValidationException(string message) : base(message) { }
}
