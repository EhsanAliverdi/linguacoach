using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

public sealed class FeatureDefinition : BaseEntity
{
    public string Key { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public FeatureCategory Category { get; private set; }
    public EnforcementMode DefaultEnforcementMode { get; private set; }
    public UsageUnitType UnitType { get; private set; }
    public bool IsExpensive { get; private set; }
    public bool IsStudentVisible { get; private set; }
    public bool IsEnabledByDefault { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private FeatureDefinition()
    {
        Key = string.Empty;
        Name = string.Empty;
    }

    public FeatureDefinition(
        string key,
        string name,
        string? description,
        FeatureCategory category,
        EnforcementMode defaultEnforcementMode,
        UsageUnitType unitType,
        bool isExpensive,
        bool isStudentVisible,
        bool isEnabledByDefault)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Feature key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Feature name is required.", nameof(name));

        Key = key.Trim().ToLowerInvariant();
        Name = name.Trim();
        Description = description?.Trim();
        Category = category;
        DefaultEnforcementMode = defaultEnforcementMode;
        UnitType = unitType;
        IsExpensive = isExpensive;
        IsStudentVisible = isStudentVisible;
        IsEnabledByDefault = isEnabledByDefault;
        UpdatedAt = CreatedAt;
    }

    public void Update(
        string name,
        string? description,
        EnforcementMode defaultEnforcementMode,
        bool isExpensive,
        bool isStudentVisible,
        bool isEnabledByDefault)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Feature name is required.", nameof(name));
        Name = name.Trim();
        Description = description?.Trim();
        DefaultEnforcementMode = defaultEnforcementMode;
        IsExpensive = isExpensive;
        IsStudentVisible = isStudentVisible;
        IsEnabledByDefault = isEnabledByDefault;
        UpdatedAt = DateTime.UtcNow;
    }
}
