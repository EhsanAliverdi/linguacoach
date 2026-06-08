namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Canonical student lifecycle stages.
/// See: docs/architecture/student-lifecycle-reset-tools.md
///
/// Note: there is intentionally no "OnboardingComplete" stage.
/// When onboarding finishes, the stage becomes <see cref="PlacementRequired"/>.
/// </summary>
public enum StudentLifecycleStage
{
    Created = 0,
    PasswordChangeRequired = 1,
    OnboardingRequired = 2,
    OnboardingInProgress = 3,
    PlacementRequired = 4,
    PlacementInProgress = 5,
    PlacementCompleted = 6,
    CourseReady = 7,
    InLesson = 8,
    ActiveLearning = 9,
    Paused = 10,
    Archived = 11
}
