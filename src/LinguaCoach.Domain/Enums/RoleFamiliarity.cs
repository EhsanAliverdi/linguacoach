namespace LinguaCoach.Domain.Enums;

/// <summary>
/// How familiar the student is with their current/target role.
/// Refines domain scenario selection alongside
/// <see cref="ProfessionalExperienceLevel"/>.
/// See: docs/architecture/professional-experience-domain-complexity.md
/// </summary>
public enum RoleFamiliarity
{
    NewToRole = 0,
    UnderstandsBasics = 1,
    CurrentlyWorkingInRole = 2,
    ExperiencedInRole = 3,
    ManagesOrTrainsOthers = 4
}
