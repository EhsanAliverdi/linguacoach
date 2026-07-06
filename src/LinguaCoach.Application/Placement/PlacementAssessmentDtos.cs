using System.Text.Json;

namespace LinguaCoach.Application.Placement;

// ── Phase 13B — Response Submission + Progress DTOs ─────────────────────────
// Form.io-native (post-migration): submissions carry the full Form.io submission.data
// dictionary instead of a single string response; ScoringRulesJson is never included in any
// of these student-facing DTOs.

/// <summary>Wire shape for a Form.io submission: { data: { <componentKey>: <value>, ... } }.</summary>
public sealed record PlacementSubmissionPayload(Dictionary<string, JsonElement> Data);

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
    int EstimatedRemainingItems,
    bool HasAudio,
    // Student-safe Form.io schema for this item — never contains a correct answer or any
    // scoring data. ScoringRulesJson is deliberately never surfaced anywhere in this DTO.
    string? FormIoSchemaJson,
    string RendererKind = "FormIo");

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
    Dictionary<string, JsonElement>? SubmissionData,
    Dictionary<string, string?>? NormalizedAnswer,
    bool? IsCorrect,
    double? Score,
    DateTime? EvaluatedAtUtc,
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

/// <summary>Per-skill status for the placement cards page — one card per skill.</summary>
public sealed record PlacementSkillStatusDto(
    string Skill,
    string Label,
    double PercentComplete,
    bool Completed,
    int EvidenceCount);

public sealed record PlacementHistoryItemDto(
    Guid AssessmentId,
    string Status,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? OverallCefrLevel,
    double? OverallConfidence,
    bool IsProvisional,
    int ItemCount);
