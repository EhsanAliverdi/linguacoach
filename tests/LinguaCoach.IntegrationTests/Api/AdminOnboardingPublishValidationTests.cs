using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Covers the publish-time hardening in AdminOnboardingTemplateService: since
/// StudentOnboardingFlowService.ApplyToProfileAsync maps submitted data onto StudentProfile purely
/// by component-key string match (see OnboardingProfileFieldMapping), a template missing a
/// required key (currently just "preferred_name") would silently produce students whose profile
/// never gets that field populated, with no error surfaced anywhere. Publish now refuses to
/// activate such a template; draft saves are unaffected so admins can iterate freely.</summary>
public sealed class AdminOnboardingPublishValidationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminOnboardingPublishValidationTests(ApiTestFactory factory) => _factory = factory;

    private System.Net.Http.HttpClient AdminClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> CreateTemplateAsync(System.Net.Http.HttpClient adminClient)
    {
        var createResp = await adminClient.PostAsJsonAsync("/api/admin/onboarding/templates", new
        {
            name = $"Publish validation test {Guid.NewGuid():N}",
            description = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        return createBody.GetProperty("templateId").GetGuid();
    }

    [Fact]
    public async Task Publish_SchemaMissingPreferredName_Returns400AndDoesNotPublish()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var adminClient = AdminClient(adminToken);
        var templateId = await CreateTemplateAsync(adminClient);

        var schemaJson = JsonSerializer.Serialize(new
        {
            components = new object[] { new { type = "textfield", key = "career_context", label = "Career" } },
        });
        var draftResp = await adminClient.PutAsJsonAsync($"/api/admin/onboarding/templates/{templateId}/draft", new
        {
            formIoSchemaJson = schemaJson,
            scoringRulesJson = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, draftResp.StatusCode);

        var publishResp = await adminClient.PostAsync($"/api/admin/onboarding/templates/{templateId}/publish", null);

        Assert.Equal(HttpStatusCode.BadRequest, publishResp.StatusCode);
        var body = await publishResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("preferred_name", body.GetProperty("error").GetString());

        var getResp = await adminClient.GetAsync($"/api/admin/onboarding/templates/{templateId}");
        var getBody = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", getBody.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Publish_SchemaWithPreferredName_Succeeds()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var adminClient = AdminClient(adminToken);
        var templateId = await CreateTemplateAsync(adminClient);

        var schemaJson = JsonSerializer.Serialize(new
        {
            components = new object[] { new { type = "textfield", key = "preferred_name", label = "Name" } },
        });
        var draftResp = await adminClient.PutAsJsonAsync($"/api/admin/onboarding/templates/{templateId}/draft", new
        {
            formIoSchemaJson = schemaJson,
            scoringRulesJson = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, draftResp.StatusCode);

        var publishResp = await adminClient.PostAsync($"/api/admin/onboarding/templates/{templateId}/publish", null);

        Assert.Equal(HttpStatusCode.OK, publishResp.StatusCode);
    }

    [Fact]
    public async Task GetProfileFieldMapping_ReturnsRequiredPreferredNameEntry()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var adminClient = AdminClient(adminToken);

        var resp = await adminClient.GetAsync("/api/admin/onboarding/profile-field-mapping");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var fields = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var preferredName = fields.EnumerateArray().First(f => f.GetProperty("key").GetString() == "preferred_name");
        Assert.True(preferredName.GetProperty("required").GetBoolean());
    }
}
