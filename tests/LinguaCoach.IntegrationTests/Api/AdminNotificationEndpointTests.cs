using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 10W-5A admin notification center endpoints.
/// GET /api/admin/notifications
/// GET /api/admin/notifications/outbox
/// POST /api/admin/notifications/outbox/{id}/retry
/// POST /api/admin/notifications/outbox/{id}/cancel
/// </summary>
public sealed class AdminNotificationEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminNotificationEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── Auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ListNotifications_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListNotifications_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"nf403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/admin/notifications");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListOutbox_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/notifications/outbox");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListOutbox_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"ob403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/admin/notifications/outbox");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Notification list ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListNotifications_AsAdmin_ReturnsPagedResponse()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out _));
        Assert.True(body.TryGetProperty("totalCount", out _));
        Assert.True(body.TryGetProperty("page", out _));
        Assert.True(body.TryGetProperty("pageSize", out _));
    }

    [Fact]
    public async Task ListNotifications_WithChannelFilter_FiltersResults()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"nfch_{Guid.NewGuid():N}@t.com");
        await SeedNotificationAsync(userId, NotificationChannel.Email);
        await SeedNotificationAsync(userId, NotificationChannel.InApp);

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync($"/api/admin/notifications?recipientUserId={userId}&channel=Email");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        // All returned items must be Email channel
        foreach (var item in items.EnumerateArray())
            Assert.Equal("Email", item.GetProperty("channel").GetString());
    }

    [Fact]
    public async Task ListNotifications_WithStatusFilter_FiltersResults()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"nfst_{Guid.NewGuid():N}@t.com");
        await SeedNotificationAsync(userId, NotificationChannel.InApp, NotificationStatus.Queued);

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync($"/api/admin/notifications?recipientUserId={userId}&status=Queued");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        foreach (var item in items.EnumerateArray())
            Assert.Equal("Queued", item.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ListNotifications_SearchOverTitle_FiltersResults()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"nfsr_{Guid.NewGuid():N}@t.com");
        var uniqueTitle = $"UniqueSearchTitle_{Guid.NewGuid():N}";
        await SeedNotificationAsync(userId, NotificationChannel.InApp, title: uniqueTitle);

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync($"/api/admin/notifications?search={uniqueTitle}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task ListNotifications_DoesNotExposeRawTokenOrPassword()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/notifications");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resetToken", body, StringComparison.OrdinalIgnoreCase);
    }

    // ── Outbox list ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ListOutbox_AsAdmin_ReturnsPagedResponse()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/notifications/outbox");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out _));
        Assert.True(body.TryGetProperty("totalCount", out _));
    }

    [Fact]
    public async Task ListOutbox_WithChannelFilter_FiltersResults()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"obch_{Guid.NewGuid():N}@t.com");
        await SeedOutboxItemAsync(userId, NotificationChannel.Email);

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync($"/api/admin/notifications/outbox?recipientUserId={userId}&channel=Email");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        foreach (var item in items.EnumerateArray())
            Assert.Equal("Email", item.GetProperty("channel").GetString());
    }

    [Fact]
    public async Task ListOutbox_FailedOnly_ReturnsOnlyFailed()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"obfo_{Guid.NewGuid():N}@t.com");
        var outboxId = await SeedOutboxItemAsync(userId, NotificationChannel.Email, failed: true);

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync($"/api/admin/notifications/outbox?recipientUserId={userId}&failedOnly=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        foreach (var item in items.EnumerateArray())
            Assert.Equal("Failed", item.GetProperty("status").GetString());
    }

    // ── Retry ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RetryOutboxItem_FailedItem_ReturnsNoContent_AndResetsStatus()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"retry_{Guid.NewGuid():N}@t.com");
        var outboxId = await SeedOutboxItemAsync(userId, NotificationChannel.Email, failed: true);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/notifications/outbox/{outboxId}/retry", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var item = await db.NotificationOutboxItems.FindAsync(outboxId);
        Assert.NotNull(item);
        Assert.Equal(NotificationStatus.Queued, item!.Status);
        Assert.True(item.NextAttemptAtUtc <= DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task RetryOutboxItem_DeliveredItem_ReturnsBadRequest()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"retryd_{Guid.NewGuid():N}@t.com");
        var outboxId = await SeedOutboxItemAsync(userId, NotificationChannel.Email, delivered: true);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/notifications/outbox/{outboxId}/retry", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RetryOutboxItem_UnknownId_ReturnsBadRequest()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/notifications/outbox/{Guid.NewGuid()}/retry", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelOutboxItem_QueuedItem_ReturnsNoContent_AndArchivesItem()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"cancel_{Guid.NewGuid():N}@t.com");
        var outboxId = await SeedOutboxItemAsync(userId, NotificationChannel.Email);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/notifications/outbox/{outboxId}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var item = await db.NotificationOutboxItems.FindAsync(outboxId);
        Assert.Equal(NotificationStatus.Archived, item!.Status);
    }

    [Fact]
    public async Task CancelOutboxItem_DeliveredItem_ReturnsBadRequest()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"canceld_{Guid.NewGuid():N}@t.com");
        var outboxId = await SeedOutboxItemAsync(userId, NotificationChannel.Email, delivered: true);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/notifications/outbox/{outboxId}/cancel", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task SeedNotificationAsync(
        Guid userId,
        NotificationChannel channel,
        NotificationStatus status = NotificationStatus.Queued,
        string? title = null)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var notifSvc = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notifSvc.QueueEmailAsync(
            userId,
            title ?? "Test notification",
            "Body text",
            NotificationCategory.System,
            NotificationSeverity.Info);
    }

    private async Task<Guid> SeedOutboxItemAsync(
        Guid userId,
        NotificationChannel channel,
        bool failed = false,
        bool delivered = false)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var item = LinguaCoach.Domain.Entities.NotificationOutboxItem.Create(
            userId, channel, "{\"title\":\"test\",\"body\":\"body\"}");

        if (failed)
            item.RecordAttempt(success: false, error: "SMTP timeout");
        else if (delivered)
            item.RecordAttempt(success: true);

        db.NotificationOutboxItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }
}
