using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class DiagnosticsEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public DiagnosticsEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Status_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/diagnostics/status");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Status_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"diag403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/admin/diagnostics/status");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── AI provider status reflects DB category config, not env var ───────────

    [Fact]
    public async Task Status_WhenDefaultLlmCategoryIsFake_ReportsAiNotConfigured()
    {
        // DefaultAiSeeder seeds llm.default with provider="fake"/model="fake".
        // IsConfigured returns false for fake providers, so diagnostics should report not configured.
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/diagnostics/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ai = body.GetProperty("ai");
        Assert.False(ai.GetProperty("providerConfigured").GetBoolean());
    }

    [Fact]
    public async Task Status_WhenDefaultLlmCategoryHasRealProvider_ReportsAiConfigured()
    {
        // Update the seeded llm.default row to a real provider so IsConfigured returns true.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var category = db.AiConfigCategories.First(c => c.CategoryKey == "llm.default");
        category.Update("gemini", "gemini-2.5-flash-lite");
        await db.SaveChangesAsync();

        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/diagnostics/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ai = body.GetProperty("ai");
        Assert.True(ai.GetProperty("providerConfigured").GetBoolean());
        Assert.Equal("gemini", ai.GetProperty("activeProvider").GetString());

        // Reset to fake so other tests are not affected.
        category.Update("fake", "fake");
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Status_ReturnsExpectedTopLevelFields()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).GetAsync("/api/admin/diagnostics/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("environment", out _));
        Assert.True(body.TryGetProperty("version", out _));
        Assert.True(body.TryGetProperty("database", out var db));
        Assert.True(db.TryGetProperty("reachable", out var reachable));
        Assert.True(reachable.GetBoolean()); // SQLite in-memory is always reachable
        Assert.True(body.TryGetProperty("ai", out _));
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
