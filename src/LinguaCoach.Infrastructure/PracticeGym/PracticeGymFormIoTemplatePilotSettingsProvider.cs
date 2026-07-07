using System.Text.Json;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.PracticeGym;

/// <summary>Fails safe: any DB or parse failure resolves to disabled (false), never enabled.</summary>
public sealed class PracticeGymFormIoTemplatePilotSettingsProvider : IPracticeGymFormIoTemplatePilotSettingsProvider
{
    private const string SettingKey = "PracticeGymFormIoPilot.Enabled";

    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<PracticeGymFormIoTemplatePilotSettingsProvider> _logger;

    public PracticeGymFormIoTemplatePilotSettingsProvider(
        LinguaCoachDbContext db,
        ILogger<PracticeGymFormIoTemplatePilotSettingsProvider> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken ct = default)
    {
        try
        {
            var valueJson = await _db.RuntimeSettingOverrides
                .AsNoTracking()
                .Where(o => o.IsActive && o.Key == SettingKey)
                .Select(o => o.ValueJson)
                .FirstOrDefaultAsync(ct);

            return valueJson is not null && JsonSerializer.Deserialize<bool>(valueJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "PracticeGymFormIoTemplatePilotSettingsProvider: could not resolve '{Key}'; defaulting to disabled.",
                SettingKey);
            return false;
        }
    }
}
