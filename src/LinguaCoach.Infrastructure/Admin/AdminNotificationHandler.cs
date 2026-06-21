using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AdminNotificationHandler : IAdminNotificationHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminNotificationHandler> _logger;

    public AdminNotificationHandler(
        LinguaCoachDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<AdminNotificationHandler> logger)
    {
        _db = db;
        _userManager = userManager;
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
