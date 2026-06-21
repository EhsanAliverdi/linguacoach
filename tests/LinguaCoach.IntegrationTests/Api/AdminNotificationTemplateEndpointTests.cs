using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 10W-5D notification template endpoints.
/// GET/POST/PUT /api/admin/notifications/templates
/// POST /api/admin/notifications/templates/{id}/deactivate
/// POST /api/admin/notifications/templates/{id}/preview
/// </summary>
public sealed class AdminNotificationTemplateEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminNotificationTemplateEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── Auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ListTemplates_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/notifications/templates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListTemplates_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"tpl403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/admin/notifications/templates");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateTemplate_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/admin/notifications/templates", new { templateKey = "x", channel = "InApp", name = "X", body = "X", title = "X" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTemplate_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"tplc403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .PostAsJsonAsync("/api/admin/notifications/templates", new { templateKey = "x", channel = "InApp", name = "X", body = "X", title = "X" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListTemplates_AsAdmin_ReturnsOkWithItems()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/notifications/templates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out _));
        Assert.True(body.TryGetProperty("totalCount", out _));
    }

    [Fact]
    public async Task ListTemplates_ReturnsPagedStructure()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/notifications/templates?pageSize=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out _));
        Assert.True(body.TryGetProperty("totalCount", out _));
        Assert.True(body.TryGetProperty("totalPages", out _));
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTemplate_InApp_RequiresTitle()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PostAsJsonAsync("/api/admin/notifications/templates", new
        {
            templateKey = $"test.inapp_{Guid.NewGuid():N}",
            channel = "InApp",
            name = "Test",
            body = "Hello {{Name}}",
            // No title — should fail
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTemplate_Email_RequiresSubject()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PostAsJsonAsync("/api/admin/notifications/templates", new
        {
            templateKey = $"test.email_{Guid.NewGuid():N}",
            channel = "Email",
            name = "Test",
            body = "Hello {{Name}}",
            // No subject — should fail
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTemplate_MissingBody_ReturnsBadRequest()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PostAsJsonAsync("/api/admin/notifications/templates", new
        {
            templateKey = $"test.nobody_{Guid.NewGuid():N}",
            channel = "InApp",
            name = "Test",
            title = "Hi",
            // body missing
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTemplate_InApp_Succeeds()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var key = $"test.inapp.{Guid.NewGuid():N}";
        var response = await ClientWithToken(token).PostAsJsonAsync("/api/admin/notifications/templates", new
        {
            templateKey = key,
            channel = "InApp",
            name = "Test InApp",
            body = "Hello {{Name}}",
            title = "Welcome {{Name}}",
            category = "Admin",
            severity = "Info",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(key, body.GetProperty("templateKey").GetString());
        Assert.Equal("InApp", body.GetProperty("channel").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.Equal(1, body.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task CreateTemplate_Email_Succeeds()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var key = $"test.email.{Guid.NewGuid():N}";
        var response = await ClientWithToken(token).PostAsJsonAsync("/api/admin/notifications/templates", new
        {
            templateKey = key,
            channel = "Email",
            name = "Test Email",
            body = "<p>Hello {{Name}}</p>",
            subject = "Welcome {{Name}}",
            category = "Account",
            severity = "Info",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Email", body.GetProperty("channel").GetString());
        Assert.Equal("Welcome {{Name}}", body.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task CreateTemplate_DuplicateActiveKeyChannel_ReturnsConflict()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var key = $"test.dup.{Guid.NewGuid():N}";
        var payload = new
        {
            templateKey = key,
            channel = "InApp",
            name = "Dup Test",
            body = "Body",
            title = "Title",
        };

        var first = await ClientWithToken(token).PostAsJsonAsync("/api/admin/notifications/templates", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await ClientWithToken(token).PostAsJsonAsync("/api/admin/notifications/templates", payload);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // ── Get ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTemplate_ReturnsCorrectItem()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var key = $"test.get.{Guid.NewGuid():N}";
        var created = await (await ClientWithToken(token).PostAsJsonAsync("/api/admin/notifications/templates", new
        {
            templateKey = key, channel = "InApp", name = "Get Test", body = "B", title = "T",
        })).Content.ReadFromJsonAsync<JsonElement>();

        var id = created.GetProperty("id").GetString();
        var response = await ClientWithToken(token).GetAsync($"/api/admin/notifications/templates/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(key, body.GetProperty("templateKey").GetString());
    }

    [Fact]
    public async Task GetTemplate_UnknownId_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync($"/api/admin/notifications/templates/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTemplate_BumpsVersion()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var key = $"test.update.{Guid.NewGuid():N}";
        var created = await (await ClientWithToken(token).PostAsJsonAsync("/api/admin/notifications/templates", new
        {
            templateKey = key, channel = "InApp", name = "Original", body = "B", title = "T",
        })).Content.ReadFromJsonAsync<JsonElement>();

        var id = created.GetProperty("id").GetString();
        var updated = await ClientWithToken(token).PutAsJsonAsync($"/api/admin/notifications/templates/{id}", new
        {
            name = "Updated Name", body = "New body", title = "New title", category = "Admin", severity = "Info",
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("version").GetInt32());
        Assert.Equal("Updated Name", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task UpdateTemplate_UnknownId_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/notifications/templates/{Guid.NewGuid()}",
            new { name = "X", body = "B", title = "T", category = "Admin", severity = "Info" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Deactivate ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateTemplate_SetsIsActiveFalse()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var key = $"test.deact.{Guid.NewGuid():N}";
        var created = await (await ClientWithToken(token).PostAsJsonAsync("/api/admin/notifications/templates", new
        {
            templateKey = key, channel = "InApp", name = "Deact Test", body = "B", title = "T",
        })).Content.ReadFromJsonAsync<JsonElement>();

        var id = created.GetProperty("id").GetString();
        var deact = await ClientWithToken(token).PostAsync($"/api/admin/notifications/templates/{id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deact.StatusCode);

        var get = await ClientWithToken(token).GetAsync($"/api/admin/notifications/templates/{id}");
        var body = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task DeactivateTemplate_UnknownId_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PostAsync(
            $"/api/admin/notifications/templates/{Guid.NewGuid()}/deactivate", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task PreviewTemplate_RendersVariables()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var key = $"test.preview.{Guid.NewGuid():N}";
        var created = await (await ClientWithToken(token).PostAsJsonAsync("/api/admin/notifications/templates", new
        {
            templateKey = key, channel = "InApp",
            name = "Preview Test", body = "Hello {{Name}}, your code is {{Code}}.", title = "Hi {{Name}}",
        })).Content.ReadFromJsonAsync<JsonElement>();

        var id = created.GetProperty("id").GetString();
        var preview = await ClientWithToken(token).PostAsJsonAsync(
            $"/api/admin/notifications/templates/{id}/preview",
            new { variables = new Dictionary<string, string> { ["Name"] = "Alice", ["Code"] = "XYZ" } });

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var body = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("succeeded").GetBoolean());
        Assert.Equal("Hello Alice, your code is XYZ.", body.GetProperty("renderedBody").GetString());
        Assert.Equal("Hi Alice", body.GetProperty("renderedTitle").GetString());
        Assert.Equal(0, body.GetProperty("missingVariables").GetArrayLength());
    }

    [Fact]
    public async Task PreviewTemplate_MissingVariables_ReportsThemAndLeavesPlaceholder()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var key = $"test.prevmiss.{Guid.NewGuid():N}";
        var created = await (await ClientWithToken(token).PostAsJsonAsync("/api/admin/notifications/templates", new
        {
            templateKey = key, channel = "InApp",
            name = "Preview Miss", body = "Hello {{Name}}, your code is {{Code}}.", title = "Hi",
        })).Content.ReadFromJsonAsync<JsonElement>();

        var id = created.GetProperty("id").GetString();
        var preview = await ClientWithToken(token).PostAsJsonAsync(
            $"/api/admin/notifications/templates/{id}/preview",
            new { variables = new Dictionary<string, string> { ["Name"] = "Bob" } });

        var body = await preview.Content.ReadFromJsonAsync<JsonElement>();
        // Missing Code — placeholder left visible, reported
        Assert.Contains("{{Code}}", body.GetProperty("renderedBody").GetString());
        Assert.Equal(1, body.GetProperty("missingVariables").GetArrayLength());
        Assert.Equal("Code", body.GetProperty("missingVariables")[0].GetString());
    }

    [Fact]
    public async Task PreviewTemplate_DoesNotQueueNotification()
    {
        // Count outbox before and after — preview must not add items
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var key = $"test.prevnoq.{Guid.NewGuid():N}";
        var created = await (await ClientWithToken(token).PostAsJsonAsync("/api/admin/notifications/templates", new
        {
            templateKey = key, channel = "Email",
            name = "Preview NoQueue", body = "Body {{X}}", subject = "Subj {{X}}",
        })).Content.ReadFromJsonAsync<JsonElement>();

        var id = created.GetProperty("id").GetString();

        var beforeOutbox = await ClientWithToken(token).GetAsync("/api/admin/notifications/outbox");
        var beforeBody = await beforeOutbox.Content.ReadFromJsonAsync<JsonElement>();
        var beforeCount = beforeBody.GetProperty("totalCount").GetInt32();

        await ClientWithToken(token).PostAsJsonAsync(
            $"/api/admin/notifications/templates/{id}/preview",
            new { variables = new Dictionary<string, string> { ["X"] = "test" } });

        var afterOutbox = await ClientWithToken(token).GetAsync("/api/admin/notifications/outbox");
        var afterBody = await afterOutbox.Content.ReadFromJsonAsync<JsonElement>();
        var afterCount = afterBody.GetProperty("totalCount").GetInt32();

        Assert.Equal(beforeCount, afterCount);
    }

    [Fact]
    public async Task PreviewTemplate_UnknownId_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PostAsJsonAsync(
            $"/api/admin/notifications/templates/{Guid.NewGuid()}/preview",
            new { variables = new Dictionary<string, string>() });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
