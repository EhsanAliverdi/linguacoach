namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase E9 — small shared validation for the published-bank selection metadata that now lives on
/// the lean Cefr* bank entities (<see cref="CefrVocabularyEntry"/>/<see cref="CefrGrammarProfileEntry"/>/
/// <see cref="CefrReadingReference"/>) as well as <see cref="CefrReadingPassage"/>. Keeps the
/// difficulty-band rule (1-5, matching <see cref="CefrReadingPassage"/>) defined once.
/// </summary>
internal static class CefrBankMetadata
{
    public static void ValidateDifficultyBand(int? difficultyBand)
    {
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5.");
    }
}
