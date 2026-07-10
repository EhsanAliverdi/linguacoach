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
    ReadingPassage = 3
}
