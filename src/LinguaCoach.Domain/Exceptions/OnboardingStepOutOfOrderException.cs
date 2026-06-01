using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Exceptions;

/// <summary>
/// Raised when a student attempts to complete an onboarding step before its prerequisite.
/// </summary>
public sealed class OnboardingStepOutOfOrderException : DomainException
{
    public OnboardingStep RequestedStep { get; }
    public OnboardingStep ExpectedStep { get; }

    public OnboardingStepOutOfOrderException(OnboardingStep requested, OnboardingStep expected)
        : base($"Cannot complete onboarding step '{requested}' before '{expected}'.")
    {
        RequestedStep = requested;
        ExpectedStep = expected;
    }
}
