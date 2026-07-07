using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class AdminActivityTemplateEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminActivityTemplateEndpointTests(ApiTestFactory factory) => _factory = factory;

    private static object ValidBody(string key) => new
    {
        key,
        skill = "speaking",
        cefrLevel = "B1",
        activityType = "roleplay",
        estimatedDurationSeconds = 300,
    };

    // ── GET list ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/activity-templates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddTemplate_ThenListFiltersBySkill_ReturnsIt()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var key = $"b1.speaking.test.{Guid.NewGuid():N}";

        var addResp = await client.PostAsJsonAsync("/api/admin/activity-templates", ValidBody(key));
        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);

        var listResp = await client.GetAsync("/api/admin/activity-templates?page=1&pageSize=200&skill=speaking");
        var listBody = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = listBody.GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(items, i => i.GetProperty("key").GetString() == key);
        Assert.All(items, i => Assert.Equal("speaking", i.GetProperty("skill").GetString()));
    }

    [Fact]
    public async Task GetTemplate_ById_ReturnsThatTemplate()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var key = $"b1.speaking.test.{Guid.NewGuid():N}";

        var addResp = await client.PostAsJsonAsync("/api/admin/activity-templates", ValidBody(key));
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = addBody.GetProperty("templateId").GetGuid();

        var getResp = await client.GetAsync($"/api/admin/activity-templates/{templateId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var getBody = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(templateId, getBody.GetProperty("templateId").GetGuid());
        Assert.Equal(key, getBody.GetProperty("key").GetString());
    }

    [Fact]
    public async Task GetTemplate_NotFound_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync($"/api/admin/activity-templates/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── POST / PUT / DELETE round trip ───────────────────────────────────────

    [Fact]
    public async Task AddTemplate_ThenUpdateTemplate_ThenDeleteTemplate_Succeeds()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var key = $"b1.speaking.test.{Guid.NewGuid():N}";

        var addResp = await client.PostAsJsonAsync("/api/admin/activity-templates", ValidBody(key));
        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = addBody.GetProperty("templateId").GetGuid();

        var updateResp = await client.PutAsJsonAsync($"/api/admin/activity-templates/{templateId}", new
        {
            skill = "speaking",
            cefrLevel = "B2",
            activityType = "roleplay",
            estimatedDurationSeconds = 400,
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updateBody = await updateResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("B2", updateBody.GetProperty("cefrLevel").GetString());
        Assert.Equal(400, updateBody.GetProperty("estimatedDurationSeconds").GetInt32());

        var deleteResp = await client.DeleteAsync($"/api/admin/activity-templates/{templateId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task AddTemplate_DuplicateKey_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var key = $"b1.speaking.dup.{Guid.NewGuid():N}";

        var first = await client.PostAsJsonAsync("/api/admin/activity-templates", ValidBody(key));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/admin/activity-templates", ValidBody(key));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task AddTemplate_InvalidSkill_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var resp = await client.PostAsJsonAsync("/api/admin/activity-templates", new
        {
            key = $"bad.{Guid.NewGuid():N}",
            skill = "not_a_real_skill",
            cefrLevel = "B1",
            activityType = "roleplay",
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AddTemplate_InvalidFormIoSchema_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var resp = await client.PostAsJsonAsync("/api/admin/activity-templates", new
        {
            key = $"bad-schema.{Guid.NewGuid():N}",
            skill = "speaking",
            cefrLevel = "B1",
            activityType = "roleplay",
            formIoBaseSchemaJson = JsonSerializer.Serialize(new { components = new object[] { new { type = "script_eval", key = "answer" } } }),
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteTemplate_NotFound_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.DeleteAsync($"/api/admin/activity-templates/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Review / publish ──────────────────────────────────────────────────────

    [Fact]
    public async Task Review_ApproveThenPublish_Succeeds()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var key = $"b1.speaking.review.{Guid.NewGuid():N}";

        var addResp = await client.PostAsJsonAsync("/api/admin/activity-templates", ValidBody(key));
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = addBody.GetProperty("templateId").GetGuid();

        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/activity-templates/{templateId}/review", new { action = "approve" });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
        var approveBody = await approveResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", approveBody.GetProperty("reviewStatus").GetString());

        var publishResp = await client.PostAsJsonAsync(
            $"/api/admin/activity-templates/{templateId}/publish", new { publish = true });
        Assert.Equal(HttpStatusCode.OK, publishResp.StatusCode);
        var publishBody = await publishResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(publishBody.GetProperty("isPublished").GetBoolean());
    }

    [Fact]
    public async Task Review_RejectWithoutReason_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var key = $"b1.speaking.reject.{Guid.NewGuid():N}";

        var addResp = await client.PostAsJsonAsync("/api/admin/activity-templates", ValidBody(key));
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = addBody.GetProperty("templateId").GetGuid();

        var rejectResp = await client.PostAsJsonAsync(
            $"/api/admin/activity-templates/{templateId}/review", new { action = "reject" });
        Assert.Equal(HttpStatusCode.BadRequest, rejectResp.StatusCode);
    }

    [Fact]
    public async Task Publish_AfterRejected_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var key = $"b1.speaking.rejectpublish.{Guid.NewGuid():N}";

        var addResp = await client.PostAsJsonAsync("/api/admin/activity-templates", ValidBody(key));
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = addBody.GetProperty("templateId").GetGuid();

        await client.PostAsJsonAsync(
            $"/api/admin/activity-templates/{templateId}/review", new { action = "reject", reason = "Not appropriate." });

        var publishResp = await client.PostAsJsonAsync(
            $"/api/admin/activity-templates/{templateId}/publish", new { publish = true });
        Assert.Equal(HttpStatusCode.BadRequest, publishResp.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private System.Net.Http.HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
