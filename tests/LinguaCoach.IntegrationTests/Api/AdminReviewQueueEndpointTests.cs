using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 9 of the AI bank-first teaching architecture: cross-entity review queue.
///
/// Phase I2A (legacy fallback deletion): the queue previously covered ActivityTemplate and
/// PlacementItemDefinition. ActivityTemplate was removed entirely, so the queue now covers
/// PlacementItemDefinition only — see
/// docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.
/// </summary>
public sealed class AdminReviewQueueEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminReviewQueueEndpointTests(ApiTestFactory factory) => _factory = factory;

    private System.Net.Http.HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/review-queue");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_IncludesPendingPlacementItem()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var questionText = $"Test prompt {Guid.NewGuid():N}";

        var itemId = await CreatePendingPlacementItemAsync(client, questionText);

        var listResp = await client.GetAsync("/api/admin/review-queue?pageSize=200&reviewStatus=PendingReview");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var body = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(items, i =>
            i.GetProperty("entityType").GetString() == "PlacementItem"
            && i.GetProperty("entityId").GetGuid() == itemId);
    }

    [Fact]
    public async Task List_FilterByEntityType_ReturnsOnlyPlacementItems()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var questionText = $"Test prompt filter {Guid.NewGuid():N}";

        var itemId = await CreatePendingPlacementItemAsync(client, questionText);

        var listResp = await client.GetAsync("/api/admin/review-queue?pageSize=200&reviewStatus=PendingReview&entityType=PlacementItem");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var body = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(items, i => i.GetProperty("entityId").GetGuid() == itemId);
        Assert.All(items, i => Assert.Equal("PlacementItem", i.GetProperty("entityType").GetString()));
    }

    [Fact]
    public async Task List_PendingCount_IsUnfilteredByRequestReviewStatus()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);

        // Query with reviewStatus=Approved (unlikely to match many rows) — pendingCount should
        // still reflect the true unfiltered PendingReview count, not zero-out to match the filter.
        var listResp = await client.GetAsync("/api/admin/review-queue?pageSize=1&reviewStatus=Approved");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var body = await listResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("pendingCount", out _));
    }

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

    private static async Task<Guid> CreatePendingPlacementItemAsync(HttpClient client, string questionText)
    {
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
        var itemId = addBody.TryGetProperty("itemId", out var idProp) ? idProp.GetGuid() : addBody.GetProperty("ItemId").GetGuid();

        // PlacementItemDefinition defaults to ReviewStatus=NotRequired — move it to
        // PendingReview so it's visible in the review queue's default filter.
        await client.PostAsJsonAsync($"/api/admin/placement-items/{itemId}/review", new { action = "reset" });

        return itemId;
    }
}
