using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

public sealed class StudentOnboardingProgress : BaseEntity
{
    public Guid UserId { get; private set; }
    public Guid FlowDefinitionId { get; private set; }
    public OnboardingFlowDefinition? FlowDefinition { get; private set; }

    public string? CurrentStepKey { get; private set; }
    public List<string> CompletedStepKeys { get; private set; } = new();
    public int PercentageComplete { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public bool IsComplete { get; private set; }
    public string? PreliminaryCefrLevel { get; private set; }

    private readonly List<StudentOnboardingResponse> _responses = new();
    public IReadOnlyList<StudentOnboardingResponse> Responses => _responses.AsReadOnly();

    private StudentOnboardingProgress() { }

    public StudentOnboardingProgress(Guid userId, Guid flowDefinitionId, string? firstStepKey)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId required.", nameof(userId));
        if (flowDefinitionId == Guid.Empty) throw new ArgumentException("FlowDefinitionId required.", nameof(flowDefinitionId));

        UserId = userId;
        FlowDefinitionId = flowDefinitionId;
        CurrentStepKey = firstStepKey;
        StartedAt = DateTimeOffset.UtcNow;
        IsComplete = false;
        PercentageComplete = 0;
    }

    // Used to initialise v2 progress for students who already completed v1 onboarding.
    public static StudentOnboardingProgress CreateCompleted(Guid userId, Guid flowDefinitionId)
    {
        var p = new StudentOnboardingProgress
        {
            UserId = userId,
            FlowDefinitionId = flowDefinitionId,
            CurrentStepKey = null,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            IsComplete = true,
            PercentageComplete = 100
        };
        return p;
    }

    public void RecordStepCompleted(string stepKey)
    {
        if (!CompletedStepKeys.Contains(stepKey))
            CompletedStepKeys.Add(stepKey);
    }

    public void UpdateCurrentStep(string? nextStepKey) => CurrentStepKey = nextStepKey;

    public void UpdatePercentage(int percentage)
    {
        if (percentage < 0 || percentage > 100)
            throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be 0–100.");
        PercentageComplete = percentage;
    }

    public void Complete(string? preliminaryCefrLevel)
    {
        if (IsComplete)
            throw new InvalidOperationException("Onboarding progress is already complete.");

        PreliminaryCefrLevel = preliminaryCefrLevel;
        IsComplete = true;
        CompletedAt = DateTimeOffset.UtcNow;
        PercentageComplete = 100;
        CurrentStepKey = null;
    }
}
