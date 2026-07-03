using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class AdminOnboardingEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminOnboardingEndpointTests(ApiTestFactory factory) => _factory = factory;

    // ── GET /api/admin/onboarding/flows ──────────────────────────────────────

    [Fact]
    public async Task ListFlows_AsAdmin_ReturnsOk()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/admin/onboarding/flows");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact]
    public async Task ListFlows_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/onboarding/flows");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GET /api/admin/onboarding/flow ───────────────────────────────────────

    [Fact]
    public async Task GetFlow_WhenActiveFlowExists_ReturnsFlow()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var hasActive = db.OnboardingFlowDefinitions.Any(f => f.IsActive);
        if (!hasActive) return; // skip gracefully if no active flow seeded

        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/admin/onboarding/flow");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("flowId", out _) || body.TryGetProperty("FlowId", out _));
    }

    // ── POST /api/admin/onboarding/flows ─────────────────────────────────────

    [Fact]
    public async Task CreateFlow_ValidRequest_Returns201()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.PostAsJsonAsync("/api/admin/onboarding/flows",
            new { name = $"Test Flow {Guid.NewGuid():N}", version = 99 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("flowId", out _) || body.TryGetProperty("FlowId", out _));
    }

    [Fact]
    public async Task CreateFlow_EmptyName_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.PostAsJsonAsync("/api/admin/onboarding/flows",
            new { name = "", version = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── POST step + DELETE step ───────────────────────────────────────────────

    [Fact]
    public async Task AddStep_ThenDeleteStep_Succeeds()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var createResp = await client.PostAsJsonAsync("/api/admin/onboarding/flows",
            new { name = $"StepFlow {Guid.NewGuid():N}", version = 50 });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var flowBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var flowId = GetFlowId(flowBody);

        var addResp = await client.PostAsJsonAsync($"/api/admin/onboarding/flows/{flowId}/steps", new
        {
            stepKey = "welcome",
            title = "Welcome",
            description = (string?)null,
            stepType = "Welcome",
            requirementType = "AdminConfigured",
            answerMapping = "None",
            stepOrder = 1,
            isEnabled = true,
            options = (object?)null
        });
        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);

        var delResp = await client.DeleteAsync($"/api/admin/onboarding/flows/{flowId}/steps/welcome");
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);
    }

    [Fact]
    public async Task AddStep_SingleChoiceWithOptions_ContentIsPopulatedAndOptionsDriven()
    {
        // Unified Question-Schema Phase 6: adding a generic-type step keeps the shadow Content
        // in sync with whatever Options the admin submitted.
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var createResp = await client.PostAsJsonAsync("/api/admin/onboarding/flows",
            new { name = $"ContentFlow {Guid.NewGuid():N}", version = 60 });
        var flowBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var flowId = GetFlowId(flowBody);

        var addResp = await client.PostAsJsonAsync($"/api/admin/onboarding/flows/{flowId}/steps", new
        {
            stepKey = "quick_check",
            title = "Quick check",
            description = (string?)null,
            stepType = "SingleChoice",
            requirementType = "AdminConfigured",
            answerMapping = "None",
            stepOrder = 1,
            isEnabled = true,
            options = new[] { new { key = "a", label = "Option A" }, new { key = "b", label = "Option B" } },
        });

        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);
        var body = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var content = body.GetProperty("content");
        Assert.Equal("single_choice", content.GetProperty("type").GetString());
        Assert.Equal(2, content.GetProperty("choices").GetArrayLength());
    }

    // ── POST activate ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ActivateFlow_WithNoSteps_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var createResp = await client.PostAsJsonAsync("/api/admin/onboarding/flows",
            new { name = $"EmptyFlow {Guid.NewGuid():N}", version = 77 });
        var flowBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var flowId = GetFlowId(flowBody);

        var activateResp = await client.PostAsync($"/api/admin/onboarding/flows/{flowId}/activate", null);
        Assert.Equal(HttpStatusCode.BadRequest, activateResp.StatusCode);
    }

    // ── PUT reorder ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderSteps_ValidOrder_ReturnsNoContent()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var createResp = await client.PostAsJsonAsync("/api/admin/onboarding/flows",
            new { name = $"ReorderFlow {Guid.NewGuid():N}", version = 55 });
        var flowBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var flowId = GetFlowId(flowBody);

        await client.PostAsJsonAsync($"/api/admin/onboarding/flows/{flowId}/steps", new
        {
            stepKey = "step-a", title = "Step A", description = (string?)null,
            stepType = "Welcome", requirementType = "AdminConfigured", answerMapping = "None",
            stepOrder = 1, isEnabled = true, options = (object?)null
        });
        await client.PostAsJsonAsync($"/api/admin/onboarding/flows/{flowId}/steps", new
        {
            stepKey = "step-b", title = "Step B", description = (string?)null,
            stepType = "FreeText", requirementType = "AdminConfigured", answerMapping = "None",
            stepOrder = 2, isEnabled = true, options = (object?)null
        });

        var reorderResp = await client.PutAsJsonAsync(
            $"/api/admin/onboarding/flows/{flowId}/steps/reorder",
            new { stepKeyOrder = new[] { "step-b", "step-a" } });

        Assert.Equal(HttpStatusCode.NoContent, reorderResp.StatusCode);
    }

    // ── Reserved step key validation ──────────────────────────────────────────

    [Theory]
    [InlineData("reorder")]
    [InlineData("activate")]
    [InlineData("flows")]
    [InlineData("delete")]
    public async Task AddStep_WithReservedKey_Returns400(string reservedKey)
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var createResp = await client.PostAsJsonAsync("/api/admin/onboarding/flows",
            new { name = $"ReservedKeyFlow {Guid.NewGuid():N}", version = 11 });
        var flowBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var flowId = GetFlowId(flowBody);

        var addResp = await client.PostAsJsonAsync($"/api/admin/onboarding/flows/{flowId}/steps", new
        {
            stepKey = reservedKey,
            title = "Test",
            description = (string?)null,
            stepType = "Welcome",
            requirementType = "AdminConfigured",
            answerMapping = "None",
            stepOrder = 1,
            isEnabled = true,
            options = (object?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, addResp.StatusCode);
        var body = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("reserved", body.GetProperty("error").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private System.Net.Http.HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Guid GetFlowId(JsonElement body)
    {
        if (body.TryGetProperty("flowId", out var fid)) return fid.GetGuid();
        return body.GetProperty("FlowId").GetGuid();
    }
}
