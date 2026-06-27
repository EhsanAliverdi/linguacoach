namespace LinguaCoach.Application.Placement;

// ── Phase 13B — Response Submission + Progress DTOs ─────────────────────────

public sealed record SubmitResponseResult(
    Guid ItemId,
    bool IsCorrect,
    double Score,
    string EvaluationNotes,
    bool AssessmentComplete,
    string? CompletionReason,
    PlacementNextItemDto? NextItem,
    PlacementAssessmentSummaryDto? Summary);

public sealed record PlacementNextItemDto(
    Guid ItemId,
    string Skill,
    string TargetCefrLevel,
    string ItemType,
    string Prompt,
    int ItemOrder,
    int AnsweredCount,
    int EstimatedRemainingItems);

public sealed record PlacementSkillProgressDto(
    string Skill,
    string CurrentEstimatedLevel,
    double Confidence,
    int EvidenceCount,
    int ConsecutiveSuccesses,
    int ConsecutiveFailures);

public sealed record PlacementItemHistoryDto(
    Guid ItemId,
    string Skill,
    string TargetCefrLevel,
    string ItemType,
    string Prompt,
    string? Response,
    bool? IsCorrect,
    double? Score,
    DateTime? EvaluatedAtUtc,
    string? EvaluationNotes,
    int? DurationSeconds,
    int ItemOrder);

public sealed record PlacementAssessmentProgressDto(
    Guid AssessmentId,
    string Status,
    int AnsweredCount,
    int TotalItemCount,
    int EstimatedRemainingItems,
    string? CurrentSkill,
    string? CurrentCefrLevel,
    double OverallConfidence,
    IReadOnlyList<PlacementSkillProgressDto> SkillProgress,
    IReadOnlyList<PlacementItemHistoryDto> ItemHistory,
    string? CompletionReason);



public sealed record PlacementSkillResultDto(
    string Skill,
    string EstimatedCefrLevel,
    double Confidence,
    int EvidenceCount,
    string? Strengths,
    string? Weaknesses,
    IReadOnlyList<string> RecommendedObjectiveKeys);

public sealed record PlacementAssessmentSummaryDto(
    Guid AssessmentId,
    Guid StudentProfileId,
    string Status,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? ExpiredAtUtc,
    string? OverallCefrLevel,
    double? OverallConfidence,
    bool IsProvisional,
    string? ResultSummary,
    string? Source,
    IReadOnlyList<PlacementSkillResultDto> SkillResults,
    bool LearningPlanRegenerated,
    string? LearningPlanRegenerationWarning,
    int ItemCount);

public sealed record PlacementHistoryItemDto(
    Guid AssessmentId,
    string Status,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? OverallCefrLevel,
    double? OverallConfidence,
    bool IsProvisional,
    int ItemCount);
