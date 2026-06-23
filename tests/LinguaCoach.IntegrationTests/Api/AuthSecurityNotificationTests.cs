using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 10Auth-F-3 security notifications.
/// Verifies that auth/security flows queue the correct notifications
/// and that sensitive data (passwords, tokens) never appears in notification payloads.
/// </summary>
public sealed class AuthSecurityNotificationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AuthSecurityNotificationTests(ApiTestFactory factory) => _factory = factory;

    // ── Password changed ──────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_Success_QueuesInAppAndEmailNotifications()
    {
        var email = $"sn_cp_{Guid.NewGuid():N}@test.com";
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
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == user!.Id)
            .ToListAsync();

        Assert.Contains(notifications, n =>
            n.Channel == NotificationChannel.InApp &&
            n.Category == NotificationCategory.Account &&
            n.Severity == NotificationSeverity.Warning);

        Assert.Contains(notifications, n =>
            n.Channel == NotificationChannel.Email &&
            n.Category == NotificationCategory.Account &&
            n.Severity == NotificationSeverity.Warning);
    }

    [Fact]
    public async Task ChangePassword_Notification_DoesNotContainPassword()
    {
        var email = $"sn_cpnopasswd_{Guid.NewGuid():N}@test.com";
        const string password = "InitPass@9876";
        await CreateActiveUserAsync(email, password);

        var token = await GetTokenDirectAsync(email);
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new { currentPassword = password, newPassword = "NewPass@9876X!" })
        };
        await client.SendAsync(req);

        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == user!.Id)
            .ToListAsync();

        foreach (var n in notifications)
        {
            Assert.DoesNotContain(password, n.Body ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NewPass", n.Body ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ChangePassword_Failure_DoesNotQueueNotification()
    {
        var email = $"sn_cpfail_{Guid.NewGuid():N}@test.com";
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
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var count = await db.Notifications.CountAsync(n => n.RecipientUserId == user!.Id);
        Assert.Equal(0, count);
    }

    // ── Password reset requested ──────────────────────────────────────────────

    [Fact]
    public async Task SendResetLink_QueuesResetLinkEmailAndInAppNotification()
    {
        var email = $"sn_rl_{Guid.NewGuid():N}@test.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var studentProfileId = await GetStudentProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await client.PostAsync($"/api/admin/students/{studentProfileId}/send-reset-link", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == userId)
            .ToListAsync();

        // Email: the reset-link email
        Assert.Contains(notifications, n => n.Channel == NotificationChannel.Email);
        // In-app: password reset requested
        Assert.Contains(notifications, n => n.Channel == NotificationChannel.InApp);
    }

    [Fact]
    public async Task SendResetLink_Notification_DoesNotContainToken()
    {
        var email = $"sn_rltoken_{Guid.NewGuid():N}@test.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var studentProfileId = await GetStudentProfileIdAsync(userId);
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await client.PostAsync($"/api/admin/students/{studentProfileId}/send-reset-link", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == userId)
            .ToListAsync();

        // In-app notification must not contain reset token
        foreach (var n in notifications.Where(n => n.Channel == NotificationChannel.InApp))
        {
            Assert.DoesNotContain("token", n.Body ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("reset-password?", n.Body ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Password reset succeeded ──────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_Success_QueuesInAppAndEmailNotifications()
    {
        var email = $"sn_rsucc_{Guid.NewGuid():N}@test.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);

        // Get a valid reset token directly via UserManager
        var (rawToken, userIdStr) = await GenerateResetTokenAsync(email);
        var encodedToken = Base64UrlEncode(rawToken);

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            userId = userIdStr,
            token = encodedToken,
            newPassword = "ResetNew@9876!",
            confirmPassword = "ResetNew@9876!"
        });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == userId)
            .ToListAsync();

        Assert.Contains(notifications, n =>
            n.Channel == NotificationChannel.InApp &&
            n.Category == NotificationCategory.Account);

        Assert.Contains(notifications, n =>
            n.Channel == NotificationChannel.Email &&
            n.Category == NotificationCategory.Account);
    }

    [Fact]
    public async Task ResetPassword_Success_NotificationDoesNotContainToken()
    {
        var email = $"sn_rtoken_{Guid.NewGuid():N}@test.com";
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync(email);
        var (rawToken, userIdStr) = await GenerateResetTokenAsync(email);
        var encodedToken = Base64UrlEncode(rawToken);

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            userId = userIdStr,
            token = encodedToken,
            newPassword = "ResetNew@9876!",
            confirmPassword = "ResetNew@9876!"
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == userId)
            .ToListAsync();

        foreach (var n in notifications)
        {
            Assert.DoesNotContain(encodedToken, n.Body ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(rawToken[..10], n.Body ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Account locked ────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_LockoutTransition_QueuesAccountLockedNotification()
    {
        var email = $"sn_lock_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "LockNotif@9876");

        // Trigger lockout via UserManager directly (5 AccessFailedAsync calls = lockout)
        using var setupScope = _factory.Services.CreateScope();
        var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        await userManager.SetLockoutEnabledAsync(user!, true);

        // Bring to 4 failed attempts (one away from lockout)
        for (var i = 0; i < 4; i++)
            await userManager.AccessFailedAsync(user!);

        // The 5th attempt via HTTP triggers lockout transition + notification
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPass@1!" });

        await Task.Delay(100); // allow SaveChangesAsync

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var userId = user!.Id;

        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == userId)
            .ToListAsync();

        Assert.Contains(notifications, n =>
            n.Channel == NotificationChannel.InApp &&
            n.Category == NotificationCategory.Account &&
            n.Severity == NotificationSeverity.Warning);

        Assert.Contains(notifications, n =>
            n.Channel == NotificationChannel.Email &&
            n.Category == NotificationCategory.Account &&
            n.Severity == NotificationSeverity.Warning);
    }

    [Fact]
    public async Task Login_AlreadyLockedAccount_DoesNotQueueAdditionalLockoutNotification()
    {
        var email = $"sn_alreadylock_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "LockNotif@9876");

        // Lock directly — no HTTP hits, no notifications yet
        using var setupScope = _factory.Services.CreateScope();
        var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        await userManager.SetLockoutEnabledAsync(user!, true);
        await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(15));

        // Attempt login against already-locked account — hits the early IsLockedOutAsync check
        // Should NOT queue a lockout notification (no lockout transition occurred)
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = "LockNotif@9876" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var count = await db.Notifications.CountAsync(n =>
            n.RecipientUserId == user!.Id &&
            n.Channel != NotificationChannel.Sms);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Login_LockoutNotification_DoesNotContainPasswordOrIp()
    {
        var email = $"sn_locknopasswd_{Guid.NewGuid():N}@test.com";
        const string password = "LockNoPasswd@9876";
        await CreateActiveUserAsync(email, password);

        using var setupScope = _factory.Services.CreateScope();
        var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        await userManager.SetLockoutEnabledAsync(user!, true);
        for (var i = 0; i < 4; i++)
            await userManager.AccessFailedAsync(user!);

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPass@1!" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var notifications = await db.Notifications
            .Where(n => n.RecipientUserId == user!.Id)
            .ToListAsync();

        foreach (var n in notifications)
        {
            Assert.DoesNotContain(password, n.Body ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WrongPass", n.Body ?? "", StringComparison.OrdinalIgnoreCase);
            // IP is not included in notification body
            Assert.DoesNotContain("127.0.0.1", n.Body ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Preference / mandatory category ──────────────────────────────────────

    [Fact]
    public async Task Account_Category_IsMandatory_CannotBeDisabledByUser()
    {
        // Verify the platform-level invariant: Account is required (cannot be opted out)
        Assert.True(Domain.Entities.NotificationPreference.IsRequired(NotificationCategory.Account));
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

    private async Task<string> GetTokenDirectAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenSvc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();
        var user = await userManager.FindByEmailAsync(email);
        return tokenSvc.GenerateToken(user!.Id, user.Email!, user.Role);
    }

    private async Task<Guid> GetStudentProfileIdAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        return await db.StudentProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .FirstAsync();
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
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
