using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Onboarding;

// ── Step DTOs (student-facing — never include assessment answers or scoring weights) ──

public sealed record OnboardingV2StepDto(
    string StepKey,
    string Title,
    string? Description,
    string StepType,
    string RequirementType,
    int StepOrder,
    bool IsEnabled,
    IReadOnlyList<OnboardingOptionDto>? Options,
    OnboardingValidationMetadataDto? ValidationMetadata
    // AssessmentMetadataJson is intentionally excluded — server-side only.
);

public sealed record OnboardingOptionDto(string Key, string Label);

public sealed record OnboardingValidationMetadataDto(int? MaxLength, int? MaxSelections, int? MinSelections);

// ── Flow query ────────────────────────────────────────────────────────────────

public sealed record GetOnboardingV2Query(Guid UserId);

public sealed record OnboardingV2StatusDto(
    Guid FlowId,
    string? CurrentStepKey,
    IReadOnlyList<OnboardingV2StepDto> Steps,
    IReadOnlyList<string> CompletedStepKeys,
    int PercentageComplete,
    bool IsComplete,
    string? PreliminaryCefrLevel
);

public interface IOnboardingV2Query
{
    Task<OnboardingV2StatusDto> HandleAsync(GetOnboardingV2Query query, CancellationToken ct = default);
}

// ── Step submission ───────────────────────────────────────────────────────────

public sealed record SubmitOnboardingStepCommand(Guid UserId, string StepKey, string AnswerJson);

public sealed record SubmitOnboardingStepResult(
    string? CurrentStepKey,
    IReadOnlyList<string> CompletedStepKeys,
    int PercentageComplete,
    bool IsComplete
);

public interface IOnboardingV2StepHandler
{
    Task<SubmitOnboardingStepResult> HandleAsync(SubmitOnboardingStepCommand command, CancellationToken ct = default);
}

// ── Completion ────────────────────────────────────────────────────────────────

public sealed record CompleteOnboardingV2Command(Guid UserId);

public sealed record CompleteOnboardingV2Result(bool Success, string? PreliminaryCefrLevel);

public interface IOnboardingV2CompleteHandler
{
    Task<CompleteOnboardingV2Result> HandleAsync(CompleteOnboardingV2Command command, CancellationToken ct = default);
}

// ── Admin flow query ──────────────────────────────────────────────────────────

public sealed record GetAdminOnboardingFlowQuery();

public sealed record AdminOnboardingFlowDto(
    Guid FlowId,
    string Name,
    int Version,
    bool IsActive,
    IReadOnlyList<AdminOnboardingStepDto> Steps
);

// Admin view includes RequirementType and AnswerMapping but NOT AssessmentMetadataJson.
public sealed record AdminOnboardingStepDto(
    string StepKey,
    string Title,
    string? Description,
    string StepType,
    string RequirementType,
    string AnswerMapping,
    int StepOrder,
    bool IsEnabled,
    IReadOnlyList<OnboardingOptionDto>? Options
);

public interface IAdminOnboardingFlowQuery
{
    Task<AdminOnboardingFlowDto?> HandleAsync(GetAdminOnboardingFlowQuery query, CancellationToken ct = default);
}

// ── Validation error ──────────────────────────────────────────────────────────

public sealed class OnboardingV2ValidationException : Exception
{
    public OnboardingV2ValidationException(string message) : base(message) { }
}
