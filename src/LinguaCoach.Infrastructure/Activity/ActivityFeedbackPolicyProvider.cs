using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Resolves the effective ActivityFeedback policy (Off/Optional/Required) per surface
/// (Today lesson vs Practice Gym) — a `RuntimeSettingOverride`-backed pair of settings with no
/// dedicated options class, following the same generic-override shortcut already used by
/// <c>PracticeGymFormIoTemplatePilotSettingsProvider</c>. Defaults to Optional per
/// docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md (Phase B2).
/// Fails safe: any DB or parse failure resolves to the Optional default, never Required.
/// </summary>
public sealed class ActivityFeedbackPolicyProvider : IActivityFeedbackPolicyProvider
{
    private const ActivityFeedbackPolicy DefaultPolicy = ActivityFeedbackPolicy.Optional;

    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<ActivityFeedbackPolicyProvider> _logger;

    public ActivityFeedbackPolicyProvider(
        LinguaCoachDbContext db,
        ILogger<ActivityFeedbackPolicyProvider> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ActivityFeedbackPolicyDto> GetEffectivePolicyAsync(
        ActivityFeedbackSurface surface, CancellationToken ct = default)
    {
        var key = surface switch
        {
            ActivityFeedbackSurface.Today => "ActivityFeedback.TodayPolicy",
            ActivityFeedbackSurface.PracticeGym => "ActivityFeedback.PracticeGymPolicy",
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, null),
        };

        try
        {
            var valueJson = await _db.RuntimeSettingOverrides
                .AsNoTracking()
                .Where(o => o.IsActive && o.Key == key)
                .Select(o => o.ValueJson)
                .FirstOrDefaultAsync(ct);

            if (valueJson is null)
                return new ActivityFeedbackPolicyDto(DefaultPolicy, surface);

            var rawValue = JsonSerializer.Deserialize<string>(valueJson);
            if (rawValue is not null && Enum.TryParse<ActivityFeedbackPolicy>(rawValue, out var parsed))
                return new ActivityFeedbackPolicyDto(parsed, surface);

            _logger.LogWarning(
                "ActivityFeedbackPolicyProvider: unrecognized override value '{Value}' for '{Key}'; defaulting to {Default}.",
                rawValue, key, DefaultPolicy);
            return new ActivityFeedbackPolicyDto(DefaultPolicy, surface);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ActivityFeedbackPolicyProvider: could not resolve '{Key}'; defaulting to {Default}.",
                key, DefaultPolicy);
            return new ActivityFeedbackPolicyDto(DefaultPolicy, surface);
        }
    }
}
