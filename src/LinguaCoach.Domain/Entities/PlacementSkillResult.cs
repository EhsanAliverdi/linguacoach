using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Per-skill scoring outcome from an adaptive placement assessment (Phase 13A).
/// </summary>
public sealed class PlacementSkillResult : BaseEntity
{
    public Guid PlacementAssessmentId { get; private set; }
    public string Skill { get; private set; } = string.Empty;
    public string EstimatedCefrLevel { get; private set; } = string.Empty;
    public double Confidence { get; private set; }
    public int EvidenceCount { get; private set; }
    public string? Strengths { get; private set; }
    public string? Weaknesses { get; private set; }
    public string? RecommendedStartingObjectiveKeys { get; private set; }

    private PlacementSkillResult() { }

    public static PlacementSkillResult Create(
        Guid assessmentId,
        string skill,
        string estimatedCefrLevel,
        double confidence,
        int evidenceCount,
        string? strengths = null,
        string? weaknesses = null,
        string? recommendedObjectiveKeys = null)
    {
        return new PlacementSkillResult
        {
            PlacementAssessmentId = assessmentId,
            Skill = skill,
            EstimatedCefrLevel = estimatedCefrLevel,
            Confidence = confidence,
            EvidenceCount = evidenceCount,
            Strengths = strengths,
            Weaknesses = weaknesses,
            RecommendedStartingObjectiveKeys = recommendedObjectiveKeys
        };
    }
}
