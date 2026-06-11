namespace LinguaCoach.Domain.Enums;

/// <summary>Status of an individual work item inside a generation batch.</summary>
public enum GenerationJobItemStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Skipped = 4
}
