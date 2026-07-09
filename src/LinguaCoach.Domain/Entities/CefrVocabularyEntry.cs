using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A single word/phrase at a given CEFR level, sourced from an external reference dataset
/// (e.g. CEFR-J). Distinct from <see cref="VocabularyEntry"/>, which tracks one student's SRS
/// progress on a word — this is global reference data, not per-student state. Not used by any
/// generation or validation pipeline yet (Phase 5+ per
/// docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md).
/// </summary>
public sealed class CefrVocabularyEntry : BaseEntity
{
    public Guid SourceId { get; private set; }
    public string Word { get; private set; } = string.Empty;
    public string CefrLevel { get; private set; } = string.Empty;
    public string? PartOfSpeech { get; private set; }
    public string? Notes { get; private set; }

    // Phase E9 — published selection metadata, aligned with CefrReadingPassage so Today/D5/PG-v2
    // selectors can filter this bank by context/focus/subskill/difficulty without re-querying the
    // staging ResourceCandidate. Nullable for backward compatibility with pre-E9 rows.
    public string? Subskill { get; private set; }
    public int? DifficultyBand { get; private set; }
    public string? ContextTagsJson { get; private set; }
    public string? FocusTagsJson { get; private set; }

    private CefrVocabularyEntry() { }

    public CefrVocabularyEntry(
        Guid sourceId,
        string word,
        string cefrLevel,
        string? partOfSpeech = null,
        string? notes = null)
    {
        if (sourceId == Guid.Empty)
            throw new ArgumentException("SourceId is required.", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(word))
            throw new ArgumentException("Word is required.", nameof(word));
        if (!CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"Invalid CEFR level '{cefrLevel}'.", nameof(cefrLevel));

        SourceId = sourceId;
        Word = word.Trim();
        CefrLevel = cefrLevel.ToUpperInvariant();
        PartOfSpeech = partOfSpeech?.Trim();
        Notes = notes?.Trim();
    }

    /// <summary>Phase E9 — sets the published selection metadata (used by the publish mapping and by
    /// the E9 backfill). DifficultyBand is validated to 1-5 (matching CefrReadingPassage); tag JSON
    /// is stored as-is.</summary>
    public void SetSelectionMetadata(string? subskill, int? difficultyBand, string? contextTagsJson, string? focusTagsJson)
    {
        CefrBankMetadata.ValidateDifficultyBand(difficultyBand);
        Subskill = subskill?.Trim();
        DifficultyBand = difficultyBand;
        ContextTagsJson = contextTagsJson;
        FocusTagsJson = focusTagsJson;
    }
}
