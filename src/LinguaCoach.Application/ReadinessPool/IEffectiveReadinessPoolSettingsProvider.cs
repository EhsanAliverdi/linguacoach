namespace LinguaCoach.Application.ReadinessPool;

/// <summary>
/// Resolves the effective <see cref="ReadinessPoolReplenishmentOptions"/> for the current
/// call — appsettings defaults with any active admin `RuntimeSettingOverride` rows applied
/// on top. Never throws: on any resolution failure, falls back to the appsettings snapshot.
/// </summary>
public interface IEffectiveReadinessPoolSettingsProvider
{
    Task<ReadinessPoolReplenishmentOptions> GetEffectiveAsync(CancellationToken ct = default);
}
