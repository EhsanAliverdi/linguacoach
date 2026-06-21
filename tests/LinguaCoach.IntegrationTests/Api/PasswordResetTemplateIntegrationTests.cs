using System.Net;
using System.Net.Http.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 10W-5D-RESET-INTEGRATION tests.
/// Verifies that reset-link and student-created emails use notification templates
/// with safe fallback when template is missing or inactive.
/// Security invariants: token never in metadata/audit, API response never exposes token.
/// </summary>
public sealed class PasswordResetTemplateIntegrationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public PasswordResetTemplateIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── Reset-link uses active template ───────────────────────────────────────

    [Fact]
    public async Task SendResetLink_WhenActiveTemplateExists_UsesTemplateSubject()
    {
        // Seed an active account.password_reset/Email template.
        var templateKey = "account.password_reset";
        var customSubject = $"Custom reset subject {Guid.NewGuid():N}";
        await SeedTemplateAsync(templateKey, NotificationChannel.Email,
            subject: customSubject,
            body: "<p>Reset: {{ResetLink}}</p>");

        var email = $"rt_tpl_{Guid.NewGuid():N}@t.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var profileId = await GetProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/students/{profileId}/send-reset-link", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var outbox = await db.NotificationOutboxItems
            .Where(o => o.RecipientUserId == userId && o.Channel == NotificationChannel.Email)
            .OrderByDescending(o => o.CreatedAt)
            .FirstAsync();

        Assert.Contains(customSubject, outbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendResetLink_WhenActiveTemplateExists_BodyContainsResetLink()
    {
        await SeedTemplateAsync("account.password_reset", NotificationChannel.Email,
            subject: "Reset your password",
            body: "<p>Click here: {{ResetLink}}</p><p>From {{AppName}}</p>");

        var email = $"rt_body_{Guid.NewGuid():N}@t.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var profileId = await GetProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/students/{profileId}/send-reset-link", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var outbox = await db.NotificationOutboxItems
            .Where(o => o.RecipientUserId == userId && o.Channel == NotificationChannel.Email)
            .OrderByDescending(o => o.CreatedAt)
            .FirstAsync();

        Assert.Contains("reset-password", outbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("userId=", outbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token=", outbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    // ── Reset-link fallback when template missing ─────────────────────────────

    [Fact]
    public async Task SendResetLink_WhenNoActiveTemplate_FallsBackAndQueuesEmail()
    {
        // Deactivate any existing account.password_reset/Email templates.
        await DeactivateTemplatesAsync("account.password_reset", NotificationChannel.Email);

        var email = $"rt_fallback_{Guid.NewGuid():N}@t.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var profileId = await GetProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/students/{profileId}/send-reset-link", null);

        // Reset still succeeds — fallback content is used.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var outbox = await db.NotificationOutboxItems
            .Where(o => o.RecipientUserId == userId && o.Channel == NotificationChannel.Email)
            .ToListAsync();

        Assert.Single(outbox);
        Assert.Contains("reset-password", outbox[0].PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    // ── Token never stored in metadata ────────────────────────────────────────

    [Fact]
    public async Task SendResetLink_TokenNotStoredInOutboxOrNotificationMetadata()
    {
        var email = $"rt_sec_{Guid.NewGuid():N}@t.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var profileId = await GetProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/students/{profileId}/send-reset-link", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // Notification rows must not contain raw token field.
        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == userId)
            .ToListAsync();
        foreach (var n in notifications)
        {
            if (n.MetadataJson is not null)
                Assert.DoesNotContain("resetToken", n.MetadataJson, StringComparison.OrdinalIgnoreCase);
        }

        // Outbox PayloadJson contains the reset link URL but not a bare token key.
        var outboxItems = await db.NotificationOutboxItems
            .Where(o => o.RecipientUserId == userId)
            .ToListAsync();
        foreach (var item in outboxItems)
        {
            Assert.DoesNotContain("\"resetToken\"", item.PayloadJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"rawToken\"", item.PayloadJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task SendResetLink_ApiResponse_DoesNotContainToken()
    {
        var email = $"rt_apiresp_{Guid.NewGuid():N}@t.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var profileId = await GetProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/students/{profileId}/send-reset-link", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
    }

    // ── Student-created uses active template ──────────────────────────────────

    [Fact]
    public async Task CreateStudent_WhenActiveTemplateExists_UsesTemplateSubject()
    {
        var customSubject = $"Welcome custom {Guid.NewGuid():N}";
        await SeedTemplateAsync("account.student_created", NotificationChannel.Email,
            subject: customSubject,
            body: "<p>Hello {{DisplayName}}, welcome to {{AppName}}. Login at {{LoginUrl}}</p>");

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var uniqueEmail = $"sc_tpl_{Guid.NewGuid():N}@t.com";

        var response = await ClientWithToken(adminToken).PostAsJsonAsync("/api/admin/students", new
        {
            email = uniqueEmail,
            temporaryPassword = "TempPass123!",
            mustChangePassword = true,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == uniqueEmail);

        var outbox = await db.NotificationOutboxItems
            .Where(o => o.RecipientUserId == user.Id && o.Channel == NotificationChannel.Email)
            .FirstOrDefaultAsync();

        Assert.NotNull(outbox);
        Assert.Contains(customSubject, outbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateStudent_WhenNoActiveTemplate_FallsBackAndStudentCreationSucceeds()
    {
        await DeactivateTemplatesAsync("account.student_created", NotificationChannel.Email);

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var uniqueEmail = $"sc_fallback_{Guid.NewGuid():N}@t.com";

        var response = await ClientWithToken(adminToken).PostAsJsonAsync("/api/admin/students", new
        {
            email = uniqueEmail,
            temporaryPassword = "TempPass123!",
            mustChangePassword = true,
        });

        // Student creation must succeed even with no active template.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
        Assert.NotNull(user);
    }

    [Fact]
    public async Task CreateStudent_TemplateWithMissingVariable_StillQueuesEmailSafely()
    {
        // Template uses an undefined variable — should not throw.
        await SeedTemplateAsync("account.student_created", NotificationChannel.Email,
            subject: "Welcome",
            body: "<p>Hello {{DisplayName}}, your code is {{UndefinedVar}}.</p>");

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var uniqueEmail = $"sc_misvar_{Guid.NewGuid():N}@t.com";

        var response = await ClientWithToken(adminToken).PostAsJsonAsync("/api/admin/students", new
        {
            email = uniqueEmail,
            temporaryPassword = "TempPass123!",
            mustChangePassword = true,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == uniqueEmail);

        var outbox = await db.NotificationOutboxItems
            .Where(o => o.RecipientUserId == user.Id && o.Channel == NotificationChannel.Email)
            .FirstOrDefaultAsync();

        // Email still queued; unresolved placeholder left visible in body.
        Assert.NotNull(outbox);
        Assert.Contains("{{UndefinedVar}}", outbox.PayloadJson, StringComparison.Ordinal);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> GetProfileIdAsync(Guid userId)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        return await db.StudentProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .FirstAsync();
    }

    private async Task SeedTemplateAsync(
        string key, NotificationChannel channel, string subject, string body)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // Remove any conflicting active template for this key+channel before inserting.
        var existing = await db.NotificationTemplates
            .Where(t => t.TemplateKey == key && t.Channel == channel && t.IsActive)
            .ToListAsync();
        foreach (var t in existing)
            t.Deactivate();

        var template = LinguaCoach.Domain.Entities.NotificationTemplate.Create(
            templateKey: key,
            channel: channel,
            name: $"Test {key}",
            body: body,
            category: LinguaCoach.Domain.Enums.NotificationCategory.Account,
            severity: LinguaCoach.Domain.Enums.NotificationSeverity.Info,
            subject: channel == NotificationChannel.Email ? subject : null,
            title: channel == NotificationChannel.InApp ? subject : null);

        db.NotificationTemplates.Add(template);
        await db.SaveChangesAsync();
    }

    private async Task DeactivateTemplatesAsync(string key, NotificationChannel channel)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var templates = await db.NotificationTemplates
            .Where(t => t.TemplateKey == key && t.Channel == channel && t.IsActive)
            .ToListAsync();

        foreach (var t in templates)
            t.Deactivate();

        await db.SaveChangesAsync();
    }
}
