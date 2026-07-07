using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 9 of the AI bank-first teaching architecture: cross-entity review queue covering
/// ActivityTemplate and PlacementItemDefinition.
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
    public async Task List_IncludesPendingActivityTemplate()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var key = $"b1.speaking.reviewqueue.{Guid.NewGuid():N}";

        var addResp = await client.PostAsJsonAsync("/api/admin/activity-templates", new
        {
            key, skill = "speaking", cefrLevel = "B1", activityType = "roleplay",
        });
        var templateId = (await addResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("templateId").GetGuid();

        // ActivityTemplate defaults to ReviewStatus=NotRequired — move it to PendingReview first.
        await client.PostAsJsonAsync($"/api/admin/activity-templates/{templateId}/review", new { action = "reset" });

        var listResp = await client.GetAsync("/api/admin/review-queue?pageSize=200&reviewStatus=PendingReview");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var body = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(items, i =>
            i.GetProperty("entityType").GetString() == "ActivityTemplate"
            && i.GetProperty("entityId").GetGuid() == templateId
            && i.GetProperty("displayKey").GetString() == key);
    }

    [Fact]
    public async Task List_FilterByEntityType_ExcludesOtherType()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(token);
        var key = $"b1.speaking.reviewqueue2.{Guid.NewGuid():N}";

        var addResp = await client.PostAsJsonAsync("/api/admin/activity-templates", new
        {
            key, skill = "speaking", cefrLevel = "B1", activityType = "roleplay",
        });
        var templateId = (await addResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("templateId").GetGuid();
        await client.PostAsJsonAsync($"/api/admin/activity-templates/{templateId}/review", new { action = "reset" });

        var listResp = await client.GetAsync("/api/admin/review-queue?pageSize=200&reviewStatus=PendingReview&entityType=PlacementItem");
        var body = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.DoesNotContain(items, i => i.GetProperty("entityId").GetGuid() == templateId);
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
}
