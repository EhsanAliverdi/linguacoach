using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Enums;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.PracticeGym;

/// <summary>
/// Integration tests verifying that ActivitySubmitHandler wires readiness item consumption
/// after activity completion (Phase 10O-F, TODO-014).
/// </summary>
public sealed class ReadinessConsumptionWiringTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public ReadinessConsumptionWiringTests(ActivityTestFactory factory)
        => _factory = factory;

    // 1. Completing a linked activity marks its Reserved readiness item Consumed.
    [Fact]
    public async Task SubmitActivity_WithLinkedReservedItem_MarksItemConsumed()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"consume-wiring-{Guid.NewGuid():N}@test.com");

        var (profileId, activityId, itemId) = await SeedActivityWithReservedItemAsync(userId);

        var client = ClientWithToken(token);
        var resp = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Test answer for consumption wiring." });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var item = await db.StudentActivityReadinessItems.FindAsync(itemId);
        Assert.Equal(ReadinessPoolStatus.Consumed, item!.Status);
    }

    // 2. Consumption is idempotent: submitting twice does not throw.
    [Fact]
    public async Task SubmitActivity_Twice_ConsumptionIdempotent()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"consume-idem-{Guid.NewGuid():N}@test.com");

        var (_, activityId, itemId) = await SeedActivityWithReservedItemAsync(userId);

        var client = ClientWithToken(token);
        var r1 = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "First submission." });
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        // Second submit — item is now Consumed; TryMarkConsumedAsync should no-op, not throw.
        var r2 = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Second submission." });
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var item = await db.StudentActivityReadinessItems.FindAsync(itemId);
        Assert.Equal(ReadinessPoolStatus.Consumed, item!.Status);
    }

    // 3. Submitting an activity with no linked readiness item does not fail.
    [Fact]
    public async Task SubmitActivity_NoLinkedItem_SucceedsNormally()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"consume-noitem-{Guid.NewGuid():N}@test.com");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var scenario = db.WritingScenarios.First();
        var activity = new LinguaCoach.Domain.Entities.LearningActivity(
            activityType: ActivityType.WritingScenario,
            source: ActivitySource.SystemFallback,
            title: "No-item activity",
            difficulty: "B1",
            aiGeneratedContentJson: """{"situation":"test","learningGoal":"test","targetPhrases":[],"targetVocabulary":[],"exampleText":"","commonMistakeToAvoid":"","instructionInSourceLanguage":""}""",
            sourceWritingScenarioId: scenario.Id);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();

        var client = ClientWithToken(token);
        var resp = await client.PostAsJsonAsync(
            $"/api/activity/{activity.Id}/attempt",
            new { submittedContent = "No linked readiness item." });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // 4. Consumed item no longer appears in suggestions after completion.
    [Fact]
    public async Task ConsumedItem_DoesNotAppearInSuggestions()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"consume-hide-{Guid.NewGuid():N}@test.com");

        var (_, activityId, itemId) = await SeedActivityWithReservedItemAsync(userId);

        var client = ClientWithToken(token);

        // Submit to consume the item.
        var submitResp = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Completion to hide item from suggestions." });
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);

        // Suggestions should not include the consumed item.
        var suggestResp = await client.GetAsync("/api/practice-gym/suggestions");
        Assert.Equal(HttpStatusCode.OK, suggestResp.StatusCode);
        var body = await suggestResp.Content.ReadAsStringAsync();
        Assert.DoesNotContain(itemId.ToString(), body);
    }

    // --- helpers ---

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> GetProfileIdAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        return profile?.Id ?? userId;
    }

    /// <summary>
    /// Seeds a WritingScenario LearningActivity and a Reserved readiness item
    /// linked to that activity via LearningActivityId. Returns (profileId, activityId, itemId).
    /// </summary>
    private async Task<(Guid profileId, Guid activityId, Guid itemId)> SeedActivityWithReservedItemAsync(
        Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var poolSvc = scope.ServiceProvider.GetRequiredService<IStudentActivityReadinessPoolService>();

        var profileId = (await db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId))?.Id
            ?? userId;

        var scenario = db.WritingScenarios.First();
        var activity = new LinguaCoach.Domain.Entities.LearningActivity(
            activityType: ActivityType.WritingScenario,
            source: ActivitySource.SystemFallback,
            title: "Consumption wiring test activity",
            difficulty: "B2",
            aiGeneratedContentJson: """{"situation":"test","learningGoal":"test","targetPhrases":[],"targetVocabulary":[],"exampleText":"","commonMistakeToAvoid":"","instructionInSourceLanguage":""}""",
            sourceWritingScenarioId: scenario.Id);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();

        // Create a readiness item and link it to the activity in the Ready state.
        var itemId = await poolSvc.CreateQueuedAsync(new CreateReadinessItemRequest
        {
            StudentId           = profileId,
            Source              = ReadinessPoolSource.PracticeGym,
            TargetCefrLevel     = "B2",
            RoutingReason       = RoutingReason.Normal,
            IsLowerLevelContent = false,
            ContextTagsJson     = "[\"general_english\"]",
            GeneratedBy         = "consumption-wiring-test"
        });
        await poolSvc.MarkGeneratingAsync(itemId);
        await poolSvc.MarkReadyAsync(itemId, learningActivityId: activity.Id);

        // Reserve it so it can be consumed on completion.
        await poolSvc.ReserveNextReadyAsync(profileId, ReadinessPoolSource.PracticeGym);

        return (profileId, activity.Id, itemId);
    }
}
