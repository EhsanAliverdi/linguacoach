using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 10Auth-F-2 auth event audit log.
/// Verifies that auth flows write correct events and that sensitive
/// fields (tokens, passwords) are never stored in audit records.
/// </summary>
public sealed class AuthAuditLogTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AuthAuditLogTests(ApiTestFactory factory) => _factory = factory;

    // ── Login events ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_Success_WritesLoginSucceededEvent()
    {
        var email = $"audit_ok_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "AuditPass@9876");

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "AuditPass@9876" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var events = await GetEventsForEmailAsync(email);
        Assert.Contains(events, e =>
            e.EventType == AuthEventType.LoginSucceeded &&
            e.Outcome == AuthEventOutcome.Success);
    }

    [Fact]
    public async Task Login_WrongPassword_WritesLoginFailedEvent()
    {
        var email = $"audit_fail_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "AuditPass@9876");

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPass@1!" });

        var events = await GetEventsForEmailAsync(email);
        Assert.Contains(events, e =>
            e.EventType == AuthEventType.LoginFailed &&
            e.Outcome == AuthEventOutcome.Failure &&
            e.FailureReasonCode == "InvalidCredentials");
    }

    [Fact]
    public async Task Login_UnknownUser_WritesLoginFailedEvent_WithGenericReason()
    {
        var email = $"nobody_{Guid.NewGuid():N}@test.com";
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = "AnyPass@123" });

        var events = await GetEventsForEmailAsync(email);
        Assert.Contains(events, e =>
            e.EventType == AuthEventType.LoginFailed &&
            e.FailureReasonCode == "UnknownUserGeneric");
    }

    [Fact]
    public async Task Login_LockedAccount_WritesLoginLockedOutEvent()
    {
        var email = $"audit_lock_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "LockAudit@9876");

        // Lock via UserManager directly
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        await userManager.SetLockoutEnabledAsync(user!, true);
        await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(15));

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = "LockAudit@9876" });

        var events = await GetEventsForEmailAsync(email);
        Assert.Contains(events, e =>
            e.EventType == AuthEventType.LoginLockedOut &&
            e.Outcome == AuthEventOutcome.Blocked &&
            e.FailureReasonCode == "LockedOut");
    }

    // ── Password change events ─────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_Success_WritesPasswordChangedEvent()
    {
        var email = $"audit_cp_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "InitPass@9876");

        var token = await GetTokenDirectAsync(email);
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new { currentPassword = "InitPass@9876", newPassword = "NewPass@9876X!" })
        };
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);
        var events = await GetEventsForUserAsync(user!.Id);
        Assert.Contains(events, e => e.EventType == AuthEventType.PasswordChanged && e.Outcome == AuthEventOutcome.Success);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_WritesPasswordChangeFailedEvent()
    {
        var email = $"audit_cpfail_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "InitPass@9876");

        var token = await GetTokenDirectAsync(email);
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new { currentPassword = "WrongCurrent@1!", newPassword = "NewPass@9876X!" })
        };
        await client.SendAsync(req);

        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);
        var events = await GetEventsForUserAsync(user!.Id);
        Assert.Contains(events, e => e.EventType == AuthEventType.PasswordChangeFailed && e.Outcome == AuthEventOutcome.Failure);
    }

    [Fact]
    public async Task ForcePasswordChange_Completion_WritesForcePasswordChangeCompletedEvent()
    {
        var email = $"audit_fpc_{Guid.NewGuid():N}@test.com";
        await CreateStudentWithMustChangeAsync(email, "TempPass@9876");

        var token = await GetTokenDirectAsync(email);
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new { currentPassword = "TempPass@9876", newPassword = "ForcedNew@9876!" })
        };
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);
        var events = await GetEventsForUserAsync(user!.Id);
        Assert.Contains(events, e => e.EventType == AuthEventType.ForcePasswordChangeCompleted && e.Outcome == AuthEventOutcome.Success);
    }

    // ── Password reset events ──────────────────────────────────────────────────

    [Fact]
    public async Task SendResetLink_WritesPasswordResetRequestedEvent()
    {
        var email = $"audit_rl_{Guid.NewGuid():N}@test.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var studentProfileId = await GetStudentProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await client.PostAsync($"/api/admin/students/{studentProfileId}/send-reset-link", null);

        var events = await GetEventsForUserAsync(userId);
        Assert.Contains(events, e =>
            e.EventType == AuthEventType.PasswordResetRequested &&
            e.Outcome == AuthEventOutcome.Requested);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_WritesPasswordResetFailedEvent()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            userId = Guid.NewGuid().ToString(),
            token = "invalid-token",
            newPassword = "NewPass@9876!",
            confirmPassword = "NewPass@9876!",
        });

        // No specific user to query — just verify a PasswordResetFailed event was written
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var exists = await db.AuthSecurityEvents
            .AnyAsync(e => e.EventType == AuthEventType.PasswordResetFailed);
        Assert.True(exists);
    }

    // ── Student creation events ────────────────────────────────────────────────

    [Fact]
    public async Task CreateStudent_WritesStudentAccountCreatedEvent()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var email = $"audit_create_{Guid.NewGuid():N}@test.com";

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var resp = await client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@12345" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        var events = await GetEventsForEmailAsync(email);
        Assert.Contains(events, e =>
            e.EventType == AuthEventType.StudentAccountCreated &&
            e.Outcome == AuthEventOutcome.Success);
    }

    // ── Security: no secrets in audit records ──────────────────────────────────

    [Fact]
    public async Task AuditEvents_DoNotContainResetToken()
    {
        var email = $"audit_sec_{Guid.NewGuid():N}@test.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var studentProfileId = await GetStudentProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await client.PostAsync($"/api/admin/students/{studentProfileId}/send-reset-link", null);

        var events = await GetEventsForUserAsync(userId);
        foreach (var e in events)
        {
            if (e.MetadataJson is not null)
                Assert.DoesNotContain("token", e.MetadataJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task AuditEvents_DoNotContainPassword()
    {
        var email = $"audit_pwd_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "AuditNoPwd@9876");
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = "AuditNoPwd@9876" });

        var events = await GetEventsForEmailAsync(email);
        foreach (var e in events)
        {
            if (e.MetadataJson is not null)
            {
                Assert.DoesNotContain("password", e.MetadataJson, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("AuditNoPwd", e.MetadataJson, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ── Admin query endpoint ───────────────────────────────────────────────────

    [Fact]
    public async Task AdminListAuthEvents_AsAdmin_Returns200WithItems()
    {
        // Ensure at least one event exists
        var email = $"audit_list_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "ListTest@9876");
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = "ListTest@9876" });

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await adminClient.GetAsync("/api/admin/auth-events?pageSize=10");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 0);
        Assert.True(body.TryGetProperty("items", out _));
    }

    [Fact]
    public async Task AdminListAuthEvents_AsStudent_Returns403()
    {
        var (studentToken, _) = await _factory.CreateStudentAndGetTokenAsync($"audit403_{Guid.NewGuid():N}@t.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var resp = await client.GetAsync("/api/admin/auth-events");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AdminListAuthEvents_FilterByEventType_ReturnsMatchingItems()
    {
        var email = $"audit_filter_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "FilterTest@9876");
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = "FilterTest@9876" });

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await adminClient.GetAsync("/api/admin/auth-events?eventType=LoginSucceeded&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.All(items, item =>
            Assert.Equal("LoginSucceeded", item.GetProperty("eventType").GetString()));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task CreateActiveUserAsync(string email, string password)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

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

    private async Task CreateStudentWithMustChangeAsync(string email, string password)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var user = new ApplicationUser
        {
            UserName = email, Email = email,
            Role = UserRole.Student, EmailConfirmed = true, MustChangePassword = true
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        db.StudentProfiles.Add(new LinguaCoach.Domain.Entities.StudentProfile(user.Id));
        await db.SaveChangesAsync();
    }

    private async Task<string> GetTokenDirectAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenSvc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();
        var user = await userManager.FindByEmailAsync(email);
        return tokenSvc.GenerateToken(user!.Id, user.Email!, user.Role);
    }

    private async Task<List<AuthSecurityEvent>> GetEventsForEmailAsync(string email)
    {
        await Task.Delay(50); // allow SaveChangesAsync to complete
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        return await db.AuthSecurityEvents
            .Where(e => e.EmailOrUserName == email.ToLowerInvariant())
            .ToListAsync();
    }

    private async Task<List<AuthSecurityEvent>> GetEventsForUserAsync(Guid userId)
    {
        await Task.Delay(50);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        return await db.AuthSecurityEvents
            .Where(e => e.UserId == userId)
            .ToListAsync();
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
