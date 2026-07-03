using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

public sealed class OnboardingFlowDefinition : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<OnboardingStepDefinition> _steps = new();
    public IReadOnlyList<OnboardingStepDefinition> Steps => _steps.AsReadOnly();

    private readonly List<OnboardingCategoryDefinition> _categories = new();
    public IReadOnlyList<OnboardingCategoryDefinition> Categories => _categories.AsReadOnly();

    private OnboardingFlowDefinition() { }

    public OnboardingFlowDefinition(string name, int version)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Flow name is required.", nameof(name));
        if (version <= 0) throw new ArgumentException("Version must be positive.", nameof(version));

        Name = name.Trim();
        Version = version;
        IsActive = false;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    public void RemoveStep(string stepKey)
    {
        var step = _steps.FirstOrDefault(s => s.StepKey == stepKey)
            ?? throw new InvalidOperationException($"Step '{stepKey}' not found in flow '{Name}'.");
        _steps.Remove(step);
    }

    public void ReorderSteps(IReadOnlyList<string> stepKeyOrder)
    {
        for (var i = 0; i < stepKeyOrder.Count; i++)
        {
            var step = _steps.FirstOrDefault(s => s.StepKey == stepKeyOrder[i])
                ?? throw new InvalidOperationException($"Step '{stepKeyOrder[i]}' not found in flow '{Name}'.");
            step.SetOrder(i + 1);
        }
    }

    public void AddStep(OnboardingStepDefinition step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (_steps.Any(s => s.StepKey == step.StepKey))
            throw new InvalidOperationException($"Duplicate step key '{step.StepKey}' in flow '{Name}'.");
        _steps.Add(step);
    }

    public void AddCategory(OnboardingCategoryDefinition category)
    {
        ArgumentNullException.ThrowIfNull(category);
        _categories.Add(category);
    }
}
