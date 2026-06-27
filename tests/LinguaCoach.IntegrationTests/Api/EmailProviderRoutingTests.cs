using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 12A email provider routing validation.
/// Verifies that:
///   - Resend/SendGrid providers skip SMTP host/port validation
///   - Invalid/unknown providers return 400
///   - Null/empty provider defaults to SMTP (requires host when enabled)
///   - Provider names persist correctly after save
///   - Secrets are never returned in responses
/// </summary>
public sealed class EmailProviderRoutingTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public EmailProviderRoutingTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<JsonElement> GetEmailConfigAsync(string adminToken)
    {
        var response = await ClientWithToken(adminToken).GetAsync("/api/admin/notifications/config");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("email");
    }

    // ── Resend skips host/port validation ─────────────────────────────────────

    [Fact]
    public async Task PutEmailConfig_ResendProvider_EnabledWithoutHost_ReturnsOk()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = true,
                provider = "Resend",
                fromAddress = "noreply@example.com",
                // No host, no port — Resend does not require them
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("succeeded").GetBoolean());
    }

    [Fact]
    public async Task PutEmailConfig_ResendProvider_CaseInsensitive_ReturnsOk()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = true,
                provider = "resend",
                fromAddress = "noreply@example.com",
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── SendGrid skips host/port validation ───────────────────────────────────

    [Fact]
    public async Task PutEmailConfig_SendGridProvider_EnabledWithoutHost_ReturnsOk()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = true,
                provider = "SendGrid",
                fromAddress = "noreply@example.com",
                // No host, no port — SendGrid does not require them
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("succeeded").GetBoolean());
    }

    [Fact]
    public async Task PutEmailConfig_SendGridProvider_CaseInsensitive_ReturnsOk()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = true,
                provider = "SENDGRID",
                fromAddress = "noreply@example.com",
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Invalid provider fails with 400 ───────────────────────────────────────

    [Fact]
    public async Task PutEmailConfig_InvalidProvider_ReturnsBadRequest()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = true,
                provider = "Mailgun",
                fromAddress = "noreply@example.com",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var bodyText = await response.Content.ReadAsStringAsync();
        Assert.Contains("Mailgun", bodyText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PutEmailConfig_UnknownProvider_ReturnsBadRequest()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = true,
                provider = "NotARealProvider",
                fromAddress = "noreply@example.com",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Null/empty provider defaults to SMTP → still requires host ────────────

    [Fact]
    public async Task PutEmailConfig_NullProvider_Enabled_StillRequiresSmtpHost()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = true,
                provider = (string?)null,
                fromAddress = "noreply@example.com",
                // No host — null provider defaults to SMTP, which requires host
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutEmailConfig_EmptyProvider_Enabled_StillRequiresSmtpHost()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = true,
                provider = "",
                fromAddress = "noreply@example.com",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Provider name persists after save ─────────────────────────────────────

    [Fact]
    public async Task PutEmailConfig_ResendProvider_PersistsProviderName()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        await ClientWithToken(adminToken).PutAsJsonAsync("/api/admin/notifications/config/email", new
        {
            isEnabled = false,
            provider = "Resend",
            fromAddress = "noreply@example.com",
        });

        var emailConfig = await GetEmailConfigAsync(adminToken);
        Assert.Equal("Resend", emailConfig.GetProperty("provider").GetString());
    }

    [Fact]
    public async Task PutEmailConfig_SendGridProvider_PersistsProviderName()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        await ClientWithToken(adminToken).PutAsJsonAsync("/api/admin/notifications/config/email", new
        {
            isEnabled = false,
            provider = "SendGrid",
            fromAddress = "noreply@example.com",
        });

        var emailConfig = await GetEmailConfigAsync(adminToken);
        Assert.Equal("SendGrid", emailConfig.GetProperty("provider").GetString());
    }

    [Fact]
    public async Task PutEmailConfig_SmtpProvider_PersistsProviderName()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        await ClientWithToken(adminToken).PutAsJsonAsync("/api/admin/notifications/config/email", new
        {
            isEnabled = false,
            provider = "Smtp",
        });

        var emailConfig = await GetEmailConfigAsync(adminToken);
        Assert.Equal("Smtp", emailConfig.GetProperty("provider").GetString());
    }

    // ── API key / secret safety ───────────────────────────────────────────────

    [Fact]
    public async Task PutEmailConfig_ResendWithApiKey_KeyNotReturnedInResponse()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = false,
                provider = "Resend",
                fromAddress = "noreply@example.com",
                newSecret = "re_supersecret_resend_key_abc123",
            });

        var bodyText = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("re_supersecret_resend_key_abc123", bodyText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("newSecret", bodyText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PutEmailConfig_SendGridWithApiKey_KeyNotReturnedInResponse()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PutAsJsonAsync("/api/admin/notifications/config/email", new
            {
                isEnabled = false,
                provider = "SendGrid",
                fromAddress = "noreply@example.com",
                newSecret = "SG.supersecret_sendgrid_key_xyz789",
            });

        var bodyText = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SG.supersecret_sendgrid_key_xyz789", bodyText, StringComparison.OrdinalIgnoreCase);
    }
}
