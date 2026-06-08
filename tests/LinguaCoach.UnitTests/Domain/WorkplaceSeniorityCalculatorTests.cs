using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class WorkplaceSeniorityCalculatorTests
{
    [Theory]
    // Base mappings with neutral familiarity (CurrentlyWorkingInRole).
    [InlineData(ProfessionalExperienceLevel.NoProfessionalExperience, RoleFamiliarity.CurrentlyWorkingInRole, DomainComplexity.BasicWorkplace)]
    [InlineData(ProfessionalExperienceLevel.Junior_0_2Years, RoleFamiliarity.CurrentlyWorkingInRole, DomainComplexity.JuniorRole)]
    [InlineData(ProfessionalExperienceLevel.MidLevel_2_5Years, RoleFamiliarity.CurrentlyWorkingInRole, DomainComplexity.IndependentContributor)]
    [InlineData(ProfessionalExperienceLevel.Senior_5_10Years, RoleFamiliarity.CurrentlyWorkingInRole, DomainComplexity.SeniorSpecialist)]
    [InlineData(ProfessionalExperienceLevel.LeadOrManager_10PlusYears, RoleFamiliarity.CurrentlyWorkingInRole, DomainComplexity.LeadOrManager)]
    // Career changer: senior experience but new to role steps down.
    [InlineData(ProfessionalExperienceLevel.Senior_5_10Years, RoleFamiliarity.NewToRole, DomainComplexity.IndependentContributor)]
    // Junior who manages/trains others steps up.
    [InlineData(ProfessionalExperienceLevel.Junior_0_2Years, RoleFamiliarity.ManagesOrTrainsOthers, DomainComplexity.IndependentContributor)]
    public void Compute_ReturnsExpectedComplexity(
        ProfessionalExperienceLevel level, RoleFamiliarity familiarity, DomainComplexity expected)
    {
        Assert.Equal(expected, WorkplaceSeniorityCalculator.Compute(level, familiarity));
    }

    [Fact]
    public void Compute_NeverGoesBelowBasicWorkplace()
    {
        var result = WorkplaceSeniorityCalculator.Compute(
            ProfessionalExperienceLevel.NoProfessionalExperience, RoleFamiliarity.NewToRole);
        Assert.Equal(DomainComplexity.BasicWorkplace, result);
    }

    [Fact]
    public void Compute_NeverExceedsLeadOrManager()
    {
        var result = WorkplaceSeniorityCalculator.Compute(
            ProfessionalExperienceLevel.LeadOrManager_10PlusYears, RoleFamiliarity.ManagesOrTrainsOthers);
        Assert.Equal(DomainComplexity.LeadOrManager, result);
    }
}
