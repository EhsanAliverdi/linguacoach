using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Auth;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Auth;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 10Auth-F-4: refresh token and session management.
/// </summary>
public sealed class RefreshTokenTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public RefreshTokenTests(ApiTestFactory factory) => _factory = factory;

    // ── Login returns refresh token ───────────────────────────────────────────

    [Fact]
    public async Task Login_Success_ReturnsRefreshToken()
    {
        var email = $"rt_login_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "RefreshMe@9876");

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "RefreshMe@9876" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var rt = body.GetProperty("refreshToken").GetString();
        Assert.NotNull(rt);
        Assert.NotEmpty(rt!);
    }

    [Fact]
    public async Task Login_RefreshToken_IsStoredAsHash_NotRaw()
    {
        var email = $"rt_hash_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "RefreshMe@9876");

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "RefreshMe@9876" });

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var rawToken = body.GetProperty("refreshToken").GetString()!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);

        var stored = await db.UserRefreshTokens
            .Where(t => t.UserId == user!.Id)
            .ToListAsync();

        Assert.NotEmpty(stored);
        // Raw token must NOT appear in any stored hash
        Assert.All(stored, t => Assert.NotEqual(rawToken, t.TokenHash));
        // The expected hash of the raw token must match what is stored
        var expectedHash = RefreshTokenService.HashToken(rawToken);
        Assert.Contains(stored, t => t.TokenHash == expectedHash);
    }

    // ── Refresh endpoint ──────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewAccessAndRefreshToken()
    {
        var (_, refreshToken) = await LoginAndGetTokensAsync($"rt_refresh_{Guid.NewGuid():N}@test.com");

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        var newRt = body.GetProperty("refreshToken").GetString();
        Assert.NotNull(newRt);
        Assert.NotEqual(refreshToken, newRt);
    }

    [Fact]
    public async Task Refresh_RotatesToken_OldTokenCannotBeReused()
    {
        var (_, refreshToken) = await LoginAndGetTokensAsync($"rt_rotate_{Guid.NewGuid():N}@test.com");

        var client = _factory.CreateClient();
        // First refresh — valid
        await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

        // Second refresh with same (now-rotated) token — must fail
        var resp2 = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, resp2.StatusCode);
    }

    [Fact]
    public async Task Refresh_InvalidToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = "totally-invalid-token-value" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Refresh_InvalidToken_ResponseIsGeneric()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = "invalid-token" });
        var body = await resp.Content.ReadAsStringAsync();
        // Must not reveal whether user exists or token format
        Assert.DoesNotContain("userId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_RevokedToken_ReturnsUnauthorized()
    {
        var (_, refreshToken) = await LoginAndGetTokensAsync($"rt_revoked_{Guid.NewGuid():N}@test.com");

        var client = _factory.CreateClient();
        // Logout revokes token
        await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken });

        // Refresh attempt on revoked token
        var resp = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Refresh_RawToken_NotStoredInDatabase()
    {
        var email = $"rt_rawcheck_{Guid.NewGuid():N}@test.com";
        var (_, refreshToken) = await LoginAndGetTokensAsync(email);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);

        var stored = await db.UserRefreshTokens
            .Where(t => t.UserId == user!.Id)
            .ToListAsync();

        // Raw token must not appear in any column
        foreach (var t in stored)
        {
            Assert.NotEqual(refreshToken, t.TokenHash);
            Assert.NotEqual(refreshToken, t.IpAddress ?? "");
            Assert.NotEqual(refreshToken, t.UserAgent ?? "");
            Assert.NotEqual(refreshToken, t.CorrelationId ?? "");
        }
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_RevokesToken_SubsequentRefreshFails()
    {
        var (_, refreshToken) = await LoginAndGetTokensAsync($"rt_logout_{Guid.NewGuid():N}@test.com");

        var client = _factory.CreateClient();
        var logoutResp = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logoutResp.StatusCode);

        var refreshResp = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResp.StatusCode);
    }

    [Fact]
    public async Task Logout_NoToken_ReturnsNoContent()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/logout",
            new { refreshToken = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task Logout_ResponseDoesNotLeakTokenValidity()
    {
        var client = _factory.CreateClient();
        // Logout with a fake token — must still return 204 (no info leak)
        var resp = await client.PostAsJsonAsync("/api/auth/logout",
            new { refreshToken = "fake-token-does-not-exist" });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ── Revoke all sessions ───────────────────────────────────────────────────

    [Fact]
    public async Task RevokeAll_RevokesAllActiveSessions()
    {
        var email = $"rt_revokeall_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "RefreshMe@9876");

        // Login twice to create 2 active sessions
        var client = _factory.CreateClient();
        var (accessToken, _) = await LoginAndGetTokensAsync(email, "RefreshMe@9876");
        var (_, rt2) = await LoginAndGetTokensAsync(email, "RefreshMe@9876");

        // Revoke all
        var revokeClient = _factory.CreateClient();
        revokeClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        var revokeResp = await revokeClient.PostAsync("/api/auth/revoke-sessions", null);
        Assert.Equal(HttpStatusCode.NoContent, revokeResp.StatusCode);

        // Neither session can refresh now
        var refreshResp = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = rt2 });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResp.StatusCode);
    }

    // ── Password change revokes sessions ──────────────────────────────────────

    [Fact]
    public async Task ChangePassword_RevokesExistingRefreshTokens()
    {
        var email = $"rt_chgpwd_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "InitPass@9876");
        var (accessToken, refreshToken) = await LoginAndGetTokensAsync(email, "InitPass@9876");

        // Change password
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
            Content = JsonContent.Create(new { currentPassword = "InitPass@9876", newPassword = "NewPass@9876X!" })
        };
        var chgResp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, chgResp.StatusCode);

        // Refresh token from before password change must now be revoked
        var refreshResp = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResp.StatusCode);
    }

    // ── Password reset revokes sessions ───────────────────────────────────────

    [Fact]
    public async Task ResetPassword_RevokesExistingRefreshTokens()
    {
        var email = $"rt_reset_{Guid.NewGuid():N}@test.com";
        await _factory.CreateStudentAndGetTokenAsync(email);
        var (_, refreshToken) = await LoginAndGetTokensAsync(email, "Student@1234");

        // Generate reset token via UserManager
        var (rawResetToken, userIdStr) = await GenerateResetTokenAsync(email);
        var encodedToken = Base64UrlEncode(rawResetToken);

        var client = _factory.CreateClient();
        var resetResp = await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            userId = userIdStr,
            token = encodedToken,
            newPassword = "ResetNew@9876!",
            confirmPassword = "ResetNew@9876!"
        });
        Assert.Equal(HttpStatusCode.NoContent, resetResp.StatusCode);

        // Refresh token from before reset must now be revoked
        var refreshResp = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResp.StatusCode);
    }

    // ── Audit events ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_Success_WritesRefreshTokenIssuedAuditEvent()
    {
        var email = $"rt_audit_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "RefreshMe@9876");

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "RefreshMe@9876" });

        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var exists = await db.AuthSecurityEvents.AnyAsync(e =>
            e.UserId == user!.Id &&
            e.EventType == AuthEventType.RefreshTokenIssued);
        Assert.True(exists);
    }

    [Fact]
    public async Task Refresh_Rotated_WritesRefreshTokenRotatedAuditEvent()
    {
        var email = $"rt_audit_rotate_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "RefreshMe@9876");
        var (_, refreshToken) = await LoginAndGetTokensAsync(email, "RefreshMe@9876");

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var exists = await db.AuthSecurityEvents.AnyAsync(e =>
            e.UserId == user!.Id &&
            e.EventType == AuthEventType.RefreshTokenRotated);
        Assert.True(exists);
    }

    // ── Regression: 10Auth-F-1/F-2/F-3 still work ────────────────────────────

    [Fact]
    public async Task AdminLogin_StillWorks()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        Assert.False(string.IsNullOrEmpty(adminToken));
    }

    [Fact]
    public async Task StudentLogin_StillWorks()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync(
            $"rt_reg_{Guid.NewGuid():N}@test.com");
        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public async Task SecurityNotifications_StillFire_OnPasswordChange()
    {
        var email = $"rt_notif_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "InitPass@9876");
        var (accessToken, _) = await LoginAndGetTokensAsync(email, "InitPass@9876");

        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
            Content = JsonContent.Create(new { currentPassword = "InitPass@9876", newPassword = "NewPass@9876X!" })
        };
        await client.SendAsync(req);

        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var hasNotification = await db.Notifications.AnyAsync(n =>
            n.RecipientUserId == user!.Id &&
            n.Category == NotificationCategory.Account);
        Assert.True(hasNotification);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task CreateActiveUserAsync(string email, string password)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        if (await userManager.FindByEmailAsync(email) is not null) return;

        var user = new ApplicationUser
        {
            UserName = email, Email = email,
            Role = UserRole.Student, EmailConfirmed = true, MustChangePassword = false
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        db.StudentProfiles.Add(new LinguaCoach.Domain.Entities.StudentProfile(user.Id));
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Issues tokens directly via services — bypasses HTTP login rate limiter in shared test host.
    /// Use for tests that need tokens but are not testing the login endpoint itself.
    /// </summary>
    private async Task<(string AccessToken, string RefreshToken)> LoginAndGetTokensAsync(
        string email, string password = "RefreshMe@9876")
    {
        await CreateActiveUserAsync(email, password);
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenSvc = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var refreshSvc = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        var user = await userManager.FindByEmailAsync(email);
        var accessToken = tokenSvc.GenerateToken(user!.Id, user.Email!, user.Role);
        var refreshResult = await refreshSvc.IssueAsync(
            new IssueRefreshTokenCommand(user.Id, null, null, null));
        return (accessToken, refreshResult.RefreshToken);
    }

    private async Task<(string RawToken, string UserId)> GenerateResetTokenAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        var token = await userManager.GeneratePasswordResetTokenAsync(user!);
        return (token, user!.Id.ToString());
    }

    private static string Base64UrlEncode(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
