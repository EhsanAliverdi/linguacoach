using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase I2C: the "review-scaffold-generation" and "practice-gym-review-scaffold-pilot" feature
/// gate groups this file originally exercised were removed along with the readiness pool — see
/// docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md. Tests that used them were
/// rewritten against "activity-feedback-policy" (the only remaining group on the same
/// ReadinessPoolOverride backing store). No remaining group has RequiresConfirmation=true, so the
/// high-risk-confirmation coverage (Update_HighRiskWithoutConfirmation_Returns400/
/// Update_HighRiskWithConfirmation_Succeeds) has no group left to exercise it against and was
/// removed — flagged as a residual coverage gap in the review doc.
/// </summary>
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

        Assert.Contains("activity-feedback-policy", keys);
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
    public async Task GetByKey_ActivityFeedbackPolicy_IncludesSourceAndDefault()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/runtime-settings/feature-gates/activity-feedback-policy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<JsonElement>();
        var settings = group.GetProperty("settings").EnumerateArray().ToList();
        var todayPolicy = settings.First(s => s.GetProperty("key").GetString() == "ActivityFeedback.TodayPolicy");

        Assert.True(todayPolicy.TryGetProperty("effectiveValueJson", out _));
        Assert.True(todayPolicy.TryGetProperty("defaultValueJson", out _));
        Assert.True(todayPolicy.TryGetProperty("valueSource", out _));
        Assert.True(todayPolicy.GetProperty("isEditableAtRuntime").GetBoolean());
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
            "/api/admin/runtime-settings/feature-gates/activity-feedback-policy/settings",
            new
            {
                values = new Dictionary<string, object>
                {
                    ["ActivityFeedback.TodayPolicy"] = "Required",
                },
                reason = "Integration test tuning today feedback policy.",
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<JsonElement>();
        var setting = group.GetProperty("settings").EnumerateArray()
            .First(s => s.GetProperty("key").GetString() == "ActivityFeedback.TodayPolicy");
        Assert.Equal("\"Required\"", setting.GetProperty("effectiveValueJson").GetString());
        Assert.Equal("databaseOverride", setting.GetProperty("valueSource").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var audited = db.AdminAuditLogs.Any(a =>
            a.Action == "UpdateFeatureGate" && a.EntityId == "ActivityFeedback.TodayPolicy");
        Assert.True(audited);
    }

    [Fact]
    public async Task Update_DisallowedValue_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/activity-feedback-policy/settings",
            new
            {
                values = new Dictionary<string, object> { ["ActivityFeedback.TodayPolicy"] = "NotAllowed" },
                reason = "Should be rejected.",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_UnknownKey_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/activity-feedback-policy/settings",
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
    public async Task Update_MissingReason_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PutAsJsonAsync(
            "/api/admin/runtime-settings/feature-gates/activity-feedback-policy/settings",
            new
            {
                values = new Dictionary<string, object> { ["ActivityFeedback.TodayPolicy"] = "Off" },
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
            "/api/admin/runtime-settings/feature-gates/activity-feedback-policy/settings",
            new
            {
                values = new Dictionary<string, object> { ["ActivityFeedback.PracticeGymPolicy"] = "Required" },
                reason = "Set custom policy before reset test.",
            });

        var resetRequest = new HttpRequestMessage(
            HttpMethod.Delete, "/api/admin/runtime-settings/feature-gates/activity-feedback-policy/override")
        {
            Content = JsonContent.Create(new { reason = "Rolling back to default for test." }),
        };
        var resetResponse = await client.SendAsync(resetRequest);

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        var group = await resetResponse.Content.ReadFromJsonAsync<JsonElement>();
        var setting = group.GetProperty("settings").EnumerateArray()
            .First(s => s.GetProperty("key").GetString() == "ActivityFeedback.PracticeGymPolicy");
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
