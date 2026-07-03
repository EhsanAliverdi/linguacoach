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
        var prompt = $"Test prompt {Guid.NewGuid():N}";

        var addResp = await client.PostAsJsonAsync("/api/admin/placement-items", new
        {
            skill = "grammar",
            cefrLevel = "A1",
            itemType = "multiple_choice",
            prompt,
            correctAnswer = "A",
            readingPassage = (string?)null,
            listeningAudioScript = (string?)null,
            itemOrder = 999,
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = GetItemId(addBody);

        var updateResp = await client.PutAsJsonAsync($"/api/admin/placement-items/{itemId}", new
        {
            skill = "grammar",
            cefrLevel = "A2",
            itemType = "multiple_choice",
            prompt,
            correctAnswer = "B",
            readingPassage = (string?)null,
            listeningAudioScript = (string?)null,
            itemOrder = 999,
            isEnabled = false
        });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updateBody = await updateResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("A2", updateBody.GetProperty("cefrLevel").GetString());
        Assert.False(updateBody.GetProperty("isEnabled").GetBoolean());

        var deleteResp = await client.DeleteAsync($"/api/admin/placement-items/{itemId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task AddItem_DuplicatePrompt_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var prompt = $"Duplicate prompt {Guid.NewGuid():N}";

        var first = await client.PostAsJsonAsync("/api/admin/placement-items", new
        {
            skill = "grammar", cefrLevel = "A1", itemType = "multiple_choice",
            prompt, correctAnswer = "A", readingPassage = (string?)null,
            listeningAudioScript = (string?)null, itemOrder = 1000, isEnabled = true
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/admin/placement-items", new
        {
            skill = "grammar", cefrLevel = "A1", itemType = "multiple_choice",
            prompt, correctAnswer = "B", readingPassage = (string?)null,
            listeningAudioScript = (string?)null, itemOrder = 1001, isEnabled = true
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
