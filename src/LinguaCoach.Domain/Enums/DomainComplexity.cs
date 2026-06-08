namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Workplace / professional complexity dimension — independent of CEFR language difficulty.
/// Used by the session generator to select appropriate workplace scenario topics.
/// Stored on StudentProfile as WorkplaceSeniority (effective DomainComplexity).
/// See: docs/architecture/professional-experience-domain-complexity.md
/// </summary>
public enum DomainComplexity
{
    BasicWorkplace = 0,
    JuniorRole = 1,
    IndependentContributor = 2,
    SeniorSpecialist = 3,
    LeadOrManager = 4
}
