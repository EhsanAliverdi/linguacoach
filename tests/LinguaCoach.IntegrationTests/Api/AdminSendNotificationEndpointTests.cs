using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 10W-5B POST /api/admin/notifications/send.
/// </summary>
public sealed class AdminSendNotificationEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminSendNotificationEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── Auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/admin/notifications/send", ValidRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Send_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"snd403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .PostAsJsonAsync("/api/admin/notifications/send", ValidRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_EmptyRecipients_ReturnsBadRequest()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var req = new { recipientUserIds = Array.Empty<Guid>(), channels = new[] { "InApp" }, title = "T", body = "B" };
        var response = await ClientWithToken(adminToken).PostAsJsonAsync("/api/admin/notifications/send", req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Send_MissingTitle_ReturnsBadRequest()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var req = new { recipientUserIds = new[] { Guid.NewGuid() }, channels = new[] { "InApp" }, title = "", body = "B" };
        var response = await ClientWithToken(adminToken).PostAsJsonAsync("/api/admin/notifications/send", req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Send_MissingBody_ReturnsBadRequest()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var req = new { recipientUserIds = new[] { Guid.NewGuid() }, channels = new[] { "InApp" }, title = "T", body = "" };
        var response = await ClientWithToken(adminToken).PostAsJsonAsync("/api/admin/notifications/send", req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Send_EmptyChannels_ReturnsBadRequest()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var req = new { recipientUserIds = new[] { Guid.NewGuid() }, channels = Array.Empty<string>(), title = "T", body = "B" };
        var response = await ClientWithToken(adminToken).PostAsJsonAsync("/api/admin/notifications/send", req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Send_SmsChannel_ReturnsBadRequest()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"sndsms_{Guid.NewGuid():N}@t.com");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var req = ValidRequest(userId, channels: new[] { "Sms" });
        var response = await ClientWithToken(adminToken).PostAsJsonAsync("/api/admin/notifications/send", req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("SMS", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Send_PastExpiry_ReturnsBadRequest()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"sndexp_{Guid.NewGuid():N}@t.com");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var req = new
        {
            recipientUserIds = new[] { userId },
            channels = new[] { "InApp" },
            title = "T", body = "B",
            expiresAtUtc = DateTime.UtcNow.AddHours(-1)
        };
        var response = await ClientWithToken(adminToken).PostAsJsonAsync("/api/admin/notifications/send", req);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── InApp queuing ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_InApp_QueuesNotificationAndOutboxRow()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"sndina_{Guid.NewGuid():N}@t.com");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync("/api/admin/notifications/send", ValidRequest(userId, channels: new[] { "InApp" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, result.GetProperty("queuedCount").GetInt32());
        Assert.Equal(0, result.GetProperty("skippedCount").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var notif = await db.Notifications
            .Where(n => n.RecipientUserId == userId && n.Title == "Admin Test Title")
            .FirstOrDefaultAsync();
        Assert.NotNull(notif);
        Assert.Equal(NotificationChannel.InApp, notif!.Channel);
    }

    // ── Email queuing ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_Email_QueuesOutboxRow_NotSentImmediately()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"sndeml_{Guid.NewGuid():N}@t.com");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync("/api/admin/notifications/send", ValidRequest(userId, channels: new[] { "Email" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, result.GetProperty("queuedCount").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // Email outbox row should be Queued, not Delivered
        var outbox = await db.NotificationOutboxItems
            .Where(o => o.RecipientUserId == userId && o.Channel == NotificationChannel.Email)
            .OrderByDescending(o => o.CreatedAtUtc)
            .FirstOrDefaultAsync();
        Assert.NotNull(outbox);
        Assert.Equal(NotificationStatus.Queued, outbox!.Status);
        Assert.Equal(0, outbox.AttemptCount);
    }

    // ── Both channels ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_BothChannels_QueuesTwoRows()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"sndbth_{Guid.NewGuid():N}@t.com");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync("/api/admin/notifications/send",
                ValidRequest(userId, channels: new[] { "InApp", "Email" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, result.GetProperty("queuedCount").GetInt32());

        var channelsQueued = result.GetProperty("channelsQueued").EnumerateArray()
            .Select(e => e.GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("InApp", channelsQueued);
        Assert.Contains("Email", channelsQueued);
    }

    // ── Multiple recipients ───────────────────────────────────────────────────

    [Fact]
    public async Task Send_MultipleRecipients_QueuesForEach()
    {
        var (_, userId1) = await _factory.CreateStudentAndGetTokenAsync($"sndm1_{Guid.NewGuid():N}@t.com");
        var (_, userId2) = await _factory.CreateStudentAndGetTokenAsync($"sndm2_{Guid.NewGuid():N}@t.com");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var req = new
        {
            recipientUserIds = new[] { userId1, userId2 },
            channels = new[] { "InApp" },
            title = "Admin Test Title",
            body = "Admin test body."
        };
        var response = await ClientWithToken(adminToken).PostAsJsonAsync("/api/admin/notifications/send", req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, result.GetProperty("requestedRecipientCount").GetInt32());
        Assert.Equal(2, result.GetProperty("queuedCount").GetInt32());
    }

    // ── Unknown user ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Send_UnknownRecipient_ReportsSkipped()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var req = ValidRequest(Guid.NewGuid()); // random non-existent user

        var response = await ClientWithToken(adminToken).PostAsJsonAsync("/api/admin/notifications/send", req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, result.GetProperty("queuedCount").GetInt32());
        Assert.Equal(1, result.GetProperty("skippedCount").GetInt32());
        Assert.True(result.GetProperty("errors").GetArrayLength() > 0);
    }

    // ── Response does not expose secrets ─────────────────────────────────────

    [Fact]
    public async Task Send_ResponseDoesNotExposeSecrets()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"sndsec_{Guid.NewGuid():N}@t.com");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync("/api/admin/notifications/send", ValidRequest(userId));

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resetToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object ValidRequest(Guid userId, string[]? channels = null) => new
    {
        recipientUserIds = new[] { userId },
        channels = channels ?? new[] { "InApp" },
        title = "Admin Test Title",
        body = "Admin test body.",
        category = "Admin",
        severity = "Info"
    };
}
