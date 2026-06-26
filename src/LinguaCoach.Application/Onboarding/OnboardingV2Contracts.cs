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

// ── Admin: list all flows ─────────────────────────────────────────────────────

public sealed record ListAdminOnboardingFlowsQuery();

public sealed record AdminOnboardingFlowSummaryDto(
    Guid FlowId,
    string Name,
    int Version,
    bool IsActive,
    int TotalSteps,
    int RequiredSteps,
    DateTimeOffset CreatedAt
);

public interface IAdminOnboardingFlowListQuery
{
    Task<IReadOnlyList<AdminOnboardingFlowSummaryDto>> HandleAsync(ListAdminOnboardingFlowsQuery query, CancellationToken ct = default);
}

// ── Admin: create flow ────────────────────────────────────────────────────────

public sealed record CreateOnboardingFlowCommand(string Name, int Version);

public interface IAdminCreateOnboardingFlowHandler
{
    Task<AdminOnboardingFlowDto> HandleAsync(CreateOnboardingFlowCommand command, CancellationToken ct = default);
}

// ── Admin: activate flow ──────────────────────────────────────────────────────

public sealed record ActivateOnboardingFlowCommand(Guid FlowId);

public interface IAdminActivateOnboardingFlowHandler
{
    Task HandleAsync(ActivateOnboardingFlowCommand command, CancellationToken ct = default);
}

// ── Admin: add step ───────────────────────────────────────────────────────────

public sealed record AddOnboardingStepCommand(
    Guid FlowId,
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

public interface IAdminAddOnboardingStepHandler
{
    Task<AdminOnboardingStepDto> HandleAsync(AddOnboardingStepCommand command, CancellationToken ct = default);
}

// ── Admin: update step ────────────────────────────────────────────────────────

public sealed record UpdateOnboardingStepCommand(
    Guid FlowId,
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

public interface IAdminUpdateOnboardingStepHandler
{
    Task<AdminOnboardingStepDto> HandleAsync(UpdateOnboardingStepCommand command, CancellationToken ct = default);
}

// ── Admin: remove step ────────────────────────────────────────────────────────

public sealed record RemoveOnboardingStepCommand(Guid FlowId, string StepKey);

public interface IAdminRemoveOnboardingStepHandler
{
    Task HandleAsync(RemoveOnboardingStepCommand command, CancellationToken ct = default);
}

// ── Admin: reorder steps ──────────────────────────────────────────────────────

public sealed record ReorderOnboardingStepsCommand(Guid FlowId, IReadOnlyList<string> StepKeyOrder);

public interface IAdminReorderOnboardingStepsHandler
{
    Task HandleAsync(ReorderOnboardingStepsCommand command, CancellationToken ct = default);
}

// ── Reserved step key guard ───────────────────────────────────────────────────

public static class OnboardingStepKeyGuard
{
    // These keywords are used as URL path segments in the admin onboarding API.
    // Allowing them as step keys would cause routing ambiguity.
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "reorder", "activate", "steps", "flow", "flows", "new", "edit",
        "delete", "remove", "create", "update", "list", "index"
    };

    public static bool IsReserved(string key) => Reserved.Contains(key);

    public static void Validate(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new OnboardingV2ValidationException("Step key is required.");
        if (IsReserved(key))
            throw new OnboardingV2ValidationException(
                $"'{key}' is a reserved word and cannot be used as a step key.");
    }
}

// ── Validation error ──────────────────────────────────────────────────────────

public sealed class OnboardingV2ValidationException : Exception
{
    public OnboardingV2ValidationException(string message) : base(message) { }
}
