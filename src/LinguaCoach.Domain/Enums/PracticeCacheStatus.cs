namespace LinguaCoach.Domain.Enums;

/// <summary>Lifecycle state of a cached Practice Gym activity.</summary>
public enum PracticeCacheStatus
{
    Pending = 0,
    Ready = 1,
    Assigned = 2,
    Completed = 3,
    Expired = 4,
    Failed = 5
}
