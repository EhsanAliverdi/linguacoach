namespace LinguaCoach.Domain.Enums;

/// <summary>Which student-facing flow a StudentFlowTemplate/StudentFlowSubmission belongs to.
/// Only Onboarding uses the template model today — placement stays on its own adaptive
/// item-bank model (PlacementItemDefinition), this enum value is reserved for future use.</summary>
public enum StudentFlowKind
{
    Onboarding = 0,
    Placement = 1
}
