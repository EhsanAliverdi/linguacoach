using LinguaCoach.Application.Admin;
using LinguaCoach.Application.Notifications;
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
    private readonly ILogger<AdminNotificationHandler> _logger;

    public AdminNotificationHandler(
        LinguaCoachDbContext db,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions,
        ILogger<AdminNotificationHandler> logger)
    {
        _db = db;
        _userManager = userManager;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
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
            Sms: new AdminChannelStatus("Sms", Enabled: false, StatusLabel: "Deferred"),
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

        if (!_emailOptions.Enabled || string.IsNullOrWhiteSpace(_emailOptions.Host))
        {
            return new AdminTestEmailResult(
                Succeeded: false,
                WasSkipped: true,
                Message: "Email is disabled or not configured. Enable it in appsettings Email section.");
        }

        var message = new EmailMessage(
            ToAddress: toAddress,
            ToDisplayName: toAddress,
            Subject: "SpeakPath — Test Email",
            BodyHtml: "<p>This is a test email from SpeakPath admin. If you received this, email delivery is working.</p>",
            BodyText: "This is a test email from SpeakPath admin. If you received this, email delivery is working.");

        var result = await _emailSender.SendAsync(message, ct);

        _logger.LogInformation(
            "Admin {AdminId} test email to {To}: succeeded={S} skipped={Sk}.",
            adminUserId, toAddress, result.Succeeded, result.WasSkipped);

        return new AdminTestEmailResult(
            Succeeded: result.Succeeded,
            WasSkipped: result.WasSkipped,
            Message: result.Succeeded
                ? $"Test email sent successfully to {toAddress}."
                : result.Error ?? (result.WasSkipped ? "Email sender is disabled." : "Send failed."));
    }

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
