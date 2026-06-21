using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for 10W-4B token-based password reset endpoints.
/// Admin: POST /api/admin/students/{id}/send-reset-link
/// Public: POST /api/auth/reset-password
/// </summary>
public sealed class PasswordResetEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public PasswordResetEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── Admin send-reset-link ─────────────────────────────────────────────────

    [Fact]
    public async Task SendResetLink_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/admin/students/{Guid.NewGuid()}/send-reset-link", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SendResetLink_AsStudent_Returns403()
    {
        var (studentToken, _) = await _factory.CreateStudentAndGetTokenAsync($"rl403_{Guid.NewGuid():N}@t.com");
        var client = ClientWithToken(studentToken);
        var response = await client.PostAsync($"/api/admin/students/{Guid.NewGuid()}/send-reset-link", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SendResetLink_AsAdmin_QueuesEmailOutboxItem()
    {
        var email = $"rl_ok_{Guid.NewGuid():N}@t.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var studentProfileId = await GetStudentProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(adminToken);

        var response = await client.PostAsync($"/api/admin/students/{studentProfileId}/send-reset-link", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var outbox = await db.NotificationOutboxItems
            .Where(o => o.RecipientUserId == userId && o.Channel == NotificationChannel.Email)
            .ToListAsync();
        Assert.Single(outbox);
    }

    [Fact]
    public async Task SendResetLink_EmailBodyContainsResetPasswordLink()
    {
        var email = $"rl_body_{Guid.NewGuid():N}@t.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var studentProfileId = await GetStudentProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        await ClientWithToken(adminToken).PostAsync($"/api/admin/students/{studentProfileId}/send-reset-link", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var outbox = await db.NotificationOutboxItems
            .Where(o => o.RecipientUserId == userId && o.Channel == NotificationChannel.Email)
            .FirstAsync();

        Assert.Contains("reset-password", outbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("userId=", outbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token=", outbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendResetLink_TokenNotStoredInNotificationMetadata()
    {
        var email = $"rl_meta_{Guid.NewGuid():N}@t.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var studentProfileId = await GetStudentProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        await ClientWithToken(adminToken).PostAsync($"/api/admin/students/{studentProfileId}/send-reset-link", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == userId)
            .ToListAsync();

        foreach (var n in notifications)
        {
            if (n.MetadataJson is not null)
                Assert.DoesNotContain("resetToken", n.MetadataJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task SendResetLink_UnknownStudent_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(adminToken);
        var response = await client.PostAsync($"/api/admin/students/{Guid.NewGuid()}/send-reset-link", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Public reset-password endpoint ────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest_GenericMessage()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            userId = Guid.NewGuid().ToString(),
            token = "invalid-token",
            newPassword = "NewPass123!",
            confirmPassword = "NewPass123!",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetPassword_MismatchedPasswords_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            userId = Guid.NewGuid().ToString(),
            token = "tok",
            newPassword = "NewPass123!",
            confirmPassword = "DifferentPass!",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_UnknownUserId_ReturnsBadRequest_GenericMessage()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            userId = Guid.NewGuid().ToString(),
            token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("fake-token")),
            newPassword = "NewPass123!",
            confirmPassword = "NewPass123!",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // Must not reveal user-not-found vs bad-token distinction.
        Assert.DoesNotContain("not found", body, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> GetStudentProfileIdAsync(Guid userId)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        return await db.StudentProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .FirstAsync();
    }
}
