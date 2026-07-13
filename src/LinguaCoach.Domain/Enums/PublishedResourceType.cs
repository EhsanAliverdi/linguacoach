namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Phase H3 — the four typed published Resource Bank tables a <see cref="Entities.LessonResourceLink"/>
/// can point to. Deliberately mirrors (but does not reference — Domain must not depend on
/// Application) <c>LinguaCoach.Application.ResourceImport.UnifiedResourceBankItemType</c> from the
/// Phase H1 unified read model; Infrastructure maps between the two.
/// </summary>
public enum PublishedResourceType
{
    Vocabulary = 0,
    Grammar = 1,
    ReadingReference = 2,
    ReadingPassage = 3,
    // Phase J5a — a published writing prompt (title + task instructions + optional genre/word
    // count target). No answer key/rubric — this is content, not a scoring implementation.
    Writing = 4,
    // Phase J5c — a published listening passage (title + optional transcript + a real uploaded
    // audio file's storage key/content type).
    Listening = 5
}
