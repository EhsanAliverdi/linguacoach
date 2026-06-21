using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class AiPricingOverrideEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AiPricingOverrideEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── Auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ListOverrides_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/ai/pricing/overrides");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListOverrides_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"povr403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token).GetAsync("/api/admin/ai/pricing/overrides");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateOverride_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/admin/ai/pricing/overrides", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Create override ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOverride_ValidPayload_Returns200WithOverride()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var payload = new
        {
            providerName = "openai",
            modelName = "gpt-4o-test-create",
            inputPricePer1KTokens = 0.005m,
            outputPricePer1KTokens = 0.015m,
            currency = "USD",
            effectiveFromUtc = DateTime.UtcNow.AddMinutes(-1),
        };

        var response = await ClientWithToken(token).PostAsJsonAsync("/api/admin/ai/pricing/overrides", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("openai", body.GetProperty("providerName").GetString());
        Assert.Equal("gpt-4o-test-create", body.GetProperty("modelName").GetString());
        Assert.Equal(0.005m, body.GetProperty("inputPricePer1KTokens").GetDecimal());
        Assert.Equal(0.015m, body.GetProperty("outputPricePer1KTokens").GetDecimal());
        Assert.Equal("USD", body.GetProperty("currency").GetString());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task CreateOverride_NegativeInputPrice_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var payload = new
        {
            providerName = "openai",
            modelName = "gpt-4o",
            inputPricePer1KTokens = -0.001m,
            outputPricePer1KTokens = 0.01m,
            currency = "USD",
            effectiveFromUtc = DateTime.UtcNow,
        };

        var response = await ClientWithToken(token).PostAsJsonAsync("/api/admin/ai/pricing/overrides", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOverride_EffectiveToBeforeFrom_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var now = DateTime.UtcNow;
        var payload = new
        {
            providerName = "openai",
            modelName = "gpt-4o",
            inputPricePer1KTokens = 0.002m,
            outputPricePer1KTokens = 0.008m,
            currency = "USD",
            effectiveFromUtc = now,
            effectiveToUtc = now.AddMinutes(-10),
        };

        var response = await ClientWithToken(token).PostAsJsonAsync("/api/admin/ai/pricing/overrides", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── List overrides ────────────────────────────────────────────────────────

    [Fact]
    public async Task ListOverrides_AsAdmin_ReturnsCreatedOverride()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var modelName = $"gpt-list-{Guid.NewGuid():N}";
        await ClientWithToken(token).PostAsJsonAsync("/api/admin/ai/pricing/overrides", new
        {
            providerName = "openai",
            modelName,
            inputPricePer1KTokens = 0.003m,
            outputPricePer1KTokens = 0.009m,
            currency = "USD",
            effectiveFromUtc = DateTime.UtcNow.AddMinutes(-1),
        });

        var response = await ClientWithToken(token).GetAsync("/api/admin/ai/pricing/overrides");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Contains(rows, r => r.GetProperty("modelName").GetString() == modelName);
    }

    // ── Update override ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateOverride_ValidPayload_UpdatesPrices()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var modelName = $"gpt-upd-{Guid.NewGuid():N}";

        var created = await (await ClientWithToken(token).PostAsJsonAsync("/api/admin/ai/pricing/overrides", new
        {
            providerName = "openai",
            modelName,
            inputPricePer1KTokens = 0.001m,
            outputPricePer1KTokens = 0.002m,
            currency = "USD",
            effectiveFromUtc = DateTime.UtcNow.AddMinutes(-1),
        })).Content.ReadFromJsonAsync<JsonElement>();

        var id = created.GetProperty("id").GetGuid();

        var updateResponse = await ClientWithToken(token).PutAsJsonAsync($"/api/admin/ai/pricing/overrides/{id}", new
        {
            inputPricePer1KTokens = 0.007m,
            outputPricePer1KTokens = 0.021m,
            currency = "USD",
            effectiveFromUtc = DateTime.UtcNow.AddMinutes(-1),
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var body = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0.007m, body.GetProperty("inputPricePer1KTokens").GetDecimal());
        Assert.Equal(0.021m, body.GetProperty("outputPricePer1KTokens").GetDecimal());
    }

    [Fact]
    public async Task UpdateOverride_NonExistentId_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).PutAsJsonAsync(
            $"/api/admin/ai/pricing/overrides/{Guid.NewGuid()}",
            new { inputPricePer1KTokens = 0.001m, outputPricePer1KTokens = 0.001m, currency = "USD", effectiveFromUtc = DateTime.UtcNow });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Deactivate override ───────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateOverride_ExistingOverride_Returns204AndDeactivates()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var modelName = $"gpt-deact-{Guid.NewGuid():N}";

        var created = await (await ClientWithToken(token).PostAsJsonAsync("/api/admin/ai/pricing/overrides", new
        {
            providerName = "openai",
            modelName,
            inputPricePer1KTokens = 0.001m,
            outputPricePer1KTokens = 0.002m,
            currency = "USD",
            effectiveFromUtc = DateTime.UtcNow.AddMinutes(-1),
        })).Content.ReadFromJsonAsync<JsonElement>();

        var id = created.GetProperty("id").GetGuid();
        var deleteResponse = await ClientWithToken(token).DeleteAsync($"/api/admin/ai/pricing/overrides/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify IsActive=false in list
        var list = (await (await ClientWithToken(token).GetAsync("/api/admin/ai/pricing/overrides"))
            .Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var row = list.FirstOrDefault(r => r.GetProperty("id").GetGuid() == id);
        Assert.NotEqual(default, row);
        Assert.False(row.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task DeactivateOverride_NonExistentId_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(token).DeleteAsync($"/api/admin/ai/pricing/overrides/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Resolver tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolver_ReturnsDbOverride_WhenActiveOverrideExists()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var modelName = $"gpt-resolver-{Guid.NewGuid():N}";

        await ClientWithToken(token).PostAsJsonAsync("/api/admin/ai/pricing/overrides", new
        {
            providerName = "openai",
            modelName,
            inputPricePer1KTokens = 0.099m,
            outputPricePer1KTokens = 0.199m,
            currency = "USD",
            effectiveFromUtc = DateTime.UtcNow.AddMinutes(-1),
        });

        using var scope = _factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IAiPricingResolver>();
        var pricing = await resolver.ResolveAsync("openai", modelName);

        Assert.NotNull(pricing);
        Assert.Equal(0.099m, pricing!.InputPer1KTokens);
        Assert.Equal(0.199m, pricing.OutputPer1KTokens);
    }

    [Fact]
    public async Task Resolver_FallsBackToConfig_WhenNoActiveOverride()
    {
        using var scope = _factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IAiPricingResolver>();

        // gpt-4o is in appsettings.json with known values
        var pricing = await resolver.ResolveAsync("OpenAI", "gpt-4o");

        Assert.NotNull(pricing);
        Assert.Equal(0.0025m, pricing!.InputPer1KTokens);
        Assert.Equal(0.01m, pricing.OutputPer1KTokens);
    }

    [Fact]
    public async Task Resolver_ReturnsNull_WhenNoOverrideAndNoConfig()
    {
        using var scope = _factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IAiPricingResolver>();

        var pricing = await resolver.ResolveAsync("openai", "gpt-nonexistent-model-xyz");

        Assert.Null(pricing);
    }

    [Fact]
    public async Task Resolver_IgnoresDeactivatedOverride()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var modelName = $"gpt-deact-res-{Guid.NewGuid():N}";

        var created = await (await ClientWithToken(token).PostAsJsonAsync("/api/admin/ai/pricing/overrides", new
        {
            providerName = "openai",
            modelName,
            inputPricePer1KTokens = 0.099m,
            outputPricePer1KTokens = 0.199m,
            currency = "USD",
            effectiveFromUtc = DateTime.UtcNow.AddMinutes(-1),
        })).Content.ReadFromJsonAsync<JsonElement>();

        var id = created.GetProperty("id").GetGuid();
        await ClientWithToken(token).DeleteAsync($"/api/admin/ai/pricing/overrides/{id}");

        using var scope = _factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IAiPricingResolver>();
        var pricing = await resolver.ResolveAsync("openai", modelName);

        // No config entry for this model, and override is deactivated → null
        Assert.Null(pricing);
    }

    private HttpClient ClientWithToken(string token)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }
}
