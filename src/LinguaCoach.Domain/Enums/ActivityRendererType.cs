namespace LinguaCoach.Domain.Enums;

/// <summary>Phase H4 — how an <see cref="Entities.ActivityDefinition"/> is rendered to a student
/// once a future Module/Practice layer (H5+) actually delivers it. <c>Formio</c> is the only
/// renderer H4's deterministic composer produces; <c>Custom</c>/<c>Legacy</c> exist so a future
/// phase can register other activity shapes without a schema change.</summary>
public enum ActivityRendererType
{
    Formio = 0,
    Custom = 1,
    Legacy = 2
}
