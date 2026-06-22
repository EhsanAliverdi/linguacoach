using LinguaCoach.Application.Notifications;
using LinguaCoach.Infrastructure.Notifications;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Notifications;

/// <summary>
/// Resolves effective notification channel configuration at runtime.
/// DB row wins over appsettings when present. App never crashes if DB is unavailable.
/// Decrypted secrets are only available inside the Infrastructure layer — never returned to API callers.
/// </summary>
public sealed class NotificationChannelConfigResolver : INotificationChannelConfigResolver
{
    private readonly LinguaCoachDbContext _db;
    private readonly EmailOptions _emailOptions;
    private readonly SmsOptions _smsOptions;
    private readonly ISecretProtector _secretProtector;
    private readonly ILogger<NotificationChannelConfigResolver> _logger;

    public NotificationChannelConfigResolver(
        LinguaCoachDbContext db,
        IOptions<EmailOptions> emailOptions,
        IOptions<SmsOptions> smsOptions,
        ISecretProtector secretProtector,
        ILogger<NotificationChannelConfigResolver> logger)
    {
        _db = db;
        _emailOptions = emailOptions.Value;
        _smsOptions = smsOptions.Value;
        _secretProtector = secretProtector;
        _logger = logger;
    }

    public async Task<ResolvedEmailConfig> ResolveEmailAsync(CancellationToken ct = default)
    {
        try
        {
            var dbRow = await _db.NotificationChannelConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Channel == "Email", ct);

            if (dbRow is not null)
            {
                string? secret = null;
                if (dbRow.HasSecret)
                {
                    try { secret = _secretProtector.Unprotect(dbRow.SecretEncrypted); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not unprotect email secret from DB config. Falling back to no credential.");
                    }
                }

                return new ResolvedEmailConfig(
                    IsEnabled: dbRow.IsEnabled,
                    Host: dbRow.Host,
                    Port: dbRow.Port ?? _emailOptions.Port,
                    UseSsl: dbRow.UseSsl ?? _emailOptions.UseSsl,
                    FromAddress: dbRow.FromAddress,
                    FromDisplayName: dbRow.FromDisplayName ?? _emailOptions.FromDisplayName,
                    Username: dbRow.Username,
                    PlaintextSecret: secret,
                    Source: "Database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read DB email config. Falling back to appsettings.");
        }

        // Appsettings fallback
        return new ResolvedEmailConfig(
            IsEnabled: _emailOptions.Enabled,
            Host: _emailOptions.Host,
            Port: _emailOptions.Port,
            UseSsl: _emailOptions.UseSsl,
            FromAddress: _emailOptions.FromAddress,
            FromDisplayName: _emailOptions.FromDisplayName,
            Username: _emailOptions.Username,
            PlaintextSecret: _emailOptions.Password,
            Source: "AppSettings");
    }

    public async Task<ResolvedSmsConfig> ResolveSmsAsync(CancellationToken ct = default)
    {
        try
        {
            var dbRow = await _db.NotificationChannelConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Channel == "Sms", ct);

            if (dbRow is not null)
            {
                string? secret = null;
                if (dbRow.HasSecret)
                {
                    try { secret = _secretProtector.Unprotect(dbRow.SecretEncrypted); }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not unprotect SMS secret from DB config. Falling back to no credential.");
                    }
                }

                return new ResolvedSmsConfig(
                    IsEnabled: dbRow.IsEnabled,
                    Provider: dbRow.Provider,
                    SenderId: dbRow.SenderId,
                    PlaintextSecret: secret,
                    Source: "Database");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read DB SMS config. Falling back to appsettings.");
        }

        return new ResolvedSmsConfig(
            IsEnabled: _smsOptions.Enabled,
            Provider: _smsOptions.Provider,
            SenderId: _smsOptions.SenderId,
            PlaintextSecret: _smsOptions.ApiKey,
            Source: "AppSettings");
    }
}
