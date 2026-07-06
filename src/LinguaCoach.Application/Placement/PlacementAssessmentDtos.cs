using LinguaCoach.Domain.Questions;

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
    int EstimatedRemainingItems,
    string? ReadingPassage = null,
    bool HasAudio = false,
    // Unified Question-Schema (Phase 2) — the shared, polymorphic representation of this item,
    // additive alongside the legacy flat fields above until the frontend renderer (Phase 3)
    // switches to consuming it and the flat fields are dropped (Phase 7).
    QuestionContent? Content = null,
    // Form.io onboarding/placement redesign — student-safe Form.io schema for this item, either
    // authored directly (PlacementItemDefinition.FormIoSchemaJson) or derived server-side from
    // the redacted Content above. Never contains a correct answer.
    string? FormIoSchemaJson = null,
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
    string? Response,
    bool? IsCorrect,
    double? Score,
    DateTime? EvaluatedAtUtc,
    string? EvaluationNotes,
    int? DurationSeconds,
    int ItemOrder,
    QuestionContent? Content = null,
    QuestionAnswer? Answer = null);

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
