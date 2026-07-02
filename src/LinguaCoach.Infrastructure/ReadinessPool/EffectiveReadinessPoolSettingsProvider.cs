using System.Text.Json;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.ReadinessPool;

/// <summary>
/// Resolves effective ReadinessPool settings by applying active `RuntimeSettingOverride`
/// rows (key prefix "ReadinessPool.") on top of the appsettings snapshot. Mirrors the same
/// key strings and JSON encoding used by <c>RuntimeSettingsService</c>
/// (LinguaCoach.Infrastructure.Admin) — keep the two in sync when adding new keys.
///
/// Fails safe: a DB failure returns the unmodified appsettings snapshot; a single
/// unparsable override value is skipped (falling back to its appsettings value) without
/// affecting any other field.
/// </summary>
public sealed class EffectiveReadinessPoolSettingsProvider : IEffectiveReadinessPoolSettingsProvider
{
    private const string KeyPrefix = "ReadinessPool.";

    private readonly LinguaCoachDbContext _db;
    private readonly ReadinessPoolReplenishmentOptions _defaults;
    private readonly ILogger<EffectiveReadinessPoolSettingsProvider> _logger;

    public EffectiveReadinessPoolSettingsProvider(
        LinguaCoachDbContext db,
        IOptions<ReadinessPoolReplenishmentOptions> options,
        ILogger<EffectiveReadinessPoolSettingsProvider> logger)
    {
        _db = db;
        _defaults = options.Value;
        _logger = logger;
    }

    public async Task<ReadinessPoolReplenishmentOptions> GetEffectiveAsync(CancellationToken ct = default)
    {
        var effective = Clone(_defaults);

        List<(string Key, string ValueJson)> overrides;
        try
        {
            overrides = await _db.RuntimeSettingOverrides
                .AsNoTracking()
                .Where(o => o.IsActive && o.Key.StartsWith(KeyPrefix))
                .Select(o => new ValueTuple<string, string>(o.Key, o.ValueJson))
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "EffectiveReadinessPoolSettingsProvider: could not load runtime setting overrides; falling back to appsettings.");
            return effective;
        }

        foreach (var (key, valueJson) in overrides)
        {
            try
            {
                Apply(effective, key, valueJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "EffectiveReadinessPoolSettingsProvider: invalid override value for '{Key}'; keeping appsettings value.",
                    key);
            }
        }

        return effective;
    }

    private static void Apply(ReadinessPoolReplenishmentOptions target, string key, string valueJson)
    {
        switch (key)
        {
            case "ReadinessPool.EnableReviewScaffoldGeneration":
                target.EnableReviewScaffoldGeneration = JsonSerializer.Deserialize<bool>(valueJson);
                break;
            case "ReadinessPool.DryRunOnly":
                target.DryRunOnly = JsonSerializer.Deserialize<bool>(valueJson);
                break;
            case "ReadinessPool.RequireAdminReview":
                target.RequireAdminReview = JsonSerializer.Deserialize<bool>(valueJson);
                break;
            case "ReadinessPool.MaxScaffoldItemsPerStudentPerDay":
                target.MaxScaffoldItemsPerStudentPerDay = JsonSerializer.Deserialize<int>(valueJson);
                break;
            case "ReadinessPool.ScaffoldAllowedSources":
                target.ScaffoldAllowedSources = JsonSerializer.Deserialize<string[]>(valueJson)
                    ?? target.ScaffoldAllowedSources;
                break;
            case "ReadinessPool.AllowTodayLessonInsertion":
                target.AllowTodayLessonInsertion = JsonSerializer.Deserialize<bool>(valueJson);
                break;
            case "ReadinessPool.MinimumConfidenceForReviewNeed":
                target.MinimumConfidenceForReviewNeed = JsonSerializer.Deserialize<string>(valueJson)
                    ?? target.MinimumConfidenceForReviewNeed;
                break;
            case "ReadinessPool.PracticeGymPilotEnabled":
                target.PracticeGymPilotEnabled = JsonSerializer.Deserialize<bool>(valueJson);
                break;
            case "ReadinessPool.PracticeGymPilotLabel":
                target.PracticeGymPilotLabel = JsonSerializer.Deserialize<string>(valueJson)
                    ?? target.PracticeGymPilotLabel;
                break;
            case "ReadinessPool.PracticeGymPilotReason":
                target.PracticeGymPilotReason = JsonSerializer.Deserialize<string>(valueJson)
                    ?? target.PracticeGymPilotReason;
                break;
            case "ReadinessPool.MaxStudentVisibleScaffoldSuggestions":
                target.MaxStudentVisibleScaffoldSuggestions = JsonSerializer.Deserialize<int>(valueJson);
                break;
            default:
                // Unknown/unwired key — ignore safely rather than throw.
                break;
        }
    }

    private static ReadinessPoolReplenishmentOptions Clone(ReadinessPoolReplenishmentOptions o) => new()
    {
        TodayLessonPoolTargetCount = o.TodayLessonPoolTargetCount,
        PracticeGymPoolTargetCount = o.PracticeGymPoolTargetCount,
        MaxGenerationAttempts = o.MaxGenerationAttempts,
        ReadyItemExpiryDays = o.ReadyItemExpiryDays,
        ReservedItemExpiryHours = o.ReservedItemExpiryHours,
        GeneratingTimeoutMinutes = o.GeneratingTimeoutMinutes,
        FailedRetryDelayMinutes = o.FailedRetryDelayMinutes,
        MaxItemsGeneratedPerRun = o.MaxItemsGeneratedPerRun,
        EnableReviewScaffoldGeneration = o.EnableReviewScaffoldGeneration,
        DryRunOnly = o.DryRunOnly,
        RequireAdminReview = o.RequireAdminReview,
        MaxScaffoldItemsPerStudentPerDay = o.MaxScaffoldItemsPerStudentPerDay,
        ScaffoldAllowedSources = (string[])o.ScaffoldAllowedSources.Clone(),
        AllowTodayLessonInsertion = o.AllowTodayLessonInsertion,
        MinimumConfidenceForReviewNeed = o.MinimumConfidenceForReviewNeed,
        MinimumReadyThreshold = o.MinimumReadyThreshold,
        MaxBufferCount = o.MaxBufferCount,
        PracticeGymPilotEnabled = o.PracticeGymPilotEnabled,
        PracticeGymPilotLabel = o.PracticeGymPilotLabel,
        PracticeGymPilotReason = o.PracticeGymPilotReason,
        MaxStudentVisibleScaffoldSuggestions = o.MaxStudentVisibleScaffoldSuggestions,
    };
}
