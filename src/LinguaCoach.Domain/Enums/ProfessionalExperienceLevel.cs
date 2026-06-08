namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Professional experience level collected during onboarding.
/// Combined with <see cref="RoleFamiliarity"/> to compute effective
/// <see cref="DomainComplexity"/> (stored as WorkplaceSeniority).
/// See: docs/architecture/professional-experience-domain-complexity.md
/// </summary>
public enum ProfessionalExperienceLevel
{
    NoProfessionalExperience = 0,
    EntryLevelOrGraduate = 1,
    Junior_0_2Years = 2,
    MidLevel_2_5Years = 3,
    Senior_5_10Years = 4,
    LeadOrManager_10PlusYears = 5
}
