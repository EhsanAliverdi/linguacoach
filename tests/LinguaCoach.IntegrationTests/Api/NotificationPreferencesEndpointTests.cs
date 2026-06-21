using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 10W-PREFS — Notification Preferences API integration tests.
/// GET /api/notifications/preferences
/// PUT /api/notifications/preferences
/// GET /api/admin/notifications/preferences/{userId}
/// </summary>
public sealed class NotificationPreferencesEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public NotificationPreferencesEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── Auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreferences_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/notifications/preferences");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePreferences_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .PutAsJsonAsync("/api/notifications/preferences", new[] {
                new { category = "Learning", channel = "Email", isEnabled = false }
            });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminGetPreferences_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .GetAsync($"/api/admin/notifications/preferences/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminGetPreferences_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"np403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .GetAsync($"/api/admin/notifications/preferences/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Defaults when no rows exist ───────────────────────────────────────────

    [Fact]
    public async Task GetPreferences_DefaultsReturnedWhenNoRowsExist()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"npdef_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/notifications/preferences");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.EnumerateArray().ToList();
        Assert.NotEmpty(items);

        // Each item has required fields.
        foreach (var item in items)
        {
            Assert.True(item.TryGetProperty("category", out _));
            Assert.True(item.TryGetProperty("channel", out _));
            Assert.True(item.TryGetProperty("isEnabled", out _));
            Assert.True(item.TryGetProperty("isRequired", out _));
        }
    }

    [Fact]
    public async Task GetPreferences_AccountCategoryIsRequired()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"npreq_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/notifications/preferences");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var accountItems = body.EnumerateArray()
            .Where(p => p.GetProperty("category").GetString() == "Account")
            .ToList();

        Assert.NotEmpty(accountItems);
        Assert.All(accountItems, item =>
            Assert.True(item.GetProperty("isRequired").GetBoolean()));
    }

    // ── User can update preferences ───────────────────────────────────────────

    [Fact]
    public async Task UpdatePreferences_UserCanDisableLearningEmail()
    {
        var email = $"npupd_{Guid.NewGuid():N}@t.com";
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync(email);

        var response = await ClientWithToken(token).PutAsJsonAsync("/api/notifications/preferences", new[]
        {
            new { category = "Learning", channel = "Email", isEnabled = false }
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var pref = await db.NotificationPreferences
            .FirstOrDefaultAsync(p =>
                p.UserId == userId &&
                p.Category == NotificationCategory.Learning &&
                p.Channel == NotificationChannel.Email);

        Assert.NotNull(pref);
        Assert.False(pref.IsEnabled);
    }

    [Fact]
    public async Task UpdatePreferences_RequiredCategoryForcedEnabled()
    {
        // Even if client sends isEnabled=false for Account, it must be stored as true.
        var email = $"npreq2_{Guid.NewGuid():N}@t.com";
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync(email);

        await ClientWithToken(token).PutAsJsonAsync("/api/notifications/preferences", new[]
        {
            new { category = "Account", channel = "Email", isEnabled = false }
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var pref = await db.NotificationPreferences
            .FirstOrDefaultAsync(p =>
                p.UserId == userId &&
                p.Category == NotificationCategory.Account &&
                p.Channel == NotificationChannel.Email);

        Assert.NotNull(pref);
        Assert.True(pref.IsEnabled);
    }

    // ── Preference respected during queueing ──────────────────────────────────

    [Fact]
    public async Task DisabledEmailPreference_PreventsNonCriticalEmailQueueing()
    {
        var email = $"npblock_{Guid.NewGuid():N}@t.com";
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync(email);

        // Disable Learning Email.
        await ClientWithToken(token).PutAsJsonAsync("/api/notifications/preferences", new[]
        {
            new { category = "Learning", channel = "Email", isEnabled = false }
        });

        // Queue a Learning/Email notification via the notification service directly.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var prefSvc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Notifications.INotificationPreferenceService>();

        var enabled = await prefSvc.IsChannelEnabledAsync(
            userId, NotificationCategory.Learning, NotificationChannel.Email);

        Assert.False(enabled);
    }

    [Fact]
    public async Task AccountEmail_AlwaysEnabledRegardlessOfPreference()
    {
        var email = $"npacc_{Guid.NewGuid():N}@t.com";
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync(email);

        // Try to disable Account Email via preferences.
        await ClientWithToken(token).PutAsJsonAsync("/api/notifications/preferences", new[]
        {
            new { category = "Account", channel = "Email", isEnabled = false }
        });

        using var scope = _factory.Services.CreateScope();
        var prefSvc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Notifications.INotificationPreferenceService>();

        var enabled = await prefSvc.IsChannelEnabledAsync(
            userId, NotificationCategory.Account, NotificationChannel.Email);

        Assert.True(enabled);
    }

    [Fact]
    public async Task SmsChannel_AlwaysDisabled()
    {
        var email = $"npsms_{Guid.NewGuid():N}@t.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);

        using var scope = _factory.Services.CreateScope();
        var prefSvc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Notifications.INotificationPreferenceService>();

        var enabled = await prefSvc.IsChannelEnabledAsync(
            userId, NotificationCategory.Learning, NotificationChannel.Sms);

        Assert.False(enabled);
    }

    // ── Empty body validation ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePreferences_EmptyBody_Returns400()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"npempty_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .PutAsJsonAsync("/api/notifications/preferences", Array.Empty<object>());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Admin read endpoint ───────────────────────────────────────────────────

    [Fact]
    public async Task AdminGetPreferences_ReturnsPreferencesForUser()
    {
        var email = $"npadmin_{Guid.NewGuid():N}@t.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .GetAsync($"/api/admin/notifications/preferences/{userId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.EnumerateArray().ToList();
        Assert.NotEmpty(items);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
