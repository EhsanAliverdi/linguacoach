using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Reference metadata about reading difficulty/text-type expectations at a CEFR level, sourced
/// from an external reference dataset. Intentionally holds only a short excerpt/citation, not a
/// full copyrighted text — reading difficulty guidance, not a content library. Not used by any
/// generation or validation pipeline yet (Phase 5+ per
/// docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md).
/// </summary>
public sealed class CefrReadingReference : BaseEntity
{
    public Guid SourceId { get; private set; }
    public string CefrLevel { get; private set; } = string.Empty;
    public string? TextType { get; private set; }
    public string? DifficultyNotes { get; private set; }
    public string? ReferenceExcerpt { get; private set; }

    private CefrReadingReference() { }

    public CefrReadingReference(
        Guid sourceId,
        string cefrLevel,
        string? textType = null,
        string? difficultyNotes = null,
        string? referenceExcerpt = null)
    {
        if (sourceId == Guid.Empty)
            throw new ArgumentException("SourceId is required.", nameof(sourceId));
        if (!CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"Invalid CEFR level '{cefrLevel}'.", nameof(cefrLevel));

        SourceId = sourceId;
        CefrLevel = cefrLevel.ToUpperInvariant();
        TextType = textType?.Trim();
        DifficultyNotes = difficultyNotes?.Trim();
        ReferenceExcerpt = referenceExcerpt?.Trim();
    }
}
