using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 10W-5C notification configuration endpoints.
/// GET /api/admin/notifications/config
/// POST /api/admin/notifications/config/email/test
/// </summary>
public sealed class AdminNotificationConfigEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminNotificationConfigEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── Auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetConfig_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/notifications/config");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetConfig_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"cfg403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/admin/notifications/config");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TestEmail_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/admin/notifications/config/email/test", new { toAddress = "a@b.com" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TestEmail_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"tet403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .PostAsJsonAsync("/api/admin/notifications/config/email/test", new { toAddress = "a@b.com" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GET config ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetConfig_AsAdmin_ReturnsOk()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/notifications/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetConfig_ContainsInAppEnabledStatus()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/notifications/config");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var inApp = body.GetProperty("inApp");
        Assert.Equal("InApp", inApp.GetProperty("channel").GetString());
        Assert.True(inApp.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task GetConfig_ContainsEmailStatus()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/notifications/config");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var email = body.GetProperty("email");
        Assert.True(email.TryGetProperty("enabled", out _));
        Assert.True(email.TryGetProperty("configured", out _));
        Assert.True(email.TryGetProperty("statusLabel", out _));
        Assert.True(email.TryGetProperty("hasPassword", out _));
        Assert.True(email.TryGetProperty("hasUsername", out _));
    }

    [Fact]
    public async Task GetConfig_DoesNotExposePassword()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/notifications/config");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // email object must NOT have a "password" property — only hasPassword (bool)
        var email = body.GetProperty("email");
        Assert.False(email.TryGetProperty("password", out _),
            "email.password must not be exposed");
        Assert.False(email.TryGetProperty("Password", out _),
            "email.Password must not be exposed");

        // hasPassword must be a boolean, not a string value
        Assert.Equal(JsonValueKind.False, email.GetProperty("hasPassword").ValueKind);
    }

    [Fact]
    public async Task GetConfig_SmsShowsDisabledOrDeferred()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/notifications/config");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var sms = body.GetProperty("sms");
        Assert.False(sms.GetProperty("enabled").GetBoolean());
        var label = sms.GetProperty("statusLabel").GetString()!;
        Assert.True(
            label.Contains("Disabled", StringComparison.OrdinalIgnoreCase) ||
            label.Contains("Deferred", StringComparison.OrdinalIgnoreCase),
            $"Expected SMS to be Disabled or Deferred, got: {label}");
    }

    [Fact]
    public async Task GetConfig_ContainsDispatchJobInfo()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/notifications/config");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var job = body.GetProperty("dispatchJob");
        Assert.True(job.GetProperty("enabled").GetBoolean());
        Assert.True(job.GetProperty("batchSize").GetInt32() > 0);
        Assert.False(string.IsNullOrWhiteSpace(job.GetProperty("intervalDescription").GetString()));
    }

    // ── POST test email ───────────────────────────────────────────────────────

    [Fact]
    public async Task TestEmail_MissingAddress_ReturnsBadRequest()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync("/api/admin/notifications/config/email/test", new { toAddress = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestEmail_WhenEmailDisabled_ReturnsOkWithSkippedResult()
    {
        // In test environment Email:Enabled is false (default appsettings)
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync("/api/admin/notifications/config/email/test",
                new { toAddress = "test@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Should not throw — returns skipped result when disabled
        Assert.True(body.TryGetProperty("succeeded", out var succeeded));
        Assert.True(body.TryGetProperty("wasSkipped", out _));
        Assert.True(body.TryGetProperty("message", out _));

        // Email is disabled in test config, so should be skipped
        Assert.False(succeeded.GetBoolean());
    }

    [Fact]
    public async Task TestEmail_ResponseDoesNotExposeSmtpSecret()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync("/api/admin/notifications/config/email/test",
                new { toAddress = "test@example.com" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("smtp", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", body, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
