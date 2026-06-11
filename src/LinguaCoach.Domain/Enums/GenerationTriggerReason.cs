namespace LinguaCoach.Domain.Enums;

/// <summary>Why a generation batch was created.</summary>
public enum GenerationTriggerReason
{
    PlacementCompleted = 0,
    LessonCompleted = 1,
    ManualAdmin = 2,
    ScheduledRefill = 3
}
