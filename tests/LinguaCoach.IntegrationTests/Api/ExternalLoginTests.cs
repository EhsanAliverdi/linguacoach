using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Auth;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 10Auth-F-5: Google external login foundation.
///
/// IGoogleTokenValidator is replaced with FakeGoogleTokenValidator in every test so
/// no real Google API calls are made. The real ExternalLoginService is exercised.
///
/// Rate limiter note: POST /api/auth/external/google uses IP-keyed "AuthExternalLogin"
/// (20 req / 5 min). All test requests share the null IP → "unknown" bucket.
/// Tests that call the endpoint directly are kept to a small number; other scenarios
/// bypass the HTTP layer by invoking IExternalLoginService directly via DI.
/// </summary>
public sealed class ExternalLoginTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private const string GoogleProvider = "Google";

    public ExternalLoginTests(ApiTestFactory factory) => _factory = factory;

    // ── Provider disabled ─────────────────────────────────────────────────────

    [Fact]
    public async Task GoogleLogin_ProviderDisabled_ReturnsUnauthorized()
    {
        // Default config has Google disabled — no fake validator needed
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/external/google",
            new { idToken = "any-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("External login failed.", body.GetProperty("error").GetString());
    }

    // ── Invalid / unverified token ────────────────────────────────────────────

    [Fact]
    public async Task GoogleLogin_InvalidToken_ReturnsGenericUnauthorized()
    {
        var client = BuildClientWithFakeValidator(
            GoogleTokenValidationResult.Fail("InvalidToken"),
            googleEnabled: true);

        var resp = await client.PostAsJsonAsync("/api/auth/external/google",
            new { idToken = "bad-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        // Generic — does not reveal token validation details
        Assert.Equal("External login failed.", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task GoogleLogin_UnverifiedEmail_ReturnsUnauthorized()
    {
        var payload = new GoogleTokenPayload("sub123", "unverified@example.com",
            EmailVerified: false, HostedDomain: null, DisplayName: "Test");

        var client = BuildClientWithFakeValidator(
            GoogleTokenValidationResult.Ok(payload),
            googleEnabled: true);

        var resp = await client.PostAsJsonAsync("/api/auth/external/google",
            new { idToken = "unverified-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Unknown user / no auto-provisioning ──────────────────────────────────

    [Fact]
    public async Task GoogleLogin_UnknownUser_NoProvisioning_ReturnsUnauthorized()
    {
        var payload = new GoogleTokenPayload(
            $"sub_{Guid.NewGuid():N}",
            $"unknown_{Guid.NewGuid():N}@example.com",
            EmailVerified: true, HostedDomain: null, DisplayName: "Ghost");

        var client = BuildClientWithFakeValidator(
            GoogleTokenValidationResult.Ok(payload),
            googleEnabled: true);

        var resp = await client.PostAsJsonAsync("/api/auth/external/google",
            new { idToken = "unknown-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Existing linked account — sign in succeeds ────────────────────────────

    [Fact]
    public async Task GoogleLogin_ExistingLinkedAccount_ReturnsTokens()
    {
        var email = $"gext_linked_{Guid.NewGuid():N}@test.com";
        var sub = $"gsub_{Guid.NewGuid():N}";
        await CreateLinkedUserAsync(email, sub);

        var payload = new GoogleTokenPayload(sub, email,
            EmailVerified: true, HostedDomain: null, DisplayName: "Linked User");

        var result = await InvokeExternalLoginDirectlyAsync(payload, googleEnabled: true);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.Equal(UserRole.Student, result.Role);
        Assert.False(result.MustChangePassword);
    }

    [Fact]
    public async Task GoogleLogin_ExistingLinkedAccount_RefreshTokenStoredAsHashNotRaw()
    {
        var email = $"gext_hash_{Guid.NewGuid():N}@test.com";
        var sub = $"gsub_{Guid.NewGuid():N}";
        await CreateLinkedUserAsync(email, sub);

        var payload = new GoogleTokenPayload(sub, email,
            EmailVerified: true, HostedDomain: null, DisplayName: "Hash Test");

        var result = await InvokeExternalLoginDirectlyAsync(payload, googleEnabled: true);

        Assert.True(result.Succeeded);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var user = await scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);

        var tokens = await db.UserRefreshTokens
            .Where(t => t.UserId == user!.Id)
            .ToListAsync();

        Assert.NotEmpty(tokens);
        Assert.All(tokens, t => Assert.NotEqual(result.RefreshToken, t.TokenHash));
        var expected = Infrastructure.Auth.RefreshTokenService.HashToken(result.RefreshToken!);
        Assert.Contains(tokens, t => t.TokenHash == expected);
    }

    // ── Auto-link by email ────────────────────────────────────────────────────

    [Fact]
    public async Task GoogleLogin_AutoLinkByEmail_LinksExistingAccount()
    {
        var email = $"gext_autolink_{Guid.NewGuid():N}@test.com";
        var sub = $"gsub_new_{Guid.NewGuid():N}";
        // Create local account — no external login linked yet
        await CreateLocalUserAsync(email, "TempPass@1234");

        var payload = new GoogleTokenPayload(sub, email,
            EmailVerified: true, HostedDomain: null, DisplayName: "Auto Link");

        var result = await InvokeExternalLoginDirectlyAsync(payload,
            googleEnabled: true, allowAutoLink: true);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.AccessToken);

        // Verify the link was persisted
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await um.FindByLoginAsync(GoogleProvider, sub);
        Assert.NotNull(user);
        Assert.Equal(email, user!.Email);
    }

    [Fact]
    public async Task GoogleLogin_AutoLinkDisabled_ExistingEmailRejected()
    {
        var email = $"gext_nolink_{Guid.NewGuid():N}@test.com";
        var sub = $"gsub_nolink_{Guid.NewGuid():N}";
        await CreateLocalUserAsync(email, "TempPass@1234");

        var payload = new GoogleTokenPayload(sub, email,
            EmailVerified: true, HostedDomain: null, DisplayName: "No Link");

        var result = await InvokeExternalLoginDirectlyAsync(payload,
            googleEnabled: true, allowAutoLink: false);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
    }

    // ── Allowed domain restriction ────────────────────────────────────────────

    [Fact]
    public async Task GoogleLogin_WrongDomain_ReturnsUnauthorized()
    {
        var payload = new GoogleTokenPayload(
            $"sub_{Guid.NewGuid():N}",
            $"user_{Guid.NewGuid():N}@otherdomain.com",
            EmailVerified: true, HostedDomain: "otherdomain.com", DisplayName: "Wrong Domain");

        var result = await InvokeExternalLoginDirectlyAsync(payload,
            googleEnabled: true, allowedDomains: ["allowed.com"]);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task GoogleLogin_AllowedDomain_Passes()
    {
        var email = $"gext_domain_{Guid.NewGuid():N}@allowed.com";
        var sub = $"gsub_domain_{Guid.NewGuid():N}";
        await CreateLinkedUserAsync(email, sub);

        var payload = new GoogleTokenPayload(sub, email,
            EmailVerified: true, HostedDomain: "allowed.com", DisplayName: "Domain User");

        var result = await InvokeExternalLoginDirectlyAsync(payload,
            googleEnabled: true, allowedDomains: ["allowed.com"]);

        Assert.True(result.Succeeded);
    }

    // ── Role preservation ─────────────────────────────────────────────────────

    [Fact]
    public async Task GoogleLogin_ExistingRole_IsPreserved()
    {
        var email = $"gext_role_{Guid.NewGuid():N}@test.com";
        var sub = $"gsub_role_{Guid.NewGuid():N}";
        await CreateLinkedUserAsync(email, sub, UserRole.Student);

        var payload = new GoogleTokenPayload(sub, email,
            EmailVerified: true, HostedDomain: null, DisplayName: "Role Check");

        var result = await InvokeExternalLoginDirectlyAsync(payload, googleEnabled: true);

        Assert.True(result.Succeeded);
        Assert.Equal(UserRole.Student, result.Role);
    }

    // ── Admin not auto-created ────────────────────────────────────────────────

    [Fact]
    public async Task GoogleLogin_UnknownAdminEmail_NotAutoProvisioned()
    {
        var payload = new GoogleTokenPayload(
            $"sub_admin_{Guid.NewGuid():N}",
            $"admin_{Guid.NewGuid():N}@test.com",
            EmailVerified: true, HostedDomain: null, DisplayName: "Would-Be Admin");

        // Even with student auto-provisioning off (default), unknown user is rejected
        var result = await InvokeExternalLoginDirectlyAsync(payload,
            googleEnabled: true, allowStudentAutoProvisioning: false);

        Assert.False(result.Succeeded);
    }

    // ── Refresh / logout works for external-login session ────────────────────

    [Fact]
    public async Task GoogleLogin_Session_CanBeRefreshed()
    {
        var email = $"gext_sess_{Guid.NewGuid():N}@test.com";
        var sub = $"gsub_sess_{Guid.NewGuid():N}";
        await CreateLinkedUserAsync(email, sub);

        var payload = new GoogleTokenPayload(sub, email,
            EmailVerified: true, HostedDomain: null, DisplayName: "Session User");

        var loginResult = await InvokeExternalLoginDirectlyAsync(payload, googleEnabled: true);
        Assert.True(loginResult.Succeeded);

        // Use the HTTP refresh endpoint
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = loginResult.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(body.GetProperty("token").GetString());
    }

    [Fact]
    public async Task GoogleLogin_Session_CanBeRevoked()
    {
        var email = $"gext_revoke_{Guid.NewGuid():N}@test.com";
        var sub = $"gsub_revoke_{Guid.NewGuid():N}";
        await CreateLinkedUserAsync(email, sub);

        var payload = new GoogleTokenPayload(sub, email,
            EmailVerified: true, HostedDomain: null, DisplayName: "Revoke User");

        var loginResult = await InvokeExternalLoginDirectlyAsync(payload, googleEnabled: true);
        Assert.True(loginResult.Succeeded);

        var client = _factory.CreateClient();
        // Logout — revoke the token
        var logoutResp = await client.PostAsJsonAsync("/api/auth/logout",
            new { refreshToken = loginResult.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logoutResp.StatusCode);

        // Refresh should now fail
        var refreshResp = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = loginResult.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResp.StatusCode);
    }

    // ── Audit events ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GoogleLogin_Success_WritesExternalLoginSucceededAuditEvent()
    {
        var email = $"gext_audit_{Guid.NewGuid():N}@test.com";
        var sub = $"gsub_audit_{Guid.NewGuid():N}";
        await CreateLinkedUserAsync(email, sub);

        var payload = new GoogleTokenPayload(sub, email,
            EmailVerified: true, HostedDomain: null, DisplayName: "Audit User");

        var result = await InvokeExternalLoginDirectlyAsync(payload, googleEnabled: true);
        Assert.True(result.Succeeded);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var user = await scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);

        var auditEvent = await db.AuthSecurityEvents
            .Where(e => e.UserId == user!.Id
                && e.EventType == AuthEventType.ExternalLoginSucceeded)
            .FirstOrDefaultAsync();

        Assert.NotNull(auditEvent);
        Assert.Equal(AuthEventOutcome.Success, auditEvent!.Outcome);
    }

    [Fact]
    public async Task GoogleLogin_AutoLink_WritesExternalLoginLinkedAuditEvent()
    {
        var email = $"gext_linkedaudit_{Guid.NewGuid():N}@test.com";
        var sub = $"gsub_linkedaudit_{Guid.NewGuid():N}";
        await CreateLocalUserAsync(email, "TempPass@1234");

        var payload = new GoogleTokenPayload(sub, email,
            EmailVerified: true, HostedDomain: null, DisplayName: "Link Audit");

        var result = await InvokeExternalLoginDirectlyAsync(payload,
            googleEnabled: true, allowAutoLink: true);
        Assert.True(result.Succeeded);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var user = await scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);

        var linkEvent = await db.AuthSecurityEvents
            .Where(e => e.UserId == user!.Id
                && e.EventType == AuthEventType.ExternalLoginLinked)
            .FirstOrDefaultAsync();

        Assert.NotNull(linkEvent);
    }

    [Fact]
    public async Task GoogleLogin_AuditMetadata_DoesNotContainRawIdToken()
    {
        // Ensure no raw token leaks into audit metadata
        var email = $"gext_tokenleak_{Guid.NewGuid():N}@test.com";
        var sub = $"gsub_tokenleak_{Guid.NewGuid():N}";
        await CreateLinkedUserAsync(email, sub);

        var rawIdToken = "fake-raw-google-id-token-must-not-appear-in-audit";
        var payload = new GoogleTokenPayload(sub, email,
            EmailVerified: true, HostedDomain: null, DisplayName: "Leak Test");

        using var scope = _factory.Services.CreateScope();
        var svc = BuildExternalLoginService(scope, payload, googleEnabled: true);
        await svc.GoogleLoginAsync(new ExternalLoginRequest(rawIdToken, null, null, null));

        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var user = await scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);

        var events = await db.AuthSecurityEvents
            .Where(e => e.UserId == user!.Id)
            .ToListAsync();

        Assert.All(events, e =>
        {
            Assert.DoesNotContain(rawIdToken, e.MetadataJson ?? string.Empty);
        });
    }

    // ── Notification on link ──────────────────────────────────────────────────

    [Fact]
    public async Task GoogleLogin_AutoLink_QueuesLinkedNotifications()
    {
        var email = $"gext_notif_{Guid.NewGuid():N}@test.com";
        var sub = $"gsub_notif_{Guid.NewGuid():N}";
        await CreateLocalUserAsync(email, "TempPass@1234");

        var payload = new GoogleTokenPayload(sub, email,
            EmailVerified: true, HostedDomain: null, DisplayName: "Notif User");

        var result = await InvokeExternalLoginDirectlyAsync(payload,
            googleEnabled: true, allowAutoLink: true);
        Assert.True(result.Succeeded);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var user = await scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>()
            .FindByEmailAsync(email);

        var notifs = await db.Notifications
            .Where(n => n.RecipientUserId == user!.Id)
            .ToListAsync();

        Assert.Contains(notifs, n => n.Channel == NotificationChannel.InApp);
        Assert.Contains(notifs, n => n.Channel == NotificationChannel.Email);
        // Notifications must not contain token or secret content
        Assert.All(notifs, n => Assert.DoesNotContain("fake-raw", n.Body ?? string.Empty));
    }

    // ── Regression ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LocalLogin_StillWorks_AfterExternalLoginPhase()
    {
        var email = $"gext_regression_local_{Guid.NewGuid():N}@test.com";
        await CreateLocalUserAsync(email, "LocalLogin@9876");

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "LocalLogin@9876" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotNull(body.GetProperty("token").GetString());
    }

    [Fact]
    public async Task RefreshToken_StillWorks_AfterExternalLoginPhase()
    {
        var email = $"gext_regression_refresh_{Guid.NewGuid():N}@test.com";
        await CreateLocalUserAsync(email, "LocalLogin@9876");

        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenSvc = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var refreshSvc = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var user = await um.FindByEmailAsync(email);
        var refreshResult = await refreshSvc.IssueAsync(
            new IssueRefreshTokenCommand(user!.Id, null, null, null));

        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = refreshResult.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Invokes IExternalLoginService directly (bypasses HTTP layer + rate limiter).
    /// Overrides IGoogleTokenValidator to return the given payload.
    /// Config is set to control enabled/autolink/domain/provisioning flags.
    /// </summary>
    private async Task<ExternalLoginResult> InvokeExternalLoginDirectlyAsync(
        GoogleTokenPayload payload,
        bool googleEnabled = true,
        bool allowAutoLink = true,
        bool allowStudentAutoProvisioning = false,
        List<string>? allowedDomains = null)
    {
        using var scope = _factory.Services.CreateScope();
        var svc = BuildExternalLoginService(scope, payload,
            googleEnabled, allowAutoLink, allowStudentAutoProvisioning, allowedDomains);
        return await svc.GoogleLoginAsync(new ExternalLoginRequest("fake-token", null, null, null));
    }

    private static IExternalLoginService BuildExternalLoginService(
        IServiceScope scope,
        GoogleTokenPayload? payload,
        bool googleEnabled = true,
        bool allowAutoLink = true,
        bool allowStudentAutoProvisioning = false,
        List<string>? allowedDomains = null)
    {
        var fakeValidator = payload is not null
            ? new FakeGoogleTokenValidator(GoogleTokenValidationResult.Ok(payload))
            : new FakeGoogleTokenValidator(GoogleTokenValidationResult.Fail("NoPayload"));

        var options = Microsoft.Extensions.Options.Options.Create(new GoogleExternalLoginOptions
        {
            Enabled = googleEnabled,
            ClientId = "fake-client-id",
            ClientSecret = "fake-secret",
            AllowAutoLinkByEmail = allowAutoLink,
            AllowStudentAutoProvisioning = allowStudentAutoProvisioning,
            AllowedDomains = allowedDomains ?? [],
        });

        return ActivatorUtilities.CreateInstance<Infrastructure.Auth.ExternalLoginService>(
            scope.ServiceProvider,
            (IGoogleTokenValidator)fakeValidator,
            options);
    }

    /// <summary>
    /// Builds an HttpClient whose DI has IGoogleTokenValidator replaced with the fake.
    /// Config overrides Google:Enabled=true, ClientId set.
    /// Only needed for tests that must go through the HTTP endpoint.
    /// </summary>
    private HttpClient BuildClientWithFakeValidator(
        GoogleTokenValidationResult validationResult,
        bool googleEnabled = true)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGoogleTokenValidator>();
                services.AddScoped<IGoogleTokenValidator>(
                    _ => new FakeGoogleTokenValidator(validationResult));

                services.PostConfigure<GoogleExternalLoginOptions>(opts =>
                {
                    opts.Enabled = googleEnabled;
                    opts.ClientId = "fake-client-id";
                    opts.ClientSecret = "fake-secret";
                    opts.AllowAutoLinkByEmail = true;
                });
            });
        }).CreateClient();
    }

    private async Task CreateLinkedUserAsync(string email, string googleSub,
        UserRole role = UserRole.Student)
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Role = role,
            MustChangePassword = false,
        };
        var result = await um.CreateAsync(user, $"ExtLogin1@{Guid.NewGuid():N}"[..16]);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await um.AddLoginAsync(user, new UserLoginInfo(GoogleProvider, googleSub, "Google"));

        if (role == UserRole.Student)
        {
            db.StudentProfiles.Add(new LinguaCoach.Domain.Entities.StudentProfile(user.Id));
            await db.SaveChangesAsync();
        }
    }

    private async Task CreateLocalUserAsync(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Role = UserRole.Student,
            MustChangePassword = false,
        };
        var result = await um.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        db.StudentProfiles.Add(new LinguaCoach.Domain.Entities.StudentProfile(user.Id));
        await db.SaveChangesAsync();
    }
}

/// <summary>
/// Test double for IGoogleTokenValidator. Returns a fixed result without calling Google APIs.
/// </summary>
internal sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleTokenValidationResult _result;

    public FakeGoogleTokenValidator(GoogleTokenValidationResult result) => _result = result;

    public Task<GoogleTokenValidationResult> ValidateAsync(
        string idToken, string expectedClientId, CancellationToken ct = default)
        => Task.FromResult(_result);
}
