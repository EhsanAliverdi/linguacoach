using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A single grammar point attributed to a CEFR level, sourced from an external reference
/// dataset (e.g. CEFR-J grammar profile). Reference data only — not used by any generation or
/// validation pipeline yet (Phase 5+ per docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md).
/// </summary>
public sealed class CefrGrammarProfileEntry : BaseEntity
{
    public Guid SourceId { get; private set; }
    public string CefrLevel { get; private set; } = string.Empty;
    public string GrammarPoint { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private CefrGrammarProfileEntry() { }

    public CefrGrammarProfileEntry(
        Guid sourceId,
        string cefrLevel,
        string grammarPoint,
        string? description = null)
    {
        if (sourceId == Guid.Empty)
            throw new ArgumentException("SourceId is required.", nameof(sourceId));
        if (!CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"Invalid CEFR level '{cefrLevel}'.", nameof(cefrLevel));
        if (string.IsNullOrWhiteSpace(grammarPoint))
            throw new ArgumentException("GrammarPoint is required.", nameof(grammarPoint));

        SourceId = sourceId;
        CefrLevel = cefrLevel.ToUpperInvariant();
        GrammarPoint = grammarPoint.Trim();
        Description = description?.Trim();
    }
}
