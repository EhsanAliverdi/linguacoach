using System.Text.Json;
using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Questions;

namespace LinguaCoach.Domain.Entities;

public sealed class OnboardingStepDefinition : BaseEntity
{
    public Guid FlowDefinitionId { get; private set; }
    public OnboardingFlowDefinition? FlowDefinition { get; private set; }

    /// <summary>Which category (Unified Question-Schema Phase 6b) this step belongs to — null for
    /// steps created before categories existed (historical/superseded flow versions only).</summary>
    public Guid? CategoryId { get; private set; }

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

    /// <summary>Unified Question-Schema (Phase 5) snapshot of this step's content, derived from
    /// OptionsJson/ValidationMetadataJson/AssessmentMetadataJson for the generic step types
    /// (SingleChoice, MultipleChoice, FreeText, AssessmentQuestion) — null for the semantically-named
    /// one-off types (SupportLanguage, LearningGoals, etc.) and non-question steps (Welcome, Summary),
    /// which keep their own dedicated orchestration.</summary>
    public string? ContentJson { get; private set; }

    public QuestionContent? Content => QuestionContentJson.TryDeserializeContent(ContentJson);

    public void SetContent(QuestionContent? content) =>
        ContentJson = content is null ? null : JsonSerializer.Serialize<QuestionContent>(content);

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
        string? assessmentMetadataJson = null,
        Guid? categoryId = null)
    {
        if (flowDefinitionId == Guid.Empty) throw new ArgumentException("FlowDefinitionId required.", nameof(flowDefinitionId));
        if (string.IsNullOrWhiteSpace(stepKey)) throw new ArgumentException("StepKey is required.", nameof(stepKey));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));

        FlowDefinitionId = flowDefinitionId;
        CategoryId = categoryId;
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
        OnboardingAnswerMapping answerMapping,
        Guid? categoryId = null)
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
        if (categoryId is not null) CategoryId = categoryId;
    }

    public void SetOrder(int order) => StepOrder = order;
}
