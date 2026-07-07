using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class AdminPlacementItemEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminPlacementItemEndpointTests(ApiTestFactory factory) => _factory = factory;

    // ── GET /api/admin/placement-items ───────────────────────────────────────

    [Fact]
    public async Task List_AsAdmin_ReturnsAtLeastSeeded72ItemsAcrossPages()
    {
        // Other tests in this fixture add items of their own (some intentionally left behind,
        // e.g. AddItem_DuplicatePrompt_Returns400), so this asserts a floor, not an exact count.
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/admin/placement-items?page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("items").GetArrayLength());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 72,
            $"Expected at least 72 items, got {body.GetProperty("totalCount").GetInt32()}");
        Assert.True(body.GetProperty("overallTotalCount").GetInt32() >= 72);
    }

    [Fact]
    public async Task List_AsAdmin_EachItemHasFormIoSchemaAndScoringRules()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/admin/placement-items?page=1&pageSize=200");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var item in body.GetProperty("items").EnumerateArray())
        {
            Assert.True(item.TryGetProperty("formIoSchemaJson", out var schema));
            Assert.False(string.IsNullOrWhiteSpace(schema.GetString()));
            Assert.True(item.TryGetProperty("scoringRulesJson", out var rules));
            Assert.False(string.IsNullOrWhiteSpace(rules.GetString()));
        }
    }

    [Fact]
    public async Task List_AsAdmin_FilterBySkill_ReturnsOnlyThatSkill()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/admin/placement-items?page=1&pageSize=200&skill=listening");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.True(items.Count > 0);
        Assert.All(items, i => Assert.Equal("listening", i.GetProperty("skill").GetString()));
    }

    [Fact]
    public async Task GetItem_ById_ReturnsThatItem()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var listResp = await client.GetAsync("/api/admin/placement-items?page=1&pageSize=1");
        var listBody = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = listBody.GetProperty("items")[0].GetProperty("itemId").GetGuid();

        var getResp = await client.GetAsync($"/api/admin/placement-items/{itemId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var getBody = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(itemId, getBody.GetProperty("itemId").GetGuid());
    }

    [Fact]
    public async Task GetItem_NotFound_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync($"/api/admin/placement-items/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/placement-items");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── POST / PUT / DELETE round trip ───────────────────────────────────────

    private static object Schema(string questionText) => new
    {
        components = new object[]
        {
            new
            {
                type = "radio", key = "answer", label = questionText,
                values = new[] { new { label = "am", value = "A" }, new { label = "is", value = "B" } }
            }
        }
    };

    private static object ScoringRules(string correctAnswer) => new
    {
        components = new Dictionary<string, object>
        {
            ["answer"] = new { kind = "single_choice", correctAnswer, points = 1.0 }
        }
    };

    [Fact]
    public async Task AddItem_ThenUpdateItem_ThenDeleteItem_Succeeds()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var questionText = $"Test prompt {Guid.NewGuid():N}";

        var addResp = await client.PostAsJsonAsync("/api/admin/placement-items", new
        {
            skill = "grammar",
            cefrLevel = "A1",
            itemType = "multiple_choice",
            prompt = questionText,
            formIoSchemaJson = JsonSerializer.Serialize(Schema(questionText)),
            scoringRulesJson = JsonSerializer.Serialize(ScoringRules("A")),
            itemOrder = 999,
            isEnabled = true,
        });
        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = GetItemId(addBody);

        var updateResp = await client.PutAsJsonAsync($"/api/admin/placement-items/{itemId}", new
        {
            skill = "grammar",
            cefrLevel = "A2",
            itemType = "multiple_choice",
            prompt = questionText,
            formIoSchemaJson = JsonSerializer.Serialize(Schema(questionText)),
            scoringRulesJson = JsonSerializer.Serialize(ScoringRules("B")),
            itemOrder = 999,
            isEnabled = false,
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updateBody = await updateResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("A2", updateBody.GetProperty("cefrLevel").GetString());
        Assert.False(updateBody.GetProperty("isEnabled").GetBoolean());
        Assert.Equal(2, updateBody.GetProperty("scoringRulesVersion").GetInt32());

        var deleteResp = await client.DeleteAsync($"/api/admin/placement-items/{itemId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task AddItem_DuplicatePrompt_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var questionText = $"Duplicate prompt {Guid.NewGuid():N}";

        object Body(string correctAnswer) => new
        {
            skill = "grammar", cefrLevel = "A1", itemType = "multiple_choice", prompt = questionText,
            formIoSchemaJson = JsonSerializer.Serialize(Schema(questionText)),
            scoringRulesJson = JsonSerializer.Serialize(ScoringRules(correctAnswer)),
            itemOrder = 1000, isEnabled = true,
        };

        var first = await client.PostAsJsonAsync("/api/admin/placement-items", Body("A"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/admin/placement-items", Body("B"));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task AddItem_InvalidFormIoSchema_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var questionText = $"Bad schema {Guid.NewGuid():N}";

        var resp = await client.PostAsJsonAsync("/api/admin/placement-items", new
        {
            skill = "grammar", cefrLevel = "A1", itemType = "multiple_choice", prompt = questionText,
            formIoSchemaJson = JsonSerializer.Serialize(new { components = new object[] { new { type = "script_eval", key = "answer" } } }),
            scoringRulesJson = JsonSerializer.Serialize(ScoringRules("A")),
            itemOrder = 1001, isEnabled = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AddItem_ScoringRulesReferenceOrphanedComponentKey_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var questionText = $"Orphaned key {Guid.NewGuid():N}";

        var resp = await client.PostAsJsonAsync("/api/admin/placement-items", new
        {
            skill = "grammar", cefrLevel = "A1", itemType = "multiple_choice", prompt = questionText,
            formIoSchemaJson = JsonSerializer.Serialize(Schema(questionText)),
            scoringRulesJson = JsonSerializer.Serialize(new
            {
                components = new Dictionary<string, object>
                {
                    ["not_a_real_component"] = new { kind = "single_choice", correctAnswer = "A", points = 1.0 }
                }
            }),
            itemOrder = 1002, isEnabled = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("not present in the Form.io schema", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task DeleteItem_NotFound_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.DeleteAsync($"/api/admin/placement-items/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private System.Net.Http.HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static Guid GetItemId(JsonElement body)
    {
        if (body.TryGetProperty("itemId", out var id)) return id.GetGuid();
        return body.GetProperty("ItemId").GetGuid();
    }
}
