namespace LinguaCoach.Application.Admin.RuntimeSettings;

/// <summary>
/// Resolves effective values for feature-gate groups (appsettings/defaults + any active
/// DB override) and applies validated, audited admin edits.
/// </summary>
public interface IRuntimeSettingsService
{
    Task<IReadOnlyList<FeatureGateGroupDto>> GetAllAsync(CancellationToken ct);

    Task<FeatureGateGroupDto?> GetByKeyAsync(string groupKey, CancellationToken ct);

    /// <exception cref="KeyNotFoundException">Unknown group key.</exception>
    /// <exception cref="InvalidOperationException">Group or a requested key is read-only/locked.</exception>
    /// <exception cref="ArgumentException">A value fails validation.</exception>
    Task<FeatureGateGroupDto> UpdateAsync(UpdateFeatureGateGroupCommand command, CancellationToken ct);

    /// <exception cref="KeyNotFoundException">Unknown group key.</exception>
    /// <exception cref="InvalidOperationException">Group is read-only/locked.</exception>
    Task<FeatureGateGroupDto> ResetAsync(ResetFeatureGateGroupCommand command, CancellationToken ct);
}
