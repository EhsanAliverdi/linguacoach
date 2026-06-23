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

            // ── Security notifications (Phase 10Auth-F-3) ──────────────────────

            // Password changed — In-App
            NotificationTemplate.Create(
                templateKey: "account.password_changed",
                channel: NotificationChannel.InApp,
                name: "Password Changed (In-App)",
                body: "Your password was changed successfully. If you did not make this change, contact your administrator immediately.",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                subject: null,
                title: "Password changed",
                description: "Sent in-app when a user successfully changes their password.",
                supportedVariablesJson: "[]"),

            // Password changed — Email
            NotificationTemplate.Create(
                templateKey: "account.password_changed",
                channel: NotificationChannel.Email,
                name: "Password Changed (Email)",
                body: "<p>Hello {{DisplayName}},</p><p>Your {{AppName}} password was changed successfully.</p><p>If you did not make this change, please contact your administrator immediately.</p><p>— {{AppName}}</p>",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                subject: "Your {{AppName}} password was changed",
                title: null,
                description: "Sent by email when a user successfully changes their password.",
                supportedVariablesJson: "[\"DisplayName\",\"AppName\"]"),

            // Password reset succeeded — In-App
            NotificationTemplate.Create(
                templateKey: "account.password_reset_succeeded",
                channel: NotificationChannel.InApp,
                name: "Password Reset Succeeded (In-App)",
                body: "Your password was reset successfully. If you did not do this, contact your administrator immediately.",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                subject: null,
                title: "Password reset successful",
                description: "Sent in-app after a password reset completes successfully.",
                supportedVariablesJson: "[]"),

            // Password reset succeeded — Email
            NotificationTemplate.Create(
                templateKey: "account.password_reset_succeeded",
                channel: NotificationChannel.Email,
                name: "Password Reset Succeeded (Email)",
                body: "<p>Hello {{DisplayName}},</p><p>Your {{AppName}} password was reset successfully.</p><p>If you did not request this reset, please contact your administrator immediately.</p><p>— {{AppName}}</p>",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                subject: "Your {{AppName}} password was reset",
                title: null,
                description: "Sent by email after a password reset completes successfully.",
                supportedVariablesJson: "[\"DisplayName\",\"AppName\"]"),

            // Password reset requested — In-App only
            // The reset-link email already serves as the email notification for this event.
            NotificationTemplate.Create(
                templateKey: "account.password_reset_requested",
                channel: NotificationChannel.InApp,
                name: "Password Reset Requested (In-App)",
                body: "A password reset was requested for your account. If this was not you, contact your administrator.",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                subject: null,
                title: "Password reset requested",
                description: "Sent in-app when an admin triggers a password reset for this user. No email variant — the reset-link email serves that purpose.",
                supportedVariablesJson: "[]"),

            // Account locked — In-App
            NotificationTemplate.Create(
                templateKey: "account.locked_out",
                channel: NotificationChannel.InApp,
                name: "Account Locked (In-App)",
                body: "Your account has been temporarily locked due to repeated unsuccessful login attempts. It will unlock automatically. Contact your administrator if you need immediate access.",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                subject: null,
                title: "Account temporarily locked",
                description: "Sent in-app when an account is locked out due to too many failed login attempts.",
                supportedVariablesJson: "[]"),

            // Account locked — Email
            NotificationTemplate.Create(
                templateKey: "account.locked_out",
                channel: NotificationChannel.Email,
                name: "Account Locked (Email)",
                body: "<p>Hello {{DisplayName}},</p><p>Your {{AppName}} account has been temporarily locked due to repeated unsuccessful login attempts.</p><p>Your account will unlock automatically after a short period. If you need immediate access, please contact your administrator.</p><p>If you did not attempt to log in, your account credentials may be at risk — contact your administrator immediately.</p><p>— {{AppName}}</p>",
                category: NotificationCategory.Account,
                severity: NotificationSeverity.Warning,
                subject: "Your {{AppName}} account has been temporarily locked",
                title: null,
                description: "Sent by email when an account is locked out due to too many failed login attempts.",
                supportedVariablesJson: "[\"DisplayName\",\"AppName\"]"),
        };
    }
}
