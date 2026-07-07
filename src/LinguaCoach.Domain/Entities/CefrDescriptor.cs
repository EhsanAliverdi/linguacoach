using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A single CEFR "can-do" descriptor for a level/skill (and optionally subskill), sourced from
/// an external reference dataset. Reference data only — not used by any generation or
/// validation pipeline yet (Phase 5+ per docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md).
/// </summary>
public sealed class CefrDescriptor : BaseEntity
{
    public Guid SourceId { get; private set; }
    public string CefrLevel { get; private set; } = string.Empty;
    public string Skill { get; private set; } = string.Empty;
    public string? Subskill { get; private set; }
    public string CanDoStatement { get; private set; } = string.Empty;
    public string? Citation { get; private set; }

    private CefrDescriptor() { }

    public CefrDescriptor(
        Guid sourceId,
        string cefrLevel,
        string skill,
        string canDoStatement,
        string? subskill = null,
        string? citation = null)
    {
        if (sourceId == Guid.Empty)
            throw new ArgumentException("SourceId is required.", nameof(sourceId));
        if (!CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"Invalid CEFR level '{cefrLevel}'.", nameof(cefrLevel));
        if (!CurriculumSkillConstants.IsValid(skill))
            throw new ArgumentException($"Invalid skill '{skill}'.", nameof(skill));
        if (string.IsNullOrWhiteSpace(canDoStatement))
            throw new ArgumentException("CanDoStatement is required.", nameof(canDoStatement));
        if (!CurriculumSubskillConstants.IsValidForSkill(skill, subskill))
            throw new ArgumentException($"Subskill '{subskill}' does not belong to skill '{skill}'.", nameof(subskill));

        SourceId = sourceId;
        CefrLevel = cefrLevel.ToUpperInvariant();
        Skill = skill.ToLowerInvariant();
        Subskill = subskill?.Trim().ToLowerInvariant();
        CanDoStatement = canDoStatement.Trim();
        Citation = citation?.Trim();
    }
}
