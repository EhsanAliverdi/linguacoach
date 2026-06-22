using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 10W-5C-2 DB-backed notification channel configuration.
/// PUT /api/admin/notifications/config/email
/// PUT /api/admin/notifications/config/sms
/// PUT /api/admin/notifications/config/in-app
/// GET /api/admin/notifications/config  (now returns V2 with source field)
/// POST /api/admin/notifications/config/email/test  (unchanged)
/// </summary>
public sealed class AdminNotificationChannelConfigEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminNotificationChannelConfigEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── Auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PutEmailConfig_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .PutAsJsonAsync("/api/admin/notifications/config/email", new { isEnabled = false });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutEmailConfig_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"cfgs403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new { isEnabled = false });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PutSmsConfig_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .PutAsJsonAsync("/api/admin/notifications/config/sms", new { isEnabled = false });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutInAppConfig_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .PutAsJsonAsync("/api/admin/notifications/config/in-app", new { isEnabled = true });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GET returns appsettings source when no DB override ───────────────────

    [Fact]
    public async Task GetConfig_ReturnsSourceField()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/notifications/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("source", out var src));
        var sourceValue = src.GetString()!;
        Assert.True(
            sourceValue == "AppSettings" || sourceValue == "Database" || sourceValue == "Mixed",
            $"Expected a valid source value, got: {sourceValue}");
    }

    [Fact]
    public async Task GetConfig_V2_ContainsInAppEmailSmsDispatchAndSource()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var body = await GetConfigBodyAsync(adminToken);

        Assert.True(body.TryGetProperty("inApp", out _));
        Assert.True(body.TryGetProperty("email", out _));
        Assert.True(body.TryGetProperty("sms", out _));
        Assert.True(body.TryGetProperty("dispatchJob", out _));
        Assert.True(body.TryGetProperty("source", out _));
    }

    // ── PUT email config ──────────────────────────────────────────────────────

    [Fact]
    public async Task PutEmailConfig_Disabled_SavesAndReturnsOk()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = false
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("succeeded").GetBoolean());
        Assert.Equal("Database", body.GetProperty("source").GetString());
    }

    [Fact]
    public async Task PutEmailConfig_EnabledWithValidFields_SavesSuccessfully()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = true,
                host = "smtp.example.com",
                port = 587,
                useSsl = true,
                fromAddress = "noreply@example.com",
                fromDisplayName = "SpeakPath Test",
                username = "smtpuser"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("succeeded").GetBoolean());
    }

    [Fact]
    public async Task GetConfig_AfterEmailSave_ReturnsDatabaseSource()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        await ClientWithToken(adminToken).PutAsJsonAsync("/api/admin/notifications/config/email", new
        {
            isEnabled = false
        });

        var body = await GetConfigBodyAsync(adminToken);
        var source = body.GetProperty("source").GetString();
        Assert.True(source == "Database" || source == "Mixed");
    }

    [Fact]
    public async Task PutEmailConfig_EnabledWithoutHost_ReturnsBadRequest()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = true,
                host = "",
                port = 587,
                fromAddress = "noreply@example.com"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutEmailConfig_EnabledWithoutFromAddress_ReturnsBadRequest()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = true,
                host = "smtp.example.com",
                port = 587,
                fromAddress = ""
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutEmailConfig_InvalidPort_ReturnsBadRequest()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = true,
                host = "smtp.example.com",
                port = 99999,
                fromAddress = "noreply@example.com"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Secret safety ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PutEmailConfig_WithSecret_SecretNotReturnedInResponse()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = false,
                newSecret = "super-secret-password"
            });

        var bodyText = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("super-secret-password", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("newSecret", bodyText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetConfig_SecretNeverReturnedToFrontend()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        await ClientWithToken(adminToken).PutAsJsonAsync("/api/admin/notifications/config/email", new
        {
            isEnabled = false,
            newSecret = "should-never-appear"
        });

        var bodyText = await (await ClientWithToken(adminToken)
            .GetAsync("/api/admin/notifications/config"))
            .Content.ReadAsStringAsync();

        Assert.DoesNotContain("should-never-appear", bodyText, StringComparison.OrdinalIgnoreCase);
        // "password" appears only as part of "hasPassword" — verify the raw secret value is absent
        Assert.DoesNotContain("secretEncrypted", bodyText, StringComparison.OrdinalIgnoreCase);
        // must not have a "password" key with a string value (only hasPassword boolean is allowed)
        Assert.DoesNotContain("\"password\":\"", bodyText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetConfig_AfterSecretSaved_ShowsHasPasswordTrue()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        await ClientWithToken(adminToken).PutAsJsonAsync("/api/admin/notifications/config/email", new
        {
            isEnabled = false,
            newSecret = "some-password"
        });

        var body = await GetConfigBodyAsync(adminToken);
        var email = body.GetProperty("email");
        Assert.True(email.GetProperty("hasPassword").GetBoolean());
    }

    [Fact]
    public async Task PutEmailConfig_UpdateWithoutSecret_DoesNotEraseExistingSecret()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        // First save with a secret
        await ClientWithToken(adminToken).PutAsJsonAsync("/api/admin/notifications/config/email", new
        {
            isEnabled = false,
            newSecret = "initial-secret"
        });

        // Second save without providing newSecret
        await ClientWithToken(adminToken).PutAsJsonAsync("/api/admin/notifications/config/email", new
        {
            isEnabled = false
            // newSecret omitted
        });

        var body = await GetConfigBodyAsync(adminToken);
        Assert.True(body.GetProperty("email").GetProperty("hasPassword").GetBoolean(),
            "Secret should be preserved when not explicitly cleared");
    }

    [Fact]
    public async Task PutEmailConfig_ClearSecret_ErasesExistingSecret()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        await ClientWithToken(adminToken).PutAsJsonAsync("/api/admin/notifications/config/email", new
        {
            isEnabled = false,
            newSecret = "initial-secret"
        });

        await ClientWithToken(adminToken).PutAsJsonAsync("/api/admin/notifications/config/email", new
        {
            isEnabled = false,
            clearSecret = true
        });

        var body = await GetConfigBodyAsync(adminToken);
        Assert.False(body.GetProperty("email").GetProperty("hasPassword").GetBoolean(),
            "Secret should be erased when clearSecret=true");
    }

    // ── PUT SMS config ────────────────────────────────────────────────────────

    [Fact]
    public async Task PutSmsConfig_Disabled_SavesSuccessfully()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/sms", new
            {
                isEnabled = false,
                provider = "Twilio",
                senderId = "SpeakPath"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("succeeded").GetBoolean());
        Assert.Contains("SMS", body.GetProperty("message").GetString()!);
    }

    [Fact]
    public async Task PutSmsConfig_SecretNotReturnedInResponse()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/sms", new
            {
                isEnabled = false,
                newSecret = "twilio-auth-token-secret"
            });

        var bodyText = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("twilio-auth-token-secret", bodyText, StringComparison.OrdinalIgnoreCase);
    }

    // ── PUT InApp config ──────────────────────────────────────────────────────

    [Fact]
    public async Task PutInAppConfig_Enabled_SavesSuccessfully()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/in-app", new { isEnabled = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("succeeded").GetBoolean());
    }

    // ── Test email still works ────────────────────────────────────────────────

    [Fact]
    public async Task TestEmail_AfterDbConfigSaved_ReturnsOkWithSkippedOrResult()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        // Save a disabled config
        await ClientWithToken(adminToken).PutAsJsonAsync("/api/admin/notifications/config/email", new
        {
            isEnabled = false
        });

        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync("/api/admin/notifications/config/email/test",
                new { toAddress = "test@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("succeeded", out _));
        Assert.True(body.TryGetProperty("wasSkipped", out _));
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PutEmailConfig_CalledTwice_SecondCallUpdates()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        await ClientWithToken(adminToken).PutAsJsonAsync("/api/admin/notifications/config/email", new
        {
            isEnabled = false,
            host = "smtp1.example.com",
            port = 587,
            fromAddress = "a@example.com"
        });

        var r2 = await ClientWithToken(adminToken).PutAsJsonAsync("/api/admin/notifications/config/email", new
        {
            isEnabled = false,
            host = "smtp2.example.com",
            port = 465,
            fromAddress = "b@example.com"
        });

        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        var body = await GetConfigBodyAsync(adminToken);
        Assert.Equal("smtp2.example.com", body.GetProperty("email").GetProperty("host").GetString());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<JsonElement> GetConfigBodyAsync(string adminToken)
    {
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/notifications/config");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
