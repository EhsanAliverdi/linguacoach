using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class AuthEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── POST /api/auth/login ──────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidAdminCredentials_Returns200WithToken()
    {
        await EnsureAdminExistsAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "login_admin@test.com", password = "Admin@1234" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("token").GetString()?.Length > 0);
        Assert.Equal("Admin", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await EnsureAdminExistsAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "login_admin@test.com", password = "WrongPassword!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@nowhere.com", password = "anything" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── POST /api/auth/change-password ────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_WithValidCredentials_Returns204()
    {
        var (token, _) = await CreateTempStudentAsync("cp_student@test.com", "Temp@12345");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(new { currentPassword = "Temp@12345", newPassword = "NewPass@5678" })
        };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "x", newPassword = "y" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TemporaryPassword_UserCannotAccessProtectedEndpoints()
    {
        var (token, _) = await CreateTempStudentAsync($"restricted_{Guid.NewGuid():N}@test.com", "Temp@12345");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/onboarding/status")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token) }
        };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task EnsureAdminExistsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (await userManager.FindByEmailAsync("login_admin@test.com") is null)
        {
            var admin = new ApplicationUser
            {
                UserName = "login_admin@test.com",
                Email = "login_admin@test.com",
                Role = UserRole.Admin,
                EmailConfirmed = true,
                MustChangePassword = false
            };
            await userManager.CreateAsync(admin, "Admin@1234");
        }
    }

    private async Task<(string Token, Guid UserId)> CreateTempStudentAsync(string email, string password)
    {
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
        await userManager.CreateAsync(user, password);
        var profile = new LinguaCoach.Domain.Entities.StudentProfile(user.Id);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();
        return (svc.GenerateToken(user.Id, user.Email!, user.Role), user.Id);
    }
}
