namespace LinguaCoach.Domain.Enums;

/// <summary>The type of work tracked by a GenerationJobItem inside a batch.</summary>
public enum GenerationJobItemType
{
    SessionPlan = 0,
    Session = 1,
    Activity = 2,
    TtsAudio = 3
}
