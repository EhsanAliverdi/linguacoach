namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Status of a student's placement assessment.
/// See: docs/architecture/placement-assessment-model.md
/// </summary>
public enum PlacementStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Abandoned = 3,
    Expired = 4,
    Failed = 5
}
