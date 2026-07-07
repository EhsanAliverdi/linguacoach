using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.PracticeGym;

/// <summary>
/// Integration tests verifying that ActivitySubmitHandler writes a real
/// StudentActivityUsageLog on activity completion (Phase B — repetition/novelty foundation).
/// See docs/architecture/repetition-and-novelty.md.
/// </summary>
public sealed class StudentActivityUsageLogWiringTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public StudentActivityUsageLogWiringTests(ActivityTestFactory factory)
        => _factory = factory;

    [Fact]
    public async Task SubmitActivity_CreatesUsageLog()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"usagelog-create-{Guid.NewGuid():N}@test.com");

        var (profileId, activityId) = await SeedActivityAsync(userId);

        var client = ClientWithToken(token);
        var resp = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Test answer for usage-log wiring." });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var log = await db.StudentActivityUsageLogs
            .FirstOrDefaultAsync(l => l.StudentProfileId == profileId && l.LearningActivityId == activityId);

        Assert.NotNull(log);
        Assert.False(string.IsNullOrWhiteSpace(log!.ContentFingerprint));
        Assert.Equal(ActivityType.WritingScenario.ToString(), log.ActivityType);
    }

    [Fact]
    public async Task SubmitActivity_Twice_DoesNotCreateDuplicateUsageLog()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"usagelog-idem-{Guid.NewGuid():N}@test.com");

        var (profileId, activityId) = await SeedActivityAsync(userId);

        var client = ClientWithToken(token);
        var r1 = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "First submission." });
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        var r2 = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Second submission." });
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var count = await db.StudentActivityUsageLogs
            .CountAsync(l => l.StudentProfileId == profileId && l.LearningActivityId == activityId);

        Assert.Equal(1, count);
    }

    // --- helpers ---

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(Guid profileId, Guid activityId)> SeedActivityAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var profileId = (await db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId))?.Id
            ?? userId;

        var scenario = db.WritingScenarios.First();
        var activity = new LinguaCoach.Domain.Entities.LearningActivity(
            activityType: ActivityType.WritingScenario,
            source: ActivitySource.SystemFallback,
            title: "Usage log wiring test activity",
            difficulty: "B2",
            aiGeneratedContentJson: """{"situation":"test","learningGoal":"test","targetPhrases":[],"targetVocabulary":[],"exampleText":"","commonMistakeToAvoid":"","instructionInSourceLanguage":""}""",
            sourceWritingScenarioId: scenario.Id);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();

        return (profileId, activity.Id);
    }
}
