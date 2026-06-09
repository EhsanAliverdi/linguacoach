namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Ordered steps in the student onboarding flow.
/// None = onboarding not yet started.
/// </summary>
public enum OnboardingStep
{
    None = 0,
    Language = 1,
    Preference = 2,  // lesson duration preference (replaces old Track step)
    Career = 3,
    Skill = 4
}
