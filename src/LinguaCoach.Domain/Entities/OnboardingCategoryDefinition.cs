using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Groups onboarding steps into a visual section shown together (e.g. "Welcome", "About you",
/// "Goals"). New in Unified Question-Schema Phase 6b — previously steps were a flat ordered list.
/// </summary>
public sealed class OnboardingCategoryDefinition : BaseEntity
{
    public Guid FlowDefinitionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int CategoryOrder { get; private set; }
    public bool IsEnabled { get; private set; }

    private OnboardingCategoryDefinition() { }

    public OnboardingCategoryDefinition(
        Guid flowDefinitionId, string name, int categoryOrder, bool isEnabled = true, string? description = null)
    {
        if (flowDefinitionId == Guid.Empty) throw new ArgumentException("FlowDefinitionId required.", nameof(flowDefinitionId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

        FlowDefinitionId = flowDefinitionId;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CategoryOrder = categoryOrder;
        IsEnabled = isEnabled;
    }

    public void Update(string name, string? description, int categoryOrder, bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CategoryOrder = categoryOrder;
        IsEnabled = isEnabled;
    }
}
