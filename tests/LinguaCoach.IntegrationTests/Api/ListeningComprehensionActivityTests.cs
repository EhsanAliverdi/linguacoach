using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class ListeningComprehensionActivityTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public ListeningComprehensionActivityTests(ActivityTestFactory factory) => _factory = factory;

    [Fact]
    public async Task GetNext_AtEveryFifthAttempt_ReturnsListeningWithoutTranscriptOrExpectedAnswers()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"listen_next_{Guid.NewGuid():N}@test.com");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var activity = db.LearningActivities.First();

        for (var i = 0; i < 5; i++)
        {
            db.ActivityAttempts.Add(new ActivityAttempt(
                profile.Id, activity.Id, $"Attempt {i}", "{}", "activity_evaluate_writing", score: 70));
        }
        await db.SaveChangesAsync();

        var resp = await ClientWithToken(token).GetAsync("/api/activity/next");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("listeningComprehension", body.GetProperty("activityType").GetString());
        Assert.True(body.TryGetProperty("listeningQuestions", out var questions));
        Assert.True(questions.GetArrayLength() >= 2);
        Assert.False(body.TryGetProperty("audioScript", out _));
        Assert.False(body.TryGetProperty("transcript", out _));
        foreach (var q in questions.EnumerateArray())
            Assert.False(q.TryGetProperty("expectedAnswer", out _));
    }

    [Fact]
    public async Task SubmitListeningAttempt_CreatesAttemptAndRevealsTranscript()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"listen_submit_{Guid.NewGuid():N}@test.com");
        var activityId = await CreateListeningActivityForStudent(userId);

        var resp = await ClientWithToken(token).PostAsJsonAsync($"/api/activity/{activityId}/attempt", new
        {
            submittedContent = "Sure, I will check it before 3 pm.",
            responseText = "Sure, I will check the latest delivery schedule and send the updated timeline before 3 pm.",
            answers = new[]
            {
                new { questionId = "q1", answer = "the latest delivery schedule" },
                new { questionId = "q2", answer = "two days" }
            }
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("score").GetDouble() >= 80);
        Assert.Contains("supplier has confirmed a two-day delay", body.GetProperty("transcript").GetString());
        Assert.True(body.GetProperty("questionFeedback").GetArrayLength() == 2);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        Assert.Contains(db.ActivityAttempts, a => a.StudentProfileId == profile.Id && a.LearningActivityId == activityId);
    }

    [Fact]
    public async Task SubmitListeningAttempt_OtherStudentCannotSubmit()
    {
        var (_, ownerUserId) = await _factory.CreateOnboardedStudentAsync($"listen_owner_{Guid.NewGuid():N}@test.com");
        var (otherToken, _) = await _factory.CreateOnboardedStudentAsync($"listen_other_{Guid.NewGuid():N}@test.com");
        var activityId = await CreateListeningActivityForStudent(ownerUserId, attachToOwnedModule: true);

        var resp = await ClientWithToken(otherToken).PostAsJsonAsync($"/api/activity/{activityId}/attempt", new
        {
            submittedContent = "I will check it.",
            responseText = "I will check it.",
            answers = new[] { new { questionId = "q1", answer = "schedule" } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    private async Task<Guid> CreateListeningActivityForStudent(Guid userId, bool attachToOwnedModule = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        Guid? moduleId = null;
        if (attachToOwnedModule)
        {
            var path = new LearningPath(profile.Id, "Listening test path", "Test learner context");
            db.LearningPaths.Add(path);
            await db.SaveChangesAsync();
            var module = new LearningModule(path.Id, "Listening module", "Practice understanding workplace updates.", 1);
            db.LearningModules.Add(module);
            await db.SaveChangesAsync();
            moduleId = module.Id;
        }
        var contentJson = JsonSerializer.Serialize(new
        {
            activityType = "ListeningComprehension",
            title = "Understand a project update",
            scenario = "Your manager leaves a short voice message about a project delay.",
            instructions = "Answer as if you listened to the message.",
            speakerRole = "Manager",
            listenerRole = "Document Controller",
            difficulty = "B1",
            audioScript = "Hi, could you please check the latest delivery schedule? The supplier has confirmed a two-day delay, and I need an updated timeline before our 3 pm meeting.",
            transcriptAvailableAfterSubmit = true,
            questions = new[]
            {
                new { id = "q1", question = "What should the listener check?", expectedAnswer = "latest delivery schedule", type = "short_answer" },
                new { id = "q2", question = "How long is the delay?", expectedAnswer = "two days", type = "short_answer" }
            },
            responseTask = new { prompt = "Write a short reply.", expectedFocus = "check schedule updated timeline before 3 pm" }
        });

        var activity = new LearningActivity(
            ActivityType.ListeningComprehension,
            ActivitySource.AiGenerated,
            "Understand a project update",
            "B1",
            contentJson,
            learningModuleId: moduleId);
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();
        return activity.Id;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
