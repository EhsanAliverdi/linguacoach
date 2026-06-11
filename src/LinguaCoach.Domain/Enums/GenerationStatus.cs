namespace LinguaCoach.Domain.Enums;

/// <summary>Generation lifecycle state for background-generated assets and sessions.</summary>
public enum GenerationStatus
{
    Pending = 0,
    Ready = 1,
    Failed = 2
}
