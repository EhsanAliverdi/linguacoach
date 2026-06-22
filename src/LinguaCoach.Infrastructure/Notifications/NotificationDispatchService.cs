using System.Text.Json;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Notifications;

public sealed class NotificationDispatchService : INotificationDispatchService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender _smsSender;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<NotificationDispatchService> _logger;

    public NotificationDispatchService(
        LinguaCoachDbContext db,
        IEmailSender emailSender,
        ISmsSender smsSender,
        UserManager<ApplicationUser> userManager,
        ILogger<NotificationDispatchService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _smsSender = smsSender;
        _userManager = userManager;
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
                    if (item.NotificationId.HasValue)
                    {
                        var notif = await _db.Notifications
                            .FirstOrDefaultAsync(n => n.Id == item.NotificationId.Value, ct);
                        notif?.MarkDelivered();
                    }

                    item.RecordAttempt(true);
                    processed++;
                }
                else if (item.Channel == NotificationChannel.Email)
                {
                    var result = await SendEmailAsync(item, ct);
                    if (result.Succeeded)
                    {
                        if (item.NotificationId.HasValue)
                        {
                            var notif = await _db.Notifications
                                .FirstOrDefaultAsync(n => n.Id == item.NotificationId.Value, ct);
                            notif?.MarkDelivered();
                        }
                        item.RecordAttempt(true);
                        processed++;
                    }
                    else if (result.WasSkipped)
                    {
                        item.RecordAttempt(false, result.Error);
                        skipped++;
                    }
                    else
                    {
                        item.RecordAttempt(false, result.Error);
                        failed++;
                    }
                }
                else if (item.Channel == NotificationChannel.Sms)
                {
                    var result = await SendSmsAsync(item, ct);
                    if (result.Succeeded)
                    {
                        if (item.NotificationId.HasValue)
                        {
                            var notif = await _db.Notifications
                                .FirstOrDefaultAsync(n => n.Id == item.NotificationId.Value, ct);
                            notif?.MarkDelivered();
                        }
                        item.RecordAttempt(true);
                        processed++;
                    }
                    else if (result.WasSkipped)
                    {
                        item.RecordAttempt(false, result.Error);
                        skipped++;
                    }
                    else
                    {
                        item.RecordAttempt(false, result.Error);
                        failed++;
                    }
                }
                else
                {
                    item.RecordAttempt(false, $"No provider registered for channel {item.Channel}.");
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

    private async Task<SmsSendResult> SendSmsAsync(
        LinguaCoach.Domain.Entities.NotificationOutboxItem item,
        CancellationToken ct)
    {
        // Resolve recipient phone number from Identity (PhoneNumber field).
        var user = await _userManager.FindByIdAsync(item.RecipientUserId.ToString());
        if (user is null || string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            _logger.LogWarning(
                "SMS dispatch: no phone number found for user {UserId}, outbox item {Id}",
                item.RecipientUserId, item.Id);
            return SmsSendResult.Skipped("Recipient phone number not found.");
        }

        string body = string.Empty;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(item.PayloadJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("body", out var bodyProp))
                body = bodyProp.GetString() ?? body;
            else if (root.TryGetProperty("title", out var titleProp))
                body = titleProp.GetString() ?? body;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse payload JSON for SMS outbox item {Id}", item.Id);
        }

        return await _smsSender.SendAsync(new SmsMessage(user.PhoneNumber, body), ct);
    }

    private async Task<EmailSendResult> SendEmailAsync(
        LinguaCoach.Domain.Entities.NotificationOutboxItem item,
        CancellationToken ct)
    {
        // Resolve recipient email from Identity.
        var user = await _userManager.FindByIdAsync(item.RecipientUserId.ToString());
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning(
                "Email dispatch: no email address found for user {UserId}, outbox item {Id}",
                item.RecipientUserId, item.Id);
            return EmailSendResult.Skipped("Recipient email address not found.");
        }

        // Extract title/body from the payload JSON stored by NotificationService.
        string subject = "SpeakPath notification";
        string bodyHtml = string.Empty;
        string displayName = user.Email;

        try
        {
            using var doc = JsonDocument.Parse(item.PayloadJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("title", out var titleProp))
                subject = titleProp.GetString() ?? subject;
            if (root.TryGetProperty("body", out var bodyProp))
                bodyHtml = bodyProp.GetString() ?? bodyHtml;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse payload JSON for outbox item {Id}", item.Id);
        }

        var message = new EmailMessage(
            ToAddress: user.Email,
            ToDisplayName: displayName,
            Subject: subject,
            BodyHtml: $"<p>{System.Net.WebUtility.HtmlEncode(bodyHtml)}</p>",
            BodyText: bodyHtml);

        return await _emailSender.SendAsync(message, ct);
    }
}
