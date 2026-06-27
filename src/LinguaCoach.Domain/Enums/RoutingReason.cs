namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Why a curriculum routing decision was made.
/// normal = exact-level match.
/// review/scaffold/remediation = deliberate lower-level content.
/// fallback = no objective matched.
/// </summary>
public enum RoutingReason
{
    Normal = 0,
    Review = 1,
    Scaffold = 2,
    Remediation = 3,
    Fallback = 4,

    /// <summary>Selected because it matches the student's active learning plan objective.</summary>
    LearningPlan = 5
}
