using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

public sealed class UsagePolicy : BaseEntity
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public UsagePolicyScopeType ScopeType { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public ICollection<UsagePolicyRule> Rules { get; private set; } = new List<UsagePolicyRule>();
    public ICollection<StudentPolicyAssignment> StudentAssignments { get; private set; } = new List<StudentPolicyAssignment>();

    private UsagePolicy()
    {
        Name = string.Empty;
    }

    public UsagePolicy(
        string name,
        string? description,
        UsagePolicyScopeType scopeType,
        bool isDefault,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Policy name is required.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
        ScopeType = scopeType;
        IsDefault = isDefault;
        IsActive = isActive;
        UpdatedAt = CreatedAt;
    }

    public void Update(string name, string? description, bool isDefault, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Policy name is required.", nameof(name));
        Name = name.Trim();
        Description = description?.Trim();
        IsDefault = isDefault;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}
