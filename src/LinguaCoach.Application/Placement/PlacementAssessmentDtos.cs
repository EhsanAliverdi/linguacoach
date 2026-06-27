namespace LinguaCoach.Application.Placement;

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
