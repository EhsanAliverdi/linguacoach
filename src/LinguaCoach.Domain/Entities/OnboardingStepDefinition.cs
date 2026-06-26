using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

public sealed class OnboardingStepDefinition : BaseEntity
{
    public Guid FlowDefinitionId { get; private set; }
    public OnboardingFlowDefinition? FlowDefinition { get; private set; }

    public string StepKey { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public OnboardingStepTypeV2 StepType { get; private set; }
    public OnboardingStepRequirementType RequirementType { get; private set; }
    public int StepOrder { get; private set; }
    public bool IsEnabled { get; private set; }

    // Serialized JSON metadata — null for steps that don't need them.
    // Serialized JSON metadata — null for steps that don't need them.
    public string? OptionsJson { get; private set; }
    public string? ValidationMetadataJson { get; private set; }
    // Typed mapping enum stored as string (human-readable in DB).
    public OnboardingAnswerMapping AnswerMapping { get; private set; }
    // Server-side only: never returned to student API. Contains correctAnswerKey + cefrScoreWeight.
    public string? AssessmentMetadataJson { get; private set; }

    private OnboardingStepDefinition() { }

    public OnboardingStepDefinition(
        Guid flowDefinitionId,
        string stepKey,
        string title,
        OnboardingStepTypeV2 stepType,
        OnboardingStepRequirementType requirementType,
        int stepOrder,
        bool isEnabled,
        string? description = null,
        string? optionsJson = null,
        string? validationMetadataJson = null,
        OnboardingAnswerMapping answerMapping = OnboardingAnswerMapping.None,
        string? assessmentMetadataJson = null)
    {
        if (flowDefinitionId == Guid.Empty) throw new ArgumentException("FlowDefinitionId required.", nameof(flowDefinitionId));
        if (string.IsNullOrWhiteSpace(stepKey)) throw new ArgumentException("StepKey is required.", nameof(stepKey));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));

        FlowDefinitionId = flowDefinitionId;
        StepKey = stepKey.Trim();
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StepType = stepType;
        RequirementType = requirementType;
        StepOrder = stepOrder;
        IsEnabled = isEnabled;
        OptionsJson = optionsJson;
        ValidationMetadataJson = validationMetadataJson;
        AnswerMapping = answerMapping;
        AssessmentMetadataJson = assessmentMetadataJson;
    }

    public void Update(
        string title,
        string? description,
        OnboardingStepTypeV2 stepType,
        OnboardingStepRequirementType requirementType,
        int stepOrder,
        bool isEnabled,
        string? optionsJson,
        OnboardingAnswerMapping answerMapping)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StepType = stepType;
        RequirementType = requirementType;
        StepOrder = stepOrder;
        IsEnabled = isEnabled;
        OptionsJson = optionsJson;
        AnswerMapping = answerMapping;
    }

    public void SetOrder(int order) => StepOrder = order;
}
