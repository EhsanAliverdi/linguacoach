using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Notifications;

public sealed class NotificationDispatchService : INotificationDispatchService
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<NotificationDispatchService> _logger;

    public NotificationDispatchService(LinguaCoachDbContext db, ILogger<NotificationDispatchService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DispatchResult> DispatchDueAsync(int batchSize = 50, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var due = await _db.NotificationOutboxItems
            .Where(o => o.Status == NotificationStatus.Queued)
            .Where(o => o.NextAttemptAtUtc == null || o.NextAttemptAtUtc <= now)
            .OrderBy(o => o.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);

        int processed = 0, skipped = 0, failed = 0;

        foreach (var item in due)
        {
            try
            {
                if (item.Channel == NotificationChannel.InApp)
                {
                    // InApp: notification row already created by NotificationService.
                    // Mark the outbox item delivered; optionally sync the notification status.
                    if (item.NotificationId.HasValue)
                    {
                        var notif = await _db.Notifications
                            .FirstOrDefaultAsync(n => n.Id == item.NotificationId.Value, ct);
                        notif?.MarkDelivered();
                    }

                    item.RecordAttempt(true);
                    processed++;
                }
                else
                {
                    // Email / SMS: external providers not wired yet (10W-4 / 10W-6).
                    // Record as a failed attempt with an informative error so the item
                    // does not stay silently queued. It will retry via backoff until
                    // a provider is registered.
                    item.RecordAttempt(false, $"No provider registered for channel {item.Channel}. Deferred to 10W-4/10W-6.");
                    skipped++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dispatch failed for outbox item {Id}", item.Id);
                item.RecordAttempt(false, ex.Message);
                failed++;
            }
        }

        if (due.Count > 0)
            await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Notification dispatch: processed={Processed} skipped={Skipped} failed={Failed}",
            processed, skipped, failed);

        return new DispatchResult(processed, skipped, failed);
    }
}
