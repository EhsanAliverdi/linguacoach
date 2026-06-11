namespace LinguaCoach.Domain.Enums;

/// <summary>Status of a lesson-generation batch for one student.</summary>
public enum GenerationBatchStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Partial = 4
}
