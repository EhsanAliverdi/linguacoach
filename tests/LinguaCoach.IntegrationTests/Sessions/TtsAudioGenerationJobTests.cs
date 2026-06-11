using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.Jobs;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Sessions;

/// <summary>
/// TTS idempotency: generating audio twice for the same transcript + speaker fingerprint
/// reuses the existing AudioAsset; a different transcript creates a new asset.
/// </summary>
public sealed class TtsAudioGenerationJobTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public TtsAudioGenerationJobTests(ActivityTestFactory factory) => _factory = factory;

    [Fact]
    public async Task GenerateForActivity_SameTranscript_ReusesAsset()
    {
        var (_, userId) = await _factory.CreateOnboardedStudentAsync($"tts_idem_{Guid.NewGuid():N}@test.com");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var listening = scope.ServiceProvider.GetRequiredService<ListeningAudioService>();

        var activity = CreateListeningActivity("The meeting starts at three.");
        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();

        var job = new TtsAudioGenerationJob(db, listening, NullLogger<TtsAudioGenerationJob>.Instance);

        await job.GenerateForActivityAsync(activity, profile.Id, null, CancellationToken.None);
        var afterFirst = await db.AudioAssets.CountAsync(a => a.LearningActivityId == activity.Id);

        await job.GenerateForActivityAsync(activity, profile.Id, null, CancellationToken.None);
        var afterSecond = await db.AudioAssets.CountAsync(a => a.LearningActivityId == activity.Id);

        Assert.Equal(1, afterFirst);
        Assert.Equal(1, afterSecond); // idempotent — no duplicate
    }

    [Fact]
    public async Task GenerateForActivity_DifferentTranscript_CreatesNewAsset()
    {
        var (_, userId) = await _factory.CreateOnboardedStudentAsync($"tts_new_{Guid.NewGuid():N}@test.com");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var listening = scope.ServiceProvider.GetRequiredService<ListeningAudioService>();
        var job = new TtsAudioGenerationJob(db, listening, NullLogger<TtsAudioGenerationJob>.Instance);

        var a1 = CreateListeningActivity("First transcript.");
        var a2 = CreateListeningActivity("A completely different transcript.");
        db.LearningActivities.AddRange(a1, a2);
        await db.SaveChangesAsync();

        await job.GenerateForActivityAsync(a1, profile.Id, null, CancellationToken.None);
        await job.GenerateForActivityAsync(a2, profile.Id, null, CancellationToken.None);

        var total = await db.AudioAssets.CountAsync(a => a.LearningActivityId == a1.Id || a.LearningActivityId == a2.Id);
        Assert.Equal(2, total);
    }

    private static LearningActivity CreateListeningActivity(string transcript)
    {
        var contentJson = JsonSerializer.Serialize(new
        {
            activityType = "ListeningComprehension",
            title = "tts test",
            audioScript = transcript,
            transcriptAvailableAfterSubmit = true,
            questions = new[] { new { id = "q1", question = "?", expectedAnswer = "x", type = "short_answer" } }
        });
        return new LearningActivity(
            ActivityType.ListeningComprehension, ActivitySource.AiGenerated,
            "tts test", "B1", contentJson);
    }
}
