namespace LinguaCoach.Domain.Enums;

/// <summary>Phase H4 — how an <see cref="Entities.ActivityDefinition"/>'s initial draft content
/// came to exist. Mirrors <see cref="LearnItemSourceMode"/>'s shape with one addition
/// (<see cref="GeneratedFromLearnItem"/>) since an Activity can also be generated starting from an
/// already-approved-or-draft Learn Item rather than raw Resource Bank rows.</summary>
public enum ActivitySourceMode
{
    Manual = 0,
    GeneratedFromResources = 1,
    GeneratedFromLearnItem = 2,
    Imported = 3
}
