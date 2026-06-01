using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class AdminEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public AdminEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── POST /api/admin/students ──────────────────────────────────────────────

    [Fact]
    public async Task CreateStudent_AsAdmin_Returns201WithStudentId()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.PostAsJsonAsync("/api/admin/students",
            new { email = $"newstudent_{Guid.NewGuid():N}@test.com", temporaryPassword = "Student@1234" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("studentProfileId").GetString()?.Length > 0);
    }

    [Fact]
    public async Task CreateStudent_DuplicateEmail_Returns409()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"dup_{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });

        var response = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/admin/students",
            new { email = "x@x.com", temporaryPassword = "Student@1234" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_ThenLogin_ReturnsMustChangePasswordTrue()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"mustchange_{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Temp@5678" });

        // Now log in as the newly created student — must_change_password should be true.
        var loginClient = _factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Temp@5678" });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task CreateStudent_AsStudent_Returns403()
    {
        var (studentToken, _) = await _factory.CreateStudentAndGetTokenAsync($"nonadmin_{Guid.NewGuid():N}@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var response = await client.PostAsJsonAsync("/api/admin/students",
            new { email = $"target_{Guid.NewGuid():N}@test.com", temporaryPassword = "Student@1234" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
