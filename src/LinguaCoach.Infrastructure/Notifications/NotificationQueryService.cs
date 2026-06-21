using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Notifications;

public sealed class NotificationQueryService : INotificationQueryService
{
    private readonly LinguaCoachDbContext _db;

    public NotificationQueryService(LinguaCoachDbContext db) => _db = db;

    public async Task<PagedNotificationResult> ListAsync(NotificationListQuery query, CancellationToken ct = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(1, query.Page);
        var now = DateTime.UtcNow;

        var q = _db.Notifications
            .Where(n => n.RecipientUserId == query.UserId)
            .Where(n => n.Status != NotificationStatus.Archived)
            .Where(n => n.ExpiresAtUtc == null || n.ExpiresAtUtc > now);

        if (query.UnreadOnly)
            q = q.Where(n => n.ReadAtUtc == null);

        if (query.Category.HasValue)
            q = q.Where(n => n.Category == query.Category.Value);

        if (query.Severity.HasValue)
            q = q.Where(n => n.Severity == query.Severity.Value);

        var totalCount = await q.CountAsync(ct);
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await q
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(
                n.Id, n.Title, n.Body,
                n.Category, n.Severity, n.Channel, n.Status,
                n.CreatedAtUtc, n.ReadAtUtc, n.ExpiresAtUtc,
                n.DeepLinkUrl, n.MetadataJson))
            .ToListAsync(ct);

        return new PagedNotificationResult(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.Notifications
            .Where(n => n.RecipientUserId == userId)
            .Where(n => n.ReadAtUtc == null)
            .Where(n => n.Status != NotificationStatus.Archived)
            .Where(n => n.ExpiresAtUtc == null || n.ExpiresAtUtc > now)
            .CountAsync(ct);
    }

    public async Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notif = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == userId, ct);

        if (notif is null) return;

        notif.MarkRead();
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var unread = await _db.Notifications
            .Where(n => n.RecipientUserId == userId && n.ReadAtUtc == null)
            .ToListAsync(ct);

        foreach (var n in unread)
            n.MarkRead();

        if (unread.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notif = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == userId, ct);

        if (notif is null) return;

        notif.Archive();
        await _db.SaveChangesAsync(ct);
    }
}
