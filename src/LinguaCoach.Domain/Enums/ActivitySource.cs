namespace LinguaCoach.Domain.Enums;

public enum ActivitySource
{
    AiGenerated = 1,
    SystemFallback = 2,
    /// <summary>Deliberate non-AI step (e.g. lesson reflection form). Not a failed AI call.</summary>
    SystemGenerated = 3
}
