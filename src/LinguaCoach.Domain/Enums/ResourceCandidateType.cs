namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Inferred shape of a staged <see cref="Entities.ResourceCandidate"/>, based on which
/// recognizable fields were present on the raw imported row. Purely a staging-time
/// classification — no downstream bank table mapping happens in Phase E1.
/// </summary>
public enum ResourceCandidateType
{
    Unknown = 0,
    VocabularyEntry = 1,
    GrammarProfileEntry = 2,
    ReadingPassage = 3,
    ActivityTemplateCandidate = 4,
    // Phase J5a — a staged writing prompt (task instructions a student would respond to in free
    // text), recognized from a row's "prompt" field.
    WritingPrompt = 5,
    // Phase J5c — a staged listening passage: title/transcript metadata plus a real uploaded
    // audio file attached separately after staging (see ResourceCandidate.AudioStorageKey).
    // Recognized from a row's "transcript" field.
    ListeningPassage = 6
}
