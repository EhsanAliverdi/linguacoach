namespace LinguaCoach.Domain.Enums;

/// <summary>Phase H3 — how a <see cref="Entities.LearnItem"/>'s initial draft content came to exist.</summary>
public enum LearnItemSourceMode
{
    Manual = 0,
    GeneratedFromResources = 1,
    Imported = 2
}
