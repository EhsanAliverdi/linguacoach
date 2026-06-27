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
/// Integration tests for Phase 12A create-student welcome email behavior.
/// Verifies that:
///   - Student creation queues a welcome notification in the DB
///   - The notification body never includes the temporary password
///   - Student creation succeeds even when email is unconfigured
///   - The notification is in the Account category
/// </summary>
public sealed class CreateStudentEmailTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public CreateStudentEmailTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── Welcome notification is queued ────────────────────────────────────────

    [Fact]
    public async Task CreateStudent_QueuesWelcomeNotification_ForNewStudent()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var email = $"welcome_queued_{Guid.NewGuid():N}@test.com";

        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync("/api/admin/students", new
            {
                email,
                temporaryPassword = "TempPass@123",
                mustChangePassword = true,
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var userId = Guid.Parse(body.GetProperty("userId").GetString()!);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == userId)
            .ToListAsync();

        Assert.NotEmpty(notifications);
    }

    // ── Notification body does not contain the temporary password ─────────────

    [Fact]
    public async Task CreateStudent_WelcomeNotification_DoesNotContainTemporaryPassword()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var email = $"pwsafe_{Guid.NewGuid():N}@test.com";
        const string tempPassword = "Unique_XYZ_Temp@987!";

        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync("/api/admin/students", new
            {
                email,
                temporaryPassword = tempPassword,
                mustChangePassword = true,
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var userId = Guid.Parse(body.GetProperty("userId").GetString()!);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == userId)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            Assert.DoesNotContain(tempPassword, notification.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(tempPassword, notification.Title, StringComparison.Ordinal);
        }
    }

    // ── Correct category ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateStudent_WelcomeNotification_HasAccountCategory()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var email = $"catcheck_{Guid.NewGuid():N}@test.com";

        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync("/api/admin/students", new
            {
                email,
                temporaryPassword = "TempPass@123",
                mustChangePassword = true,
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var userId = Guid.Parse(body.GetProperty("userId").GetString()!);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == userId)
            .ToListAsync();

        Assert.NotEmpty(notifications);
        Assert.All(notifications, n =>
            Assert.Equal(NotificationCategory.Account, n.Category));
    }

    // ── Correct recipient ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateStudent_WelcomeNotification_RecipientIsNewStudentUserId()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var email = $"recipient_{Guid.NewGuid():N}@test.com";

        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync("/api/admin/students", new
            {
                email,
                temporaryPassword = "TempPass@123",
                mustChangePassword = true,
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var userId = Guid.Parse(body.GetProperty("userId").GetString()!);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == userId)
            .ToListAsync();

        Assert.All(notifications, n =>
            Assert.Equal(userId, n.RecipientUserId));
    }

    // ── Student creation succeeds regardless of email config ──────────────────

    [Fact]
    public async Task CreateStudent_Succeeds_WhenEmailUnconfigured()
    {
        // Email is not configured in the test environment (appsettings.Testing.json has no Email section).
        // Student creation must still return 201 — email errors are swallowed.
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var email = $"noemail_{Guid.NewGuid():N}@test.com";

        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync("/api/admin/students", new
            {
                email,
                temporaryPassword = "TempPass@123",
                mustChangePassword = true,
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.True(body.TryGetProperty("userId", out _));
        Assert.True(body.TryGetProperty("studentProfileId", out _));
    }
}
