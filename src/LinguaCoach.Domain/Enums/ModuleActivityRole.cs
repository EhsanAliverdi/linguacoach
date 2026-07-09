namespace LinguaCoach.Domain.Enums;

/// <summary>Phase H5 — a <see cref="Entities.ModuleDefinitionActivityLink"/>'s role within its
/// Module. Distinct from <see cref="LearnItemResourceRole"/> (Primary/Supporting, used for the
/// Learn Item link) because a Module's practice activities have a richer set of roles than a
/// single source resource link does.</summary>
public enum ModuleActivityRole
{
    PrimaryPractice = 0,
    SupportingPractice = 1,
    Review = 2,
    Extension = 3
}
