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
    ActivityTemplateCandidate = 4
}
