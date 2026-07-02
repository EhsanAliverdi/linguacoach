using System.Text.Json;

namespace LinguaCoach.Application.Admin.RuntimeSettings;

public sealed record FeatureGateSettingValueDto
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required FeatureGateDataType DataType { get; init; }
    public required string EffectiveValueJson { get; init; }
    public required string DefaultValueJson { get; init; }
    public required FeatureGateValueSource ValueSource { get; init; }
    public required bool IsEditableAtRuntime { get; init; }
    public required FeatureGateRiskLevel RiskLevel { get; init; }
    public bool RequiresConfirmation { get; init; }
    public double? MinValue { get; init; }
    public double? MaxValue { get; init; }
    public int? MaxLength { get; init; }
    public IReadOnlyList<string>? AllowedValues { get; init; }
}

public sealed record FeatureGateGroupDto
{
    public required string GroupKey { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required FeatureGateCategory Category { get; init; }
    public required bool IsReadOnly { get; init; }
    public required bool RequiresRestart { get; init; }
    public required bool ProductionChangeAllowed { get; init; }
    public required IReadOnlyList<string> Dependencies { get; init; }
    public string? WarningText { get; init; }
    public required IReadOnlyList<FeatureGateSettingValueDto> Settings { get; init; }
    public string? LastChangedByUserId { get; init; }
    public DateTime? LastChangedAtUtc { get; init; }
    public string? LastChangeReason { get; init; }
    public required bool HasActiveOverride { get; init; }
}

public sealed record UpdateFeatureGateGroupCommand(
    string GroupKey,
    Guid AdminUserId,
    IReadOnlyDictionary<string, JsonElement> Values,
    string Reason,
    string? ConfirmationText);

public sealed record ResetFeatureGateGroupCommand(
    string GroupKey,
    Guid AdminUserId,
    string Reason);
