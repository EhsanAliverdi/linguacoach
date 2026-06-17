using System.Net;
using System.Net.Http.Headers;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Enums;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.PracticeGym;

/// <summary>
/// Integration tests for Phase 10O Practice Gym suggestion service and API endpoints.
/// </summary>
public sealed class PracticeGymSuggestionIntegrationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public PracticeGymSuggestionIntegrationTests(ApiTestFactory factory)
        => _factory = factory;

    // 1. IPracticeGymSuggestionService is registered and resolves from DI.
    [Fact]
    public void SuggestionService_IsRegisteredInDI()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<IPracticeGymSuggestionService>();
        Assert.NotNull(svc);
    }

    // 2. GET /api/practice-gym/suggestions returns 200 with empty sections when pool is empty.
    [Fact]
    public async Task GetSuggestions_EmptyPool_Returns200WithEmptySections()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync("suggestion-empty@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/practice-gym/suggestions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("suggestedItems", body);
        Assert.Contains("continueItems", body);
        Assert.Contains("reviewItems", body);
    }

    // 3. GET suggestions returns personalized ready items in SuggestedItems section.
    [Fact]
    public async Task GetSuggestions_ReadyItemsAppearInSuggestedSection()
    {
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync("suggestion-ready@test.com");
        var profileId = await GetProfileIdAsync(userId);

        await CreateReadyItemAsync(profileId, ReadinessPoolSource.PracticeGym, RoutingReason.Normal, isLower: false);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/practice-gym/suggestions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        // Should have a non-empty suggestedItems array.
        Assert.DoesNotContain("\"suggestedItems\":[]", body.Replace(" ", ""));
    }

    // 4. GET suggestions returns reserved valid items in ContinueItems section.
    [Fact]
    public async Task GetSuggestions_ReservedItemsAppearInContinueSection()
    {
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync("suggestion-continue@test.com");
        var profileId = await GetProfileIdAsync(userId);

        await CreateReservedItemAsync(profileId, ReadinessPoolSource.PracticeGym,
            expiresAt: DateTime.UtcNow.AddHours(2));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/practice-gym/suggestions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"continueItems\":[]", body.Replace(" ", ""));
    }

    // 5. GET suggestions returns ReviewOnly items in ReviewItems section.
    [Fact]
    public async Task GetSuggestions_ReviewOnlyItemsAppearInReviewSection()
    {
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync("suggestion-review@test.com");
        var profileId = await GetProfileIdAsync(userId);

        await CreateItemWithStatusAsync(profileId, ReadinessPoolStatus.ReviewOnly,
            RoutingReason.Review, isLower: true);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/practice-gym/suggestions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"reviewItems\":[]", body.Replace(" ", ""));
    }

    // 6. POST start reserves a ready item and returns navigation target.
    [Fact]
    public async Task PostStart_ReservesReadyItem_ReturnsSuccess()
    {
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync("suggestion-start@test.com");
        var profileId = await GetProfileIdAsync(userId);

        var itemId = await CreateReadyItemAsync(profileId, ReadinessPoolSource.PracticeGym,
            RoutingReason.Normal, isLower: false);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/practice-gym/suggestions/{itemId}/start", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"success\":true", body.Replace(" ", ""));
    }

    // 7. POST start is idempotent — already reserved item returns AlreadyReserved=true.
    [Fact]
    public async Task PostStart_AlreadyReserved_ReturnsAlreadyReserved()
    {
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync("suggestion-idem@test.com");
        var profileId = await GetProfileIdAsync(userId);

        var itemId = await CreateReservedItemAsync(profileId, ReadinessPoolSource.PracticeGym,
            expiresAt: DateTime.UtcNow.AddHours(2));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/practice-gym/suggestions/{itemId}/start", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"alreadyReserved\":true", body.Replace(" ", ""));
    }

    // 8. POST complete marks a reserved item consumed.
    [Fact]
    public async Task PostComplete_MarksReservedItemConsumed()
    {
        var (token, userId) = await _factory.CreateStudentAndGetTokenAsync("suggestion-complete@test.com");
        var profileId = await GetProfileIdAsync(userId);

        var itemId = await CreateReservedItemAsync(profileId, ReadinessPoolSource.PracticeGym,
            expiresAt: DateTime.UtcNow.AddHours(2));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/practice-gym/suggestions/{itemId}/complete", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var item = await db.StudentActivityReadinessItems.FindAsync(itemId);
        Assert.Equal(ReadinessPoolStatus.Consumed, item!.Status);
    }

    // 9. Existing /api/activity/practice-gym/next smoke test — still works after Phase 10O.
    [Fact]
    public async Task ExistingPracticeGymNext_StillWorks()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync("suggestion-smoke@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Should return 200 (or 429 rate-limit) — not 404 or 500.
        var response = await client.GetAsync("/api/activity/practice-gym/next?skill=speaking");
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.TooManyRequests,
            $"Unexpected status: {response.StatusCode}");
    }

    // 10. Admin readiness pool endpoints remain read-only (no write routes added).
    [Fact]
    public async Task AdminReadinessPool_NoWriteEndpointsAdded()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // POST to admin pool should 404 or 405 — no write endpoint registered.
        var someId = Guid.NewGuid();
        var response = await client.PostAsync($"/api/admin/students/{someId}/readiness-pool", null);
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound ||
            response.StatusCode == HttpStatusCode.MethodNotAllowed,
            $"Expected no write endpoint, got: {response.StatusCode}");
    }

    // --- helpers ---

    private async Task<Guid> GetProfileIdAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        return profile?.Id ?? userId;
    }

    private async Task<Guid> CreateReadyItemAsync(
        Guid profileId,
        ReadinessPoolSource source,
        RoutingReason routingReason,
        bool isLower)
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();

        var id = await poolSvc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId           = profileId,
            Source              = source,
            TargetCefrLevel     = "B2",
            RoutingReason       = routingReason,
            IsLowerLevelContent = isLower,
            ContextTagsJson     = "[\"general_english\"]",
            GeneratedBy         = "integration-test"
        });
        await poolSvc.MarkGeneratingAsync(id);
        await poolSvc.MarkReadyAsync(id);
        return id;
    }

    private async Task<Guid> CreateReservedItemAsync(
        Guid profileId,
        ReadinessPoolSource source,
        DateTime? expiresAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();

        var id = await poolSvc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId           = profileId,
            Source              = source,
            TargetCefrLevel     = "B2",
            RoutingReason       = RoutingReason.Normal,
            IsLowerLevelContent = false,
            ContextTagsJson     = "[\"general_english\"]",
            ExpiresAt           = expiresAt,
            GeneratedBy         = "integration-test"
        });
        await poolSvc.MarkGeneratingAsync(id);
        await poolSvc.MarkReadyAsync(id);

        // Reserve directly via the pool service.
        await poolSvc.ReserveNextReadyAsync(profileId, source);
        return id;
    }

    private async Task<Guid> CreateItemWithStatusAsync(
        Guid profileId,
        ReadinessPoolStatus status,
        RoutingReason routingReason,
        bool isLower)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var item = new LinguaCoach.Domain.Entities.StudentActivityReadinessItem(
            studentId: profileId,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B1",
            routingReason: routingReason,
            isLowerLevelContent: isLower,
            contextTagsJson: "[\"general_english\"]");

        // Force status via EF shadow property workaround — set via reflection.
        typeof(LinguaCoach.Domain.Entities.StudentActivityReadinessItem)
            .GetProperty(nameof(LinguaCoach.Domain.Entities.StudentActivityReadinessItem.Status))!
            .SetValue(item, status);

        db.StudentActivityReadinessItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }
}
