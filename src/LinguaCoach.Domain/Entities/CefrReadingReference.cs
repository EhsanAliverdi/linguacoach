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

    // Phase E9 — published selection metadata, aligned with CefrReadingPassage. Nullable for
    // backward compatibility with pre-E9 rows. (DifficultyBand is the numeric 1-5 band used by
    // selectors; the free-text DifficultyNotes above stays as human-readable guidance.)
    public string? Subskill { get; private set; }
    public int? DifficultyBand { get; private set; }
    public string? ContextTagsJson { get; private set; }
    public string? FocusTagsJson { get; private set; }

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

    /// <summary>Phase E9 — sets the published selection metadata (used by the publish mapping and by
    /// the E9 backfill). DifficultyBand is validated to 1-5; tag JSON is stored as-is.</summary>
    public void SetSelectionMetadata(string? subskill, int? difficultyBand, string? contextTagsJson, string? focusTagsJson)
    {
        CefrBankMetadata.ValidateDifficultyBand(difficultyBand);
        Subskill = subskill?.Trim();
        DifficultyBand = difficultyBand;
        ContextTagsJson = contextTagsJson;
        FocusTagsJson = focusTagsJson;
    }
}
