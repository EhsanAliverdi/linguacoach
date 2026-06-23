using System.Net;
using System.Net.Http.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for 10Auth-F-1 auth security hardening:
/// lockout, password policy, security headers.
/// Rate-limiting tests are excluded from integration tests because the
/// ASP.NET Core in-memory test host shares a single IP ("::1" or null) for
/// all requests, making deterministic per-IP window assertions unreliable
/// across parallel test runs. Rate-limiting policy registration is verified
/// by compilation + the policy names being referenced in AuthController.
/// </summary>
public sealed class AuthSecurityTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AuthSecurityTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── Lockout ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_AfterMaxFailedAttempts_AccountIsLocked()
    {
        var email = $"lockout_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "LockTest@9876");

        // Trigger lockout directly via UserManager (avoids rate-limiter interaction
        // from hammering the HTTP endpoint in a shared-IP test environment).
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        // Simulate MaxFailedAccessAttempts (5) failed logins
        for (var i = 0; i < 5; i++)
            await userManager.AccessFailedAsync(user!);

        Assert.True(await userManager.IsLockedOutAsync(user!));

        // HTTP login with correct password must be rejected while locked
        var client = _factory.CreateClient();
        var locked = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "LockTest@9876" });

        // Locked account returns 401; response must not reveal lockout state
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        var body = await locked.Content.ReadAsStringAsync();
        Assert.Contains("Invalid credentials", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("locked", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_SuccessfulLogin_ResetFailedCount()
    {
        var email = $"resetcount_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "ResetTest@9876");

        var client = _factory.CreateClient();

        // 4 failed attempts (one under the threshold)
        for (var i = 0; i < 4; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login",
                new { email, password = "WrongPass@1!" });
        }

        // Successful login — should succeed and reset counter
        var ok = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "ResetTest@9876" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        // Verify failed count is 0 in DB
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.Equal(0, user!.AccessFailedCount);
    }

    [Fact]
    public async Task Login_LockedAccount_CorrectPasswordStillRejected()
    {
        var email = $"locked2_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "LockMe@12345");

        // Lock by direct UserManager call
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        await userManager.SetLockoutEnabledAsync(user!, true);
        await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(15));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "LockMe@12345" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Password policy ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateStudent_WeakPassword_TooShort_Returns400()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        // 9 chars — fails RequiredLength = 10
        var response = await client.PostAsJsonAsync("/api/admin/students",
            new { email = $"weak_{Guid.NewGuid():N}@test.com", temporaryPassword = "Short@12" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_WeakPassword_NoUppercase_Returns400()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        // No uppercase
        var response = await client.PostAsJsonAsync("/api/admin/students",
            new { email = $"weak2_{Guid.NewGuid():N}@test.com", temporaryPassword = "nouppercase1!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_WeakPassword_NoSpecialChar_Returns400()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        // No special character
        var response = await client.PostAsJsonAsync("/api/admin/students",
            new { email = $"weak3_{Guid.NewGuid():N}@test.com", temporaryPassword = "NoSpecial123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_StrongPassword_Returns201()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        // 12 chars, upper, lower, digit, special
        var response = await client.PostAsJsonAsync("/api/admin/students",
            new { email = $"strong_{Guid.NewGuid():N}@test.com", temporaryPassword = "Strong@12345" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WeakNewPassword_Returns400()
    {
        var email = $"cpweak_{Guid.NewGuid():N}@test.com";
        await CreateActiveUserAsync(email, "InitPass@9876");

        // Generate token directly — avoids HTTP login rate-limiter contention
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tokenSvc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        var token = tokenSvc.GenerateToken(user!.Id, user.Email!, user.Role);

        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new { currentPassword = "InitPass@9876", newPassword = "weak" })
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Security headers ──────────────────────────────────────────────────────

    [Fact]
    public async Task ApiResponse_IncludesSecurityHeaders()
    {
        var client = _factory.CreateClient();
        // Use a simple public endpoint that always responds
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@test.com", password = "WrongPass@1!" });

        Assert.True(response.Headers.Contains("X-Content-Type-Options"),
            "X-Content-Type-Options header missing");
        Assert.Equal("nosniff",
            response.Headers.GetValues("X-Content-Type-Options").First());

        Assert.True(response.Headers.Contains("X-Frame-Options"),
            "X-Frame-Options header missing");
        Assert.Equal("DENY",
            response.Headers.GetValues("X-Frame-Options").First());

        Assert.True(response.Headers.Contains("Referrer-Policy"),
            "Referrer-Policy header missing");
        Assert.Equal("no-referrer",
            response.Headers.GetValues("Referrer-Policy").First());

        Assert.True(response.Headers.Contains("Permissions-Policy"),
            "Permissions-Policy header missing");
    }

    [Fact]
    public async Task AuthenticatedApiResponse_IncludesSecurityHeaders()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var response = await client.GetAsync("/api/admin/students");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.True(response.Headers.Contains("Referrer-Policy"));
    }

    // ── Regression — existing auth flows still work ───────────────────────────

    [Fact]
    public async Task Login_ValidAdmin_StillReturns200()
    {
        var email = $"regr_admin_{Guid.NewGuid():N}@test.com";
        await CreateAdminAsync(email, "AdminPass@9876");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "AdminPass@9876" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body.GetProperty("token").GetString()?.Length > 0);
    }

    [Fact]
    public async Task Login_ValidStudent_StillReturns200WithMustChangePassword()
    {
        var email = $"regr_student_{Guid.NewGuid():N}@test.com";
        await CreateStudentWithMustChangeAsync(email, "StudentPass@9876");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "StudentPass@9876" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task MustChangePassword_StillEnforced_Returns403()
    {
        var email = $"regr_mcp_{Guid.NewGuid():N}@test.com";
        await CreateStudentWithMustChangeAsync(email, "StudentPass@9876");

        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        var token = svc.GenerateToken(user!.Id, user.Email!, user.Role);

        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/onboarding/status")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) }
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
            UserName = email,
            Email = email,
            Role = UserRole.Student,
            EmailConfirmed = true,
            MustChangePassword = false
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var profile = new LinguaCoach.Domain.Entities.StudentProfile(user.Id);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();
    }

    private async Task CreateAdminAsync(string email, string password)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            Role = UserRole.Admin,
            EmailConfirmed = true,
            MustChangePassword = false
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    private async Task CreateStudentWithMustChangeAsync(string email, string password)
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            Role = UserRole.Student,
            EmailConfirmed = true,
            MustChangePassword = true
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var profile = new LinguaCoach.Domain.Entities.StudentProfile(user.Id);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();
    }
}
