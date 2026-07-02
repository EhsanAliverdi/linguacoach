namespace LinguaCoach.Application.Admin.RuntimeSettings;

/// <summary>
/// Static, code-defined metadata for a single settable field within a feature-gate group.
/// Never holds a live value — effective values are resolved at request time by
/// IRuntimeSettingsService.
/// </summary>
public sealed record FeatureGateSettingDefinition
{
    /// <summary>Stable key, e.g. "ReadinessPool.EnableReviewScaffoldGeneration".</summary>
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required FeatureGateDataType DataType { get; init; }
    public required string DefaultValueJson { get; init; }
    public bool IsEditableAtRuntime { get; init; }

    /// <summary>
    /// When true, a change to this setting is consumed by a real running service/job on its
    /// next run/request — no redeploy or restart needed (Phase 20C). When false, the value is
    /// stored and displayed but no code path currently reads it (e.g. some lesson-generation
    /// fields have no consuming job yet); editing it has no observable effect.
    /// </summary>
    public bool IsRuntimeEffective { get; init; } = true;

    public FeatureGateRiskLevel RiskLevel { get; init; } = FeatureGateRiskLevel.Low;
    public bool RequiresConfirmation { get; init; }
    public double? MinValue { get; init; }
    public double? MaxValue { get; init; }
    public int? MaxLength { get; init; }
    public IReadOnlyList<string>? AllowedValues { get; init; }
}

/// <summary>
/// Static, code-defined metadata for a feature-gate group: the gate itself plus any
/// related settings edited together in the same admin drawer.
/// </summary>
public sealed record FeatureGateGroupDefinition
{
    /// <summary>Stable kebab-case key used in the URL, e.g. "practice-gym-review-scaffold-pilot".</summary>
    public required string GroupKey { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required FeatureGateCategory Category { get; init; }
    public required FeatureGateBackingStore BackingStore { get; init; }

    /// <summary>When true, no field in this group is editable this phase regardless of per-setting flags.</summary>
    public bool IsReadOnly { get; init; }
    public bool RequiresRestart { get; init; }
    public bool ProductionChangeAllowed { get; init; } = true;
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public string? WarningText { get; init; }
    public required IReadOnlyList<FeatureGateSettingDefinition> Settings { get; init; }
}
