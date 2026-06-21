using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Seeds default notification templates. Idempotent: only inserts if no active template
/// exists for the same TemplateKey + Channel combination. Never overwrites admin edits.
/// </summary>
public static class NotificationTemplateSeeder
{
    public static async Task SeedAsync(
        LinguaCoachDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        var existing = await db.NotificationTemplates
            .Where(t => t.IsActive)
            .Select(t => new { t.TemplateKey, t.Channel })
            .ToListAsync(ct);

        var existingSet = existing
            .Select(x => $"{x.TemplateKey}::{x.Channel}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var defaults = BuildDefaults();
        var added = 0;

        foreach (var template in defaults)
        {
            var key = $"{template.TemplateKey}::{template.Channel}";
            if (existingSet.Contains(key))
                continue;

            db.NotificationTemplates.Add(template);
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "NotificationTemplateSeeder: {Added} default template(s) seeded.",
                added);
        }
    }

    private static IReadOnlyList<NotificationTemplate> BuildDefaults()
    {
        return new[]
        {
            // Password reset email
            NotificationTemplate.Create(
                templateKey: "account.password_reset",
                channel: NotificationChannel.Email,
                name: "Password Reset",
                body: "<p>Hello {{DisplayName}},</p><p>Click the link below to reset your password. This link expires in 24 hours.</p><p><a href=\"{{ResetLink}}\">Reset my password</a></p><p>If you did not request this, please ignore this email.</p><p>— {{AppName}}</p>",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Info,
                subject: "Reset your {{AppName}} password",
                title: null,
                description: "Sent when an admin requests a password reset link for a student.",
                supportedVariablesJson: "[\"DisplayName\",\"ResetLink\",\"AppName\"]"),

            // Student created email
            NotificationTemplate.Create(
                templateKey: "account.student_created",
                channel: NotificationChannel.Email,
                name: "Student Account Created",
                body: "<p>Hello {{DisplayName}},</p><p>Your {{AppName}} account has been created. You can log in and get started at {{LoginUrl}}.</p><p>If you have any questions, contact your administrator.</p><p>— {{AppName}}</p>",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Info,
                subject: "Welcome to {{AppName}}",
                title: null,
                description: "Sent when a new student account is created by an admin.",
                supportedVariablesJson: "[\"DisplayName\",\"LoginUrl\",\"AppName\"]"),

            // Manual admin notification — InApp
            NotificationTemplate.Create(
                templateKey: "admin.manual_notification",
                channel: NotificationChannel.InApp,
                name: "Admin Manual Notification (In-App)",
                body: "{{Body}}",
                category: NotificationCategory.Admin,
                severity: NotificationSeverity.Info,
                subject: null,
                title: "{{Title}}",
                description: "Template for manually sent in-app notifications from the admin panel.",
                supportedVariablesJson: "[\"Title\",\"Body\"]"),

            // Manual admin notification — Email
            NotificationTemplate.Create(
                templateKey: "admin.manual_notification",
                channel: NotificationChannel.Email,
                name: "Admin Manual Notification (Email)",
                body: "<p>{{Body}}</p><p>— {{AppName}} Admin</p>",
                category: NotificationCategory.Admin,
                severity: NotificationSeverity.Info,
                subject: "{{Title}}",
                title: null,
                description: "Template for manually sent email notifications from the admin panel.",
                supportedVariablesJson: "[\"Title\",\"Body\",\"AppName\"]"),
        };
    }
}
