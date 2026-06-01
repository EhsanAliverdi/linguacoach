namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Ordered steps in the student onboarding flow.
/// None = onboarding not yet started.
/// </summary>
public enum OnboardingStep
{
    None = 0,
    Language = 1,
    Track = 2,
    Career = 3,
    Skill = 4
}
