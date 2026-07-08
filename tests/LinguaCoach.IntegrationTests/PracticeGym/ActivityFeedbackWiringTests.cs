using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Enums;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.PracticeGym;

/// <summary>
/// Integration tests for Phase B2 — activity feedback signals: the
/// POST /api/activity/attempt/{attemptId}/feedback endpoint, and ActivitySubmitHandler
/// attaching an effective FeedbackPolicy to the submit-attempt response.
/// </summary>
public sealed class ActivityFeedbackWiringTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public ActivityFeedbackWiringTests(ActivityTestFactory factory)
        => _factory = factory;

    [Fact]
    public async Task SubmitAttempt_Response_CarriesDefaultOptionalFeedbackPolicy()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"feedback-policy-{Guid.NewGuid():N}@test.com");

        var (_, activityId) = await SeedActivityAsync(userId);

        var client = ClientWithToken(token);
        var resp = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Test answer for feedback policy wiring." });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var feedbackPolicy = body.GetProperty("feedbackPolicy");
        // Enum values are serialized camelCase (see Program.cs JsonStringEnumConverter).
        Assert.Equal("optional", feedbackPolicy.GetProperty("policy").GetString());
        // Not linked to a SessionExercise → Practice Gym surface.
        Assert.Equal("practiceGym", feedbackPolicy.GetProperty("surface").GetString());
    }

    [Fact]
    public async Task SubmitFeedback_ForOwnAttempt_Returns200AndPersists()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(
            $"feedback-submit-{Guid.NewGuid():N}@test.com");

        var (_, activityId) = await SeedActivityAsync(userId);
        var client = ClientWithToken(token);

        var submitResp = await client.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Answer to generate an attempt." });
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = submitBody.GetProperty("attemptId").GetString();

        var feedbackResp = await client.PostAsJsonAsync(
            $"/api/activity/attempt/{attemptId}/feedback",
            new
            {
                learningActivityId = activityId,
                difficultyRating = "RightLevel",
                clarityRating = "Clear",
                usefulnessRating = "Useful",
                repeatPreference = "MoreLikeThis",
                optionalComment = "Helpful exercise.",
            });

        Assert.Equal(HttpStatusCode.OK, feedbackResp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var signal = await db.ActivityFeedbackSignals
            .FirstOrDefaultAsync(s => s.ActivityAttemptId == Guid.Parse(attemptId!));

        Assert.NotNull(signal);
        Assert.Equal(ActivityFeedbackDifficultyRating.RightLevel, signal!.DifficultyRating);
        Assert.Equal("Helpful exercise.", signal.OptionalComment);
    }

    [Fact]
    public async Task SubmitFeedback_ForAnotherStudentsAttempt_ReturnsForbidden()
    {
        var (ownerToken, ownerUserId) = await _factory.CreateOnboardedStudentAsync(
            $"feedback-owner-{Guid.NewGuid():N}@test.com");
        var (otherToken, _) = await _factory.CreateOnboardedStudentAsync(
            $"feedback-other-{Guid.NewGuid():N}@test.com");

        var (_, activityId) = await SeedActivityAsync(ownerUserId);
        var ownerClient = ClientWithToken(ownerToken);

        var submitResp = await ownerClient.PostAsJsonAsync(
            $"/api/activity/{activityId}/attempt",
            new { submittedContent = "Owner's answer." });
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = submitBody.GetProperty("attemptId").GetString();

        var otherClient = ClientWithToken(otherToken);
        var feedbackResp = await otherClient.PostAsJsonAsync(
            $"/api/activity/attempt/{attemptId}/feedback",
            new
            {
                learningActivityId = activityId,
                difficultyRating = "RightLevel",
                clarityRating = "Clear",
                usefulnessRating = "Useful",
                repeatPreference = "MoreLikeThis",
            });

        Assert.Equal(HttpStatusCode.Forbidden, feedbackResp.StatusCode);
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
            title: "Feedback wiring test activity",
            difficulty: "B2",
            aiGeneratedContentJson: """{"situation":"test","learningGoal":"test","targetPhrases":[],"targetVocabulary":[],"exampleText":"","commonMistakeToAvoid":"","instructionInSourceLanguage":""}""",
            sourceWritingScenarioId: scenario.Id);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();

        return (profileId, activity.Id);
    }
}
