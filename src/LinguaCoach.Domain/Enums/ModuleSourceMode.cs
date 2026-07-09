namespace LinguaCoach.Domain.Enums;

/// <summary>Phase H5 — how a <see cref="Entities.ModuleDefinition"/>'s initial draft came to
/// exist. Mirrors <see cref="ActivitySourceMode"/>'s shape.</summary>
public enum ModuleSourceMode
{
    Manual = 0,
    GeneratedFromLearnAndActivities = 1,
    GeneratedFromResources = 2,
    Imported = 3
}
