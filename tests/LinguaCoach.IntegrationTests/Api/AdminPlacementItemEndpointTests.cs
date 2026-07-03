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
    public async Task List_AsAdmin_ReturnsAtLeastSeeded72Items()
    {
        // Other tests in this fixture add items of their own (some intentionally left behind,
        // e.g. AddItem_DuplicatePrompt_Returns400), so this asserts a floor, not an exact count.
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/admin/placement-items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.True(body.GetArrayLength() >= 72, $"Expected at least 72 items, got {body.GetArrayLength()}");
    }

    [Fact]
    public async Task List_AsAdmin_EachItemHasStructuredContent()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/admin/placement-items");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var item in body.EnumerateArray())
        {
            Assert.True(item.TryGetProperty("content", out var content));
            Assert.True(content.TryGetProperty("type", out _));
        }
    }

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/placement-items");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── POST / PUT / DELETE round trip ───────────────────────────────────────

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
            content = new
            {
                type = "single_choice",
                id = "q1",
                questionText,
                choices = new[] { new { key = "A", label = "am" }, new { key = "B", label = "is" } },
                correctAnswerKey = "A",
            },
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
            content = new
            {
                type = "single_choice",
                id = "q1",
                questionText,
                choices = new[] { new { key = "A", label = "am" }, new { key = "B", label = "is" } },
                correctAnswerKey = "B",
            },
            itemOrder = 999,
            isEnabled = false,
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updateBody = await updateResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("A2", updateBody.GetProperty("cefrLevel").GetString());
        Assert.False(updateBody.GetProperty("isEnabled").GetBoolean());
        Assert.Equal("B", updateBody.GetProperty("correctAnswer").GetString());

        var deleteResp = await client.DeleteAsync($"/api/admin/placement-items/{itemId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task AddItem_ReadingGroupWithTwoSubQuestions_Succeeds()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var passage = $"The cat sat on the mat. {Guid.NewGuid():N}";

        var addResp = await client.PostAsJsonAsync("/api/admin/placement-items", new
        {
            skill = "reading",
            cefrLevel = "A1",
            content = new
            {
                type = "reading_group",
                id = "g1",
                passage,
                questions = new object[]
                {
                    new { type = "single_choice", id = "q1", questionText = "Where did the cat sit?", choices = new[] { new { key = "A", label = "mat" } }, correctAnswerKey = "A" },
                    new { type = "gap_fill", id = "q2", questionText = "The ___ sat on the mat.", correctAnswer = "cat" },
                },
            },
            itemOrder = 998,
            isEnabled = true,
        });

        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);
        var body = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var content = body.GetProperty("content");
        Assert.Equal("reading_group", content.GetProperty("type").GetString());
        Assert.Equal(2, content.GetProperty("questions").GetArrayLength());

        await client.DeleteAsync($"/api/admin/placement-items/{GetItemId(body)}");
    }

    [Fact]
    public async Task AddItem_DuplicatePrompt_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var questionText = $"Duplicate prompt {Guid.NewGuid():N}";

        object Content(string correctAnswerKey) => new
        {
            type = "single_choice",
            id = "q1",
            questionText,
            choices = new[] { new { key = "A", label = "am" }, new { key = "B", label = "is" } },
            correctAnswerKey,
        };

        var first = await client.PostAsJsonAsync("/api/admin/placement-items", new
        {
            skill = "grammar", cefrLevel = "A1", content = Content("A"), itemOrder = 1000, isEnabled = true,
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/admin/placement-items", new
        {
            skill = "grammar", cefrLevel = "A1", content = Content("B"), itemOrder = 1001, isEnabled = true,
        });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
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
