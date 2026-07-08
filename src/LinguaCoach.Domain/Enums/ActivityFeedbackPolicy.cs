namespace LinguaCoach.Domain.Enums;

/// <summary>Effective policy telling the client whether/how to prompt for activity feedback
/// after a submission (Phase B2). Resolved per surface (Today vs Practice Gym) by
/// IActivityFeedbackPolicyProvider.</summary>
public enum ActivityFeedbackPolicy
{
    Off,
    Optional,
    Required,
}
