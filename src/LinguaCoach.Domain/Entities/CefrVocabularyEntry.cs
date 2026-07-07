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
}
