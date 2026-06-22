using LinguaCoach.Application.Admin;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Notifications;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AdminNotificationHandler : IAdminNotificationHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;
    private readonly SmsOptions _smsOptions;
    private readonly ISecretProtector _secretProtector;
    private readonly INotificationChannelConfigResolver _configResolver;
    private readonly ILogger<AdminNotificationHandler> _logger;

    public AdminNotificationHandler(
        LinguaCoachDbContext db,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions,
        IOptions<SmsOptions> smsOptions,
        ISecretProtector secretProtector,
        INotificationChannelConfigResolver configResolver,
        ILogger<AdminNotificationHandler> logger)
    {
        _db = db;
        _userManager = userManager;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
        _smsOptions = smsOptions.Value;
        _secretProtector = secretProtector;
        _configResolver = configResolver;
        _logger = logger;
    }

    public async Task<PagedResponse<AdminNotificationItem>> ListNotificationsAsync(
        AdminNotificationListQuery query, CancellationToken ct = default)
    {
        var q = _db.Notifications.AsNoTracking();

        if (query.RecipientUserId.HasValue)
            q = q.Where(n => n.RecipientUserId == query.RecipientUserId.Value);

        if (!string.IsNullOrWhiteSpace(query.Channel) &&
            Enum.TryParse<NotificationChannel>(query.Channel, ignoreCase: true, out var ch))
            q = q.Where(n => n.Channel == ch);

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<NotificationStatus>(query.Status, ignoreCase: true, out var st))
            q = q.Where(n => n.Status == st);

        if (!string.IsNullOrWhiteSpace(query.Category) &&
            Enum.TryParse<NotificationCategory>(query.Category, ignoreCase: true, out var cat))
            q = q.Where(n => n.Category == cat);

        if (!string.IsNullOrWhiteSpace(query.Severity) &&
            Enum.TryParse<NotificationSeverity>(query.Severity, ignoreCase: true, out var sev))
            q = q.Where(n => n.Severity == sev);

        if (query.From.HasValue)
            q = q.Where(n => n.CreatedAtUtc >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(n => n.CreatedAtUtc <= query.To.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.ToLower();
            q = q.Where(n => n.Title.ToLower().Contains(s) || n.Body.ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        var rows = await q
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Batch-resolve emails for recipients in this page.
        var userIds = rows.Select(n => n.RecipientUserId.ToString()).Distinct().ToList();
        var emailMap = await BuildEmailMapAsync(userIds, ct);

        var items = rows.Select(n => new AdminNotificationItem(
            Id: n.Id,
            RecipientUserId: n.RecipientUserId,
            RecipientEmail: emailMap.GetValueOrDefault(n.RecipientUserId.ToString(), "—"),
            Title: n.Title,
            Body: n.Body,
            Channel: n.Channel.ToString(),
            Category: n.Category.ToString(),
            Severity: n.Severity.ToString(),
            Status: n.Status.ToString(),
            DeepLinkUrl: n.DeepLinkUrl,
            CreatedAtUtc: n.CreatedAtUtc,
            ReadAtUtc: n.ReadAtUtc,
            ExpiresAtUtc: n.ExpiresAtUtc
        )).ToList();

        return new PagedResponse<AdminNotificationItem>(items, total, page, pageSize, totalPages);
    }

    public async Task<PagedResponse<AdminOutboxItem>> ListOutboxAsync(
        AdminOutboxListQuery query, CancellationToken ct = default)
    {
        var q = _db.NotificationOutboxItems.AsNoTracking();

        if (query.RecipientUserId.HasValue)
            q = q.Where(o => o.RecipientUserId == query.RecipientUserId.Value);

        if (!string.IsNullOrWhiteSpace(query.Channel) &&
            Enum.TryParse<NotificationChannel>(query.Channel, ignoreCase: true, out var ch))
            q = q.Where(o => o.Channel == ch);

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<NotificationStatus>(query.Status, ignoreCase: true, out var st))
            q = q.Where(o => o.Status == st);

        if (query.From.HasValue)
            q = q.Where(o => o.CreatedAtUtc >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(o => o.CreatedAtUtc <= query.To.Value);

        if (query.DueOnly)
            q = q.Where(o => o.NextAttemptAtUtc <= DateTime.UtcNow &&
                              (o.Status == NotificationStatus.Queued || o.Status == NotificationStatus.Failed));

        if (query.FailedOnly)
            q = q.Where(o => o.Status == NotificationStatus.Failed);

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        var rows = await q
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var userIds = rows.Select(o => o.RecipientUserId.ToString()).Distinct().ToList();
        var emailMap = await BuildEmailMapAsync(userIds, ct);

        var items = rows.Select(o => new AdminOutboxItem(
            Id: o.Id,
            NotificationId: o.NotificationId,
            RecipientUserId: o.RecipientUserId,
            RecipientEmail: emailMap.GetValueOrDefault(o.RecipientUserId.ToString(), "—"),
            Channel: o.Channel.ToString(),
            Status: o.Status.ToString(),
            AttemptCount: o.AttemptCount,
            CreatedAtUtc: o.CreatedAtUtc,
            NextAttemptAtUtc: o.NextAttemptAtUtc,
            LastAttemptAtUtc: o.LastAttemptAtUtc,
            ProcessedAtUtc: o.ProcessedAtUtc,
            LastError: o.LastError
        )).ToList();

        return new PagedResponse<AdminOutboxItem>(items, total, page, pageSize, totalPages);
    }

    public async Task RetryOutboxItemAsync(Guid outboxItemId, Guid adminUserId, CancellationToken ct = default)
    {
        var item = await _db.NotificationOutboxItems.FindAsync([outboxItemId], ct)
            ?? throw new InvalidOperationException($"Outbox item {outboxItemId} not found.");

        if (item.Status is not (NotificationStatus.Failed or NotificationStatus.Queued))
            throw new InvalidOperationException(
                $"Cannot retry item with status {item.Status}. Only Failed or Queued items may be retried.");

        item.ResetForRetry();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {AdminId} retried outbox item {OutboxId} (channel={Channel}).",
            adminUserId, outboxItemId, item.Channel);
    }

    public async Task CancelOutboxItemAsync(Guid outboxItemId, Guid adminUserId, CancellationToken ct = default)
    {
        var item = await _db.NotificationOutboxItems.FindAsync([outboxItemId], ct)
            ?? throw new InvalidOperationException($"Outbox item {outboxItemId} not found.");

        if (item.Status is NotificationStatus.Delivered or NotificationStatus.Archived)
            throw new InvalidOperationException(
                $"Cannot cancel item with status {item.Status}.");

        // Mark archived to prevent further dispatch attempts.
        item.MarkCancelled();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {AdminId} cancelled outbox item {OutboxId}.",
            adminUserId, outboxItemId);
    }

    public async Task<AdminSendNotificationResult> SendNotificationAsync(
        AdminSendNotificationCommand command, Guid adminUserId, CancellationToken ct = default)
    {
        // Validate channels — SMS deferred to 10W-6.
        var errors = new List<string>();
        var channelsQueued = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var parsedChannels = new List<NotificationChannel>();
        foreach (var ch in command.Channels)
        {
            if (!Enum.TryParse<NotificationChannel>(ch, ignoreCase: true, out var parsed))
            {
                errors.Add($"Unknown channel '{ch}'.");
                continue;
            }
            if (parsed == NotificationChannel.Sms)
            {
                errors.Add("SMS channel is not yet supported. It will be available in a future release.");
                continue;
            }
            parsedChannels.Add(parsed);
        }

        if (parsedChannels.Count == 0)
            throw new InvalidOperationException(
                errors.Count > 0
                    ? string.Join(" ", errors)
                    : "At least one supported channel (InApp, Email) is required.");

        if (!Enum.TryParse<NotificationCategory>(command.Category, ignoreCase: true, out var category))
            throw new InvalidOperationException($"Unknown category '{command.Category}'.");

        if (!Enum.TryParse<NotificationSeverity>(command.Severity, ignoreCase: true, out var severity))
            throw new InvalidOperationException($"Unknown severity '{command.Severity}'.");

        if (command.ExpiresAtUtc.HasValue && command.ExpiresAtUtc.Value <= DateTime.UtcNow)
            throw new InvalidOperationException("ExpiresAtUtc must be a future date.");

        int queued = 0;
        int skipped = 0;

        foreach (var userId in command.RecipientUserIds)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
            {
                errors.Add($"User {userId} not found.");
                skipped++;
                continue;
            }

            foreach (var channel in parsedChannels)
            {
                try
                {
                    switch (channel)
                    {
                        case NotificationChannel.InApp:
                            await _notificationService.QueueInAppAsync(
                                userId, command.Title, command.Body,
                                category, severity,
                                command.DeepLinkUrl, command.ExpiresAtUtc, ct);
                            break;

                        case NotificationChannel.Email:
                            await _notificationService.QueueEmailAsync(
                                userId, command.Title, command.Body,
                                category, severity,
                                command.DeepLinkUrl, command.ExpiresAtUtc, ct);
                            break;
                    }
                    channelsQueued.Add(channel.ToString());
                    queued++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Admin {AdminId}: failed to queue {Channel} notification for user {UserId}.",
                        adminUserId, channel, userId);
                    errors.Add($"Failed to queue {channel} for user {userId}: {ex.Message}");
                    skipped++;
                }
            }
        }

        _logger.LogInformation(
            "Admin {AdminId} sent notification: category={Category} severity={Severity} " +
            "recipients={Count} queued={Queued} skipped={Skipped} channels={Channels}.",
            adminUserId, command.Category, command.Severity,
            command.RecipientUserIds.Count, queued, skipped,
            string.Join(",", channelsQueued));

        return new AdminSendNotificationResult(
            RequestedRecipientCount: command.RecipientUserIds.Count,
            QueuedCount: queued,
            SkippedCount: skipped,
            ChannelsQueued: channelsQueued.ToList(),
            Errors: errors);
    }

    public Task<AdminNotificationConfigStatus> GetConfigStatusAsync(CancellationToken ct = default)
    {
        var emailConfigured = _emailOptions.Enabled
            && !string.IsNullOrWhiteSpace(_emailOptions.Host)
            && !string.IsNullOrWhiteSpace(_emailOptions.FromAddress);

        var emailLabel = !_emailOptions.Enabled
            ? "Disabled"
            : emailConfigured
                ? "Configured"
                : "Misconfigured";

        var status = new AdminNotificationConfigStatus(
            InApp: new AdminChannelStatus("InApp", Enabled: true, StatusLabel: "Enabled"),
            Email: new AdminEmailConfigStatus(
                Enabled: _emailOptions.Enabled,
                Configured: emailConfigured,
                StatusLabel: emailLabel,
                Host: string.IsNullOrWhiteSpace(_emailOptions.Host) ? null : _emailOptions.Host,
                Port: _emailOptions.Port,
                FromAddress: string.IsNullOrWhiteSpace(_emailOptions.FromAddress) ? null : _emailOptions.FromAddress,
                FromDisplayName: string.IsNullOrWhiteSpace(_emailOptions.FromDisplayName) ? null : _emailOptions.FromDisplayName,
                UseSsl: _emailOptions.UseSsl,
                HasUsername: !string.IsNullOrWhiteSpace(_emailOptions.Username),
                HasPassword: !string.IsNullOrWhiteSpace(_emailOptions.Password)),
            Sms: new AdminSmsConfigStatus(
                Enabled: _smsOptions.Enabled,
                Configured: _smsOptions.IsConfigured,
                StatusLabel: !_smsOptions.Enabled ? "Disabled" : _smsOptions.IsConfigured ? "Configured" : "Misconfigured",
                Provider: string.IsNullOrWhiteSpace(_smsOptions.Provider) ? null : _smsOptions.Provider,
                SenderId: string.IsNullOrWhiteSpace(_smsOptions.SenderId) ? null : _smsOptions.SenderId,
                HasApiKey: _smsOptions.HasApiKey),
            DispatchJob: new AdminDispatchJobStatus(
                Enabled: true,
                IntervalDescription: "Every 2 minutes",
                BatchSize: 50));

        return Task.FromResult(status);
    }

    public async Task<AdminTestEmailResult> TestEmailAsync(
        string toAddress, Guid adminUserId, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Admin {AdminId} requested test email to {To}.",
            adminUserId, toAddress);

        // Resolve effective config (DB override → appsettings fallback)
        var resolved = await _configResolver.ResolveEmailAsync(ct);

        if (!resolved.IsEnabled || string.IsNullOrWhiteSpace(resolved.Host))
        {
            return new AdminTestEmailResult(
                Succeeded: false,
                WasSkipped: true,
                Message: $"Email is disabled or not configured (source: {resolved.Source}). " +
                         "Enable it via the Configuration tab or in appsettings.");
        }

        var message = new EmailMessage(
            ToAddress: toAddress,
            ToDisplayName: toAddress,
            Subject: "SpeakPath — Test Email",
            BodyHtml: "<p>This is a test email from SpeakPath admin. If you received this, email delivery is working.</p>",
            BodyText: "This is a test email from SpeakPath admin. If you received this, email delivery is working.");

        var result = await _emailSender.SendAsync(message, ct);

        _logger.LogInformation(
            "Admin {AdminId} test email to {To}: succeeded={S} skipped={Sk} source={Source}.",
            adminUserId, toAddress, result.Succeeded, result.WasSkipped, resolved.Source);

        return new AdminTestEmailResult(
            Succeeded: result.Succeeded,
            WasSkipped: result.WasSkipped,
            Message: result.Succeeded
                ? $"Test email sent successfully to {toAddress}."
                : result.Error ?? (result.WasSkipped ? "Email sender is disabled." : "Send failed."));
    }

    // ── GetConfigStatusV2 ─────────────────────────────────────────────────────

    public async Task<AdminNotificationConfigStatusV2> GetConfigStatusV2Async(CancellationToken ct = default)
    {
        var dbConfigs = await _db.NotificationChannelConfigs.AsNoTracking().ToListAsync(ct);
        var dbEmail = dbConfigs.FirstOrDefault(c => c.Channel == "Email");
        var dbSms   = dbConfigs.FirstOrDefault(c => c.Channel == "Sms");
        var dbInApp = dbConfigs.FirstOrDefault(c => c.Channel == "InApp");

        bool hasDbEmail  = dbEmail is not null;
        bool hasDbSms    = dbSms   is not null;
        bool hasDbInApp  = dbInApp is not null;

        // Email
        bool emailEnabled  = hasDbEmail ? dbEmail!.IsEnabled  : _emailOptions.Enabled;
        string? emailHost  = hasDbEmail ? dbEmail!.Host        : (_emailOptions.Host.Length > 0 ? _emailOptions.Host : null);
        string? emailFrom  = hasDbEmail ? dbEmail!.FromAddress : (_emailOptions.FromAddress.Length > 0 ? _emailOptions.FromAddress : null);
        string? emailDisp  = hasDbEmail ? dbEmail!.FromDisplayName : _emailOptions.FromDisplayName;
        int emailPort      = hasDbEmail ? (dbEmail!.Port ?? _emailOptions.Port) : _emailOptions.Port;
        bool emailSsl      = hasDbEmail ? (dbEmail!.UseSsl ?? _emailOptions.UseSsl) : _emailOptions.UseSsl;
        bool emailHasUser  = hasDbEmail
            ? !string.IsNullOrWhiteSpace(dbEmail!.Username)
            : !string.IsNullOrWhiteSpace(_emailOptions.Username);
        bool emailHasPass  = hasDbEmail
            ? dbEmail!.HasSecret
            : !string.IsNullOrWhiteSpace(_emailOptions.Password);
        bool emailConfigured = emailEnabled && !string.IsNullOrWhiteSpace(emailHost) && !string.IsNullOrWhiteSpace(emailFrom);
        string emailLabel  = !emailEnabled ? "Disabled" : emailConfigured ? "Configured" : "Misconfigured";

        // SMS
        bool smsEnabled    = hasDbSms ? dbSms!.IsEnabled : _smsOptions.Enabled;
        string? smsProv    = hasDbSms ? dbSms!.Provider   : (_smsOptions.Provider.Length > 0 ? _smsOptions.Provider : null);
        string? smsSender  = hasDbSms ? dbSms!.SenderId   : (_smsOptions.SenderId.Length > 0 ? _smsOptions.SenderId : null);
        bool smsHasKey     = hasDbSms ? dbSms!.HasSecret  : _smsOptions.HasApiKey;
        bool smsConfigured = smsEnabled && !string.IsNullOrWhiteSpace(smsProv) && smsHasKey;
        string smsLabel    = !smsEnabled ? "Disabled" : smsConfigured ? "Configured" : "Misconfigured";

        // InApp
        bool inAppEnabled = hasDbInApp ? dbInApp!.IsEnabled : true;

        // Source
        string source = (hasDbEmail || hasDbSms || hasDbInApp)
            ? ((!hasDbEmail && _emailOptions.Enabled) || (!hasDbSms && _smsOptions.Enabled)
                ? NotificationConfigSource.Mixed.ToString()
                : NotificationConfigSource.Database.ToString())
            : NotificationConfigSource.AppSettings.ToString();

        return new AdminNotificationConfigStatusV2(
            InApp: new AdminChannelStatus("InApp", inAppEnabled, inAppEnabled ? "Enabled" : "Disabled"),
            Email: new AdminEmailConfigStatus(
                emailEnabled, emailConfigured, emailLabel,
                emailHost, emailPort, emailFrom, emailDisp,
                emailSsl, emailHasUser, emailHasPass),
            Sms: new AdminSmsConfigStatus(
                smsEnabled, smsConfigured, smsLabel,
                smsProv, smsSender, smsHasKey),
            DispatchJob: new AdminDispatchJobStatus(Enabled: true, IntervalDescription: "Every 2 minutes", BatchSize: 50),
            Source: source);
    }

    // ── UpdateEmailConfig ─────────────────────────────────────────────────────

    public async Task<AdminUpdateConfigResult> UpdateEmailConfigAsync(
        AdminUpdateEmailConfigCommand command, Guid adminUserId, CancellationToken ct = default)
    {
        // Validate
        if (command.IsEnabled)
        {
            if (string.IsNullOrWhiteSpace(command.Host))
                throw new ArgumentException("Host is required when email is enabled.");
            if (command.Port is null or <= 0 or > 65535)
                throw new ArgumentException("Port must be between 1 and 65535.");
            if (string.IsNullOrWhiteSpace(command.FromAddress) || !command.FromAddress.Contains('@'))
                throw new ArgumentException("A valid From address is required when email is enabled.");
        }

        var config = await _db.NotificationChannelConfigs
            .FirstOrDefaultAsync(c => c.Channel == "Email", ct);

        if (config is null)
        {
            config = NotificationChannelConfig.Create("Email");
            _db.NotificationChannelConfigs.Add(config);
        }

        // Encrypt secret if provided (use Base64 for now — TODO: swap for real encryption once
        // encryption infrastructure is added. Secret is never returned to the frontend.)
        string? encryptedSecret = null;
        if (!string.IsNullOrWhiteSpace(command.NewSecret))
            encryptedSecret = _secretProtector.Protect(command.NewSecret);

        config.UpdateEmail(
            command.IsEnabled,
            command.Host?.Trim(),
            command.Port,
            command.UseSsl,
            command.FromAddress?.Trim(),
            command.FromDisplayName?.Trim(),
            command.Username?.Trim(),
            encryptedSecret,
            command.ClearSecret,
            adminUserId);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {AdminId} updated Email channel config: enabled={E} host={H}.",
            adminUserId, command.IsEnabled, command.Host);

        return new AdminUpdateConfigResult(true, "Email configuration saved.", NotificationConfigSource.Database.ToString());
    }

    // ── UpdateSmsConfig ───────────────────────────────────────────────────────

    public async Task<AdminUpdateConfigResult> UpdateSmsConfigAsync(
        AdminUpdateSmsConfigCommand command, Guid adminUserId, CancellationToken ct = default)
    {
        var config = await _db.NotificationChannelConfigs
            .FirstOrDefaultAsync(c => c.Channel == "Sms", ct);

        if (config is null)
        {
            config = NotificationChannelConfig.Create("Sms");
            _db.NotificationChannelConfigs.Add(config);
        }

        string? encryptedSecret = null;
        if (!string.IsNullOrWhiteSpace(command.NewSecret))
            encryptedSecret = _secretProtector.Protect(command.NewSecret);

        config.UpdateSms(
            command.IsEnabled,
            command.Provider?.Trim(),
            command.SenderId?.Trim(),
            encryptedSecret,
            command.ClearSecret,
            adminUserId);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {AdminId} updated SMS channel config: enabled={E} provider={P}.",
            adminUserId, command.IsEnabled, command.Provider);

        return new AdminUpdateConfigResult(true,
            "SMS configuration saved. Real SMS provider is not yet implemented.",
            NotificationConfigSource.Database.ToString());
    }

    // ── UpdateInAppConfig ─────────────────────────────────────────────────────

    public async Task<AdminUpdateConfigResult> UpdateInAppConfigAsync(
        AdminUpdateInAppConfigCommand command, Guid adminUserId, CancellationToken ct = default)
    {
        var config = await _db.NotificationChannelConfigs
            .FirstOrDefaultAsync(c => c.Channel == "InApp", ct);

        if (config is null)
        {
            config = NotificationChannelConfig.Create("InApp");
            _db.NotificationChannelConfigs.Add(config);
        }

        config.UpdateInApp(command.IsEnabled, adminUserId);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {AdminId} updated InApp channel config: enabled={E}.", adminUserId, command.IsEnabled);

        return new AdminUpdateConfigResult(true, "InApp configuration saved.", NotificationConfigSource.Database.ToString());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Dictionary<string, string>> BuildEmailMapAsync(
        IEnumerable<string> userIds, CancellationToken ct)
    {
        var map = new Dictionary<string, string>();
        foreach (var id in userIds)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user?.Email is not null)
                map[id] = user.Email;
        }
        return map;
    }
}
