using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Notifications;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Notifications;

public sealed class NotificationApiTests : IClassFixture<NotificationApiTestFactory>, IAsyncLifetime
{
    private readonly NotificationApiTestFactory _factory;
    private HttpClient _client = null!;
    private Guid _userId;
    private string _token = null!;

    public NotificationApiTests(NotificationApiTestFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        (_token, _userId) = await _factory.CreateStudentAndGetTokenAsync("notif-api@test.com");
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> QueueInAppAsync(string title = "Hello", string body = "Body",
        NotificationCategory cat = NotificationCategory.System,
        NotificationSeverity sev = NotificationSeverity.Info,
        DateTime? expiresAtUtc = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var svc = new NotificationService(db, NullLogger<NotificationService>.Instance);
        await svc.QueueInAppAsync(_userId, title, body, cat, sev, expiresAtUtc: expiresAtUtc);
        var notif = db.Notifications.OrderByDescending(n => n.CreatedAtUtc).First();
        return notif.Id;
    }

    private static JsonElement GetJson(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);

    // ── GET /api/notifications ────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsCurrentUserNotifications()
    {
        await QueueInAppAsync("Mine");

        var resp = await _client.GetAsync("/api/notifications");
        resp.EnsureSuccessStatusCode();

        var json = GetJson(await resp.Content.ReadAsStringAsync());
        Assert.True(json.GetProperty("totalCount").GetInt32() >= 1);
        var first = json.GetProperty("items").EnumerateArray().First();
        Assert.Equal("Mine", first.GetProperty("title").GetString());
    }

    [Fact]
    public async Task List_DoesNotReturnOtherUsersNotifications()
    {
        // Queue a notification for a different user directly
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var svc = new NotificationService(db, NullLogger<NotificationService>.Instance);
        await svc.QueueInAppAsync(Guid.NewGuid(), "OtherUser", "Body",
            NotificationCategory.System, NotificationSeverity.Info);

        var resp = await _client.GetAsync("/api/notifications");
        resp.EnsureSuccessStatusCode();

        var json = GetJson(await resp.Content.ReadAsStringAsync());
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(items, i => i.GetProperty("title").GetString() == "OtherUser");
    }

    [Fact]
    public async Task List_PaginationWorks()
    {
        // Queue 3 notifications
        await QueueInAppAsync("P1");
        await QueueInAppAsync("P2");
        await QueueInAppAsync("P3");

        var resp = await _client.GetAsync("/api/notifications?page=1&pageSize=2");
        resp.EnsureSuccessStatusCode();

        var json = GetJson(await resp.Content.ReadAsStringAsync());
        Assert.Equal(2, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, json.GetProperty("page").GetInt32());
        Assert.Equal(2, json.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task List_UnreadOnlyFilter_ExcludesReadNotifications()
    {
        var id = await QueueInAppAsync("UnreadFilter");

        // Mark it read
        var readResp = await _client.PostAsync($"/api/notifications/{id}/read", null);
        readResp.EnsureSuccessStatusCode();

        var resp = await _client.GetAsync("/api/notifications?unreadOnly=true");
        resp.EnsureSuccessStatusCode();

        var json = GetJson(await resp.Content.ReadAsStringAsync());
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(items, i => i.GetProperty("id").GetString() == id.ToString());
    }

    [Fact]
    public async Task List_CategoryFilter_Works()
    {
        await QueueInAppAsync("CatFilter", cat: NotificationCategory.Admin);

        var resp = await _client.GetAsync("/api/notifications?category=Admin");
        resp.EnsureSuccessStatusCode();

        var json = GetJson(await resp.Content.ReadAsStringAsync());
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("title").GetString() == "CatFilter");
    }

    [Fact]
    public async Task List_InvalidCategory_Returns400()
    {
        var resp = await _client.GetAsync("/api/notifications?category=NotACategory");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task List_SeverityFilter_Works()
    {
        await QueueInAppAsync("SevFilter", sev: NotificationSeverity.Error);

        var resp = await _client.GetAsync("/api/notifications?severity=Error");
        resp.EnsureSuccessStatusCode();

        var json = GetJson(await resp.Content.ReadAsStringAsync());
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("title").GetString() == "SevFilter");
    }

    [Fact]
    public async Task List_ExpiredNotifications_Excluded()
    {
        await QueueInAppAsync("Expired", expiresAtUtc: DateTime.UtcNow.AddSeconds(-1));

        var resp = await _client.GetAsync("/api/notifications");
        resp.EnsureSuccessStatusCode();

        var json = GetJson(await resp.Content.ReadAsStringAsync());
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(items, i => i.GetProperty("title").GetString() == "Expired");
    }

    // ── GET /api/notifications/unread-count ───────────────────────────────────

    [Fact]
    public async Task UnreadCount_OnlyCountsCurrentUserAndUnread()
    {
        await QueueInAppAsync("UnreadCount1");
        var readId = await QueueInAppAsync("UnreadCount2");

        // Mark one read
        await _client.PostAsync($"/api/notifications/{readId}/read", null);

        var resp = await _client.GetAsync("/api/notifications/unread-count");
        resp.EnsureSuccessStatusCode();

        var json = GetJson(await resp.Content.ReadAsStringAsync());
        var count = json.GetProperty("unreadCount").GetInt32();
        Assert.True(count >= 1);
    }

    // ── POST /api/notifications/{id}/read ─────────────────────────────────────

    [Fact]
    public async Task MarkRead_SetsReadState()
    {
        var id = await QueueInAppAsync("ReadMe");

        var resp = await _client.PostAsync($"/api/notifications/{id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // Verify via list
        var listResp = await _client.GetAsync("/api/notifications?unreadOnly=false");
        var json = GetJson(await listResp.Content.ReadAsStringAsync());
        var items = json.GetProperty("items").EnumerateArray().ToList();
        var item = items.FirstOrDefault(i => i.GetProperty("id").GetString() == id.ToString());
        Assert.NotNull(item.ValueKind == JsonValueKind.Undefined ? null : (object?)item);
        Assert.NotEqual(JsonValueKind.Null, item.GetProperty("readAtUtc").ValueKind);
    }

    [Fact]
    public async Task MarkRead_AnotherUsersNotification_IsNoOp_NotError()
    {
        // Create a notification for a different user
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var svc = new NotificationService(db, NullLogger<NotificationService>.Instance);
        await svc.QueueInAppAsync(Guid.NewGuid(), "Other", "Body",
            NotificationCategory.System, NotificationSeverity.Info);
        var otherId = db.Notifications.OrderByDescending(n => n.CreatedAtUtc).First().Id;

        // Current user tries to mark it read — should be no-op, not error
        var resp = await _client.PostAsync($"/api/notifications/{otherId}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // Verify the other notification is still unread
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var notif = await db2.Notifications.FindAsync(otherId);
        Assert.Null(notif!.ReadAtUtc);
    }

    // ── POST /api/notifications/read-all ──────────────────────────────────────

    [Fact]
    public async Task MarkAllRead_MarksAllCurrentUserUnread()
    {
        await QueueInAppAsync("All1");
        await QueueInAppAsync("All2");

        var resp = await _client.PostAsync("/api/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var countResp = await _client.GetAsync("/api/notifications/unread-count");
        var json = GetJson(await countResp.Content.ReadAsStringAsync());
        Assert.Equal(0, json.GetProperty("unreadCount").GetInt32());
    }

    // ── POST /api/notifications/{id}/archive ──────────────────────────────────

    [Fact]
    public async Task Archive_HidesNotificationFromDefaultList()
    {
        var id = await QueueInAppAsync("ArchiveMe");

        var resp = await _client.PostAsync($"/api/notifications/{id}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var listResp = await _client.GetAsync("/api/notifications");
        var json = GetJson(await listResp.Content.ReadAsStringAsync());
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(items, i => i.GetProperty("id").GetString() == id.ToString());
    }

    [Fact]
    public async Task Archive_AnotherUsersNotification_IsNoOp()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var svc = new NotificationService(db, NullLogger<NotificationService>.Instance);
        var otherId2 = Guid.NewGuid();
        await svc.QueueInAppAsync(otherId2, "OtherA", "Body",
            NotificationCategory.System, NotificationSeverity.Info);
        var notifId = db.Notifications.OrderByDescending(n => n.CreatedAtUtc).First().Id;

        var resp = await _client.PostAsync($"/api/notifications/{notifId}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // Notification should still be Queued (not Archived)
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var notif = await db2.Notifications.FindAsync(notifId);
        Assert.NotEqual(NotificationStatus.Archived, notif!.Status);
    }

    // ── Unauthenticated ───────────────────────────────────────────────────────

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
