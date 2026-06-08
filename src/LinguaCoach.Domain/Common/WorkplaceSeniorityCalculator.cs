using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Common;

/// <summary>
/// Computes effective <see cref="DomainComplexity"/> (WorkplaceSeniority) from a student's
/// professional experience level and role familiarity.
/// See: docs/architecture/professional-experience-domain-complexity.md
/// </summary>
public static class WorkplaceSeniorityCalculator
{
    /// <summary>
    /// Maps experience level and role familiarity to an effective DomainComplexity.
    /// A high experience level combined with low familiarity (e.g. career changer)
    /// is reduced so the student is not exposed to senior-level domain concepts prematurely.
    /// </summary>
    public static DomainComplexity Compute(
        ProfessionalExperienceLevel experienceLevel,
        RoleFamiliarity roleFamiliarity)
    {
        var baseComplexity = MapExperienceToComplexity(experienceLevel);

        // Familiarity adjusts the base. Low familiarity reduces; high familiarity can raise.
        var adjustment = MapFamiliarityAdjustment(roleFamiliarity);

        var raw = (int)baseComplexity + adjustment;
        var clamped = Math.Clamp(raw, (int)DomainComplexity.BasicWorkplace, (int)DomainComplexity.LeadOrManager);
        return (DomainComplexity)clamped;
    }

    private static DomainComplexity MapExperienceToComplexity(ProfessionalExperienceLevel level) => level switch
    {
        ProfessionalExperienceLevel.NoProfessionalExperience => DomainComplexity.BasicWorkplace,
        ProfessionalExperienceLevel.EntryLevelOrGraduate => DomainComplexity.BasicWorkplace,
        ProfessionalExperienceLevel.Junior_0_2Years => DomainComplexity.JuniorRole,
        ProfessionalExperienceLevel.MidLevel_2_5Years => DomainComplexity.IndependentContributor,
        ProfessionalExperienceLevel.Senior_5_10Years => DomainComplexity.SeniorSpecialist,
        ProfessionalExperienceLevel.LeadOrManager_10PlusYears => DomainComplexity.LeadOrManager,
        _ => DomainComplexity.BasicWorkplace
    };

    private static int MapFamiliarityAdjustment(RoleFamiliarity familiarity) => familiarity switch
    {
        // New to the role / only understands basics → step down one complexity level.
        RoleFamiliarity.NewToRole => -1,
        RoleFamiliarity.UnderstandsBasics => -1,
        RoleFamiliarity.CurrentlyWorkingInRole => 0,
        RoleFamiliarity.ExperiencedInRole => 0,
        // Manages/trains others → step up one complexity level.
        RoleFamiliarity.ManagesOrTrainsOthers => 1,
        _ => 0
    };
}
