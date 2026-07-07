using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Ai;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 5 of the AI bank-first teaching architecture: the AI generation + validation pipeline
/// for ActivityTemplate instances. Uses fake AI providers — no real API calls, per project
/// convention (see ActivityTestFactory).
/// </summary>
public sealed class AdminActivityTemplateGenerationEndpointTests : IClassFixture<ActivityTemplateGenerationTestFactory>
{
    private readonly ActivityTemplateGenerationTestFactory _factory;

    public AdminActivityTemplateGenerationEndpointTests(ActivityTemplateGenerationTestFactory factory) => _factory = factory;

    private static object TemplateBody(string key, string? validationRulesJson = null) => new
    {
        key,
        skill = "speaking",
        cefrLevel = "B1",
        activityType = "roleplay",
        formIoBaseSchemaJson = JsonSerializer.Serialize(new
        {
            display = "form",
            components = new object[] { new { type = "textfield", key = "prompt_text", label = "Prompt" } },
        }),
        generationInstructions = "Write a short roleplay ordering coffee politely.",
        validationRulesJson,
    };

    private async Task<(HttpClient Client, Guid TemplateId)> CreateTemplateAsync(string? validationRulesJson = null)
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var key = $"b1.speaking.genpreview.{Guid.NewGuid():N}";
        var addResp = await client.PostAsJsonAsync("/api/admin/activity-templates", TemplateBody(key, validationRulesJson));
        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        return (client, addBody.GetProperty("templateId").GetGuid());
    }

    [Fact]
    public async Task GeneratePreview_ValidTemplate_ReturnsPersonalizedSchema()
    {
        _factory.UseWellFormedProvider();
        var (client, templateId) = await CreateTemplateAsync();

        var resp = await client.PostAsJsonAsync($"/api/admin/activity-templates/{templateId}/generate-preview", new { });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(templateId, body.GetProperty("templateId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("generatedSchemaJson").GetString()));
        Assert.Equal("fake-provider", body.GetProperty("providerName").GetString());
    }

    [Fact]
    public async Task GeneratePreview_TemplateNotFound_Returns400()
    {
        _factory.UseWellFormedProvider();
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var resp = await client.PostAsJsonAsync($"/api/admin/activity-templates/{Guid.NewGuid()}/generate-preview", new { });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GeneratePreview_MissingGenerationInstructions_Returns400()
    {
        _factory.UseWellFormedProvider();
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var key = $"b1.speaking.noinstr.{Guid.NewGuid():N}";
        var addResp = await client.PostAsJsonAsync("/api/admin/activity-templates", new
        {
            key,
            skill = "speaking",
            cefrLevel = "B1",
            activityType = "roleplay",
            formIoBaseSchemaJson = JsonSerializer.Serialize(new { display = "form", components = new object[] { new { type = "textfield", key = "prompt_text" } } }),
        });
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = addBody.GetProperty("templateId").GetGuid();

        var resp = await client.PostAsJsonAsync($"/api/admin/activity-templates/{templateId}/generate-preview", new { });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GeneratePreview_MalformedAiResponse_Returns422AfterRetry()
    {
        _factory.UseMalformedProvider();
        var (client, templateId) = await CreateTemplateAsync();

        var resp = await client.PostAsJsonAsync($"/api/admin/activity-templates/{templateId}/generate-preview", new { });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }

    [Fact]
    public async Task GeneratePreview_MissingRequiredComponentKey_Returns422AfterRetry()
    {
        _factory.UseWellFormedProvider();
        var (client, templateId) = await CreateTemplateAsync(
            validationRulesJson: JsonSerializer.Serialize(new { requiredComponentKeys = new[] { "not_in_schema" } }));

        var resp = await client.PostAsJsonAsync($"/api/admin/activity-templates/{templateId}/generate-preview", new { });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
    }
}

/// <summary>Test factory wiring a swappable fake IAiProvider so each test controls AI behavior.</summary>
public sealed class ActivityTemplateGenerationTestFactory : ApiTestFactory
{
    private readonly SwappableFakeAiProvider _provider = new();

    public void UseWellFormedProvider() => _provider.Inner = new WellFormedFormIoAiProvider();
    public void UseMalformedProvider() => _provider.Inner = new MalformedFakeAiProvider();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var providerDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAiProvider));
            if (providerDescriptor is not null) services.Remove(providerDescriptor);
            services.AddSingleton<IAiProvider>(_provider);

            var resolverDescriptors = services.Where(d => d.ServiceType == typeof(IAiProviderResolver)).ToList();
            foreach (var d in resolverDescriptors) services.Remove(d);
            services.AddScoped<IAiProviderResolver>(sp => new FakeAiProviderResolver(sp.GetRequiredService<IAiProvider>()));
        });
    }
}

/// <summary>Delegates to a swappable inner provider so tests can change AI behavior per-call.</summary>
internal sealed class SwappableFakeAiProvider : IAiProvider
{
    public IAiProvider Inner { get; set; } = new WellFormedFormIoAiProvider();
    public string ProviderName => Inner.ProviderName;
    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default) => Inner.CompleteAsync(request, ct);
}

/// <summary>Returns a valid, student-safe Form.io schema matching the test template's base schema shape.</summary>
internal sealed class WellFormedFormIoAiProvider : IAiProvider
{
    public string ProviderName => "fake-provider";

    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        const string json = """
            {"display":"form","components":[{"type":"textfield","key":"prompt_text","label":"Order a coffee politely."}]}
            """;
        return Task.FromResult(new AiResponse(json, InputTokens: 300, OutputTokens: 150, CostUsd: 0.001m, "fake-model", ProviderName));
    }
}
