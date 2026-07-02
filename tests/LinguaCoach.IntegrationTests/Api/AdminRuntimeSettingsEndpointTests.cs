using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class AdminRuntimeSettingsEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminRuntimeSettingsEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/runtime-settings/feature-gates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"rts403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/admin/runtime-settings/feature-gates");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── list / detail ────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsExpectedGroups()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/runtime-settings/feature-gates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var groups = await response.Content.ReadFromJsonAsync<JsonElement>();
        var keys = groups.EnumerateArray().Select(g => g.GetProperty("groupKey").GetString()).ToList();

        Assert.Contains("review-scaffold-generation", keys);
        Assert.Contains("practice-gym-review-scaffold-pilot", keys);
        Assert.Contains("lesson-generation-buffer", keys);
        Assert.Contains("ai-signal-safety-speaking", keys);
        Assert.Contains("ai-signal-safety-writing", keys);
        Assert.Contains("learning-plan-regeneration", keys);
    }

    [Fact]
    public async Task GetByKey_UnknownKey_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/runtime-settings/feature-gates/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByKey_PracticeGymPilot_IncludesSourceAndDefault()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/runtime-settings/feature-gates/practice-gym-review-scaffold-pilot");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<JsonElement>();
        var settings = group.GetProperty("settings").EnumerateArray().ToList();
        var pilotEnabled = settings.First(s => s.GetProperty("key").GetString() == "ReadinessPool.PracticeGymPilotEnabled");

        Assert.True(pilotEnabled.TryGetProperty("effectiveValueJson", out _));
        Assert.True(pilotEnabled.TryGetProperty("defaultValueJson", out _));
        Assert.True(pilotEnabled.TryGetProperty("valueSource", out _));
        Assert.True(pilotEnabled.GetProperty("isEditableAtRuntime").GetBoolean());
    }

    [Fact]
    public async Task GetByKey_AiSignalSafety_IsReadOnlyAndHasNoSecrets()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/runtime-settings/feature-gates/ai-signal-safety-speaking");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(group.GetProperty("isReadOnly").GetBoolean());

        foreach (var setting in group.GetProperty("settings").EnumerateArray())
        {
            Assert.False(setting.GetProperty("isEditableAtRuntime").GetBoolean());
        }

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("apiKey", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ── update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ValidValue_PersistsOverrideAndAudits()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/practice-gym-review-scaffold-pilot/settings",
            new
            {
                values = new Dictionary<string, object>
                {
                    ["ReadinessPool.MaxStudentVisibleScaffoldSuggestions"] = 3,
                },
                reason = "Integration test tuning max visible suggestions.",
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<JsonElement>();
        var setting = group.GetProperty("settings").EnumerateArray()
            .First(s => s.GetProperty("key").GetString() == "ReadinessPool.MaxStudentVisibleScaffoldSuggestions");
        Assert.Equal("3", setting.GetProperty("effectiveValueJson").GetString());
        Assert.Equal("databaseOverride", setting.GetProperty("valueSource").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var audited = db.AdminAuditLogs.Any(a =>
            a.Action == "UpdateFeatureGate" && a.EntityId == "ReadinessPool.MaxStudentVisibleScaffoldSuggestions");
        Assert.True(audited);
    }

    [Fact]
    public async Task Update_OutOfRangeValue_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/practice-gym-review-scaffold-pilot/settings",
            new
            {
                values = new Dictionary<string, object> { ["ReadinessPool.MaxStudentVisibleScaffoldSuggestions"] = 99 },
                reason = "Should be rejected.",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownKey_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/practice-gym-review-scaffold-pilot/settings",
            new
            {
                values = new Dictionary<string, object> { ["NotARealKey"] = true },
                reason = "Should be rejected.",
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReadOnlyGate_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/ai-signal-safety-speaking/settings",
            new
            {
                values = new Dictionary<string, object> { ["Speaking.AllowCefrUpdate"] = true },
                reason = "Should be rejected.",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_HighRiskWithoutConfirmation_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/review-scaffold-generation/settings",
            new
            {
                values = new Dictionary<string, object> { ["ReadinessPool.EnableReviewScaffoldGeneration"] = true },
                reason = "Trying to enable without confirmation.",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_HighRiskWithConfirmation_Succeeds()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/review-scaffold-generation/settings",
            new
            {
                values = new Dictionary<string, object> { ["ReadinessPool.EnableReviewScaffoldGeneration"] = true },
                reason = "Confirmed high-risk enable for test.",
                confirmationText = "CONFIRM",
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_MissingReason_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/practice-gym-review-scaffold-pilot/settings",
            new
            {
                values = new Dictionary<string, object> { ["ReadinessPool.MaxStudentVisibleScaffoldSuggestions"] = 1 },
                reason = "",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reset_AfterOverride_RestoresDefaultAndAudits()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        await client.PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/practice-gym-review-scaffold-pilot/settings",
            new
            {
                values = new Dictionary<string, object> { ["ReadinessPool.PracticeGymPilotLabel"] = "Custom label" },
                reason = "Set custom label before reset test.",
            });

        var resetRequest = new HttpRequestMessage(
            HttpMethod.Delete, "/api/admin/runtime-settings/feature-gates/practice-gym-review-scaffold-pilot/override")
        {
            Content = JsonContent.Create(new { reason = "Rolling back to default for test." }),
        };
        var resetResponse = await client.SendAsync(resetRequest);

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        var group = await resetResponse.Content.ReadFromJsonAsync<JsonElement>();
        var setting = group.GetProperty("settings").EnumerateArray()
            .First(s => s.GetProperty("key").GetString() == "ReadinessPool.PracticeGymPilotLabel");
        Assert.Equal("appSettings", setting.GetProperty("valueSource").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.True(db.AdminAuditLogs.Any(a => a.Action == "ResetFeatureGateOverride"));
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
