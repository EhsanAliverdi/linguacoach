using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// T-Sprint10: VocabularyPractice activity type.
/// Tests: activity selection, generation, submission, scoring, vocab item updates, student isolation.
/// </summary>
public sealed class VocabularyPracticeActivityTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public VocabularyPracticeActivityTests(ActivityTestFactory factory) => _factory = factory;

    // Phase I2A (legacy fallback deletion): GET /api/activity/next was removed — the
    // vocab-item-count / attempt-cadence activity-type selection logic it used to exercise
    // (VocabularyPractice vs WritingScenario routing) no longer exists. Coverage of the
    // VocabularyPractice submission/scoring/vocab-item-update behaviour lives in the
    // directly-seeded tests below (CreateVocabActivityForStudent). See
    // docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.

    // ── VocabularyPractice submission creates ActivityAttempt ─────────────────

    [Fact]
    public async Task SubmitVocabAttempt_CreatesActivityAttempt()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vp_submit_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var (activityId, vocabItemId) = await CreateVocabActivityForStudent(userId);

        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt", new
        {
            submittedContent = "",
            answers = new[] { new { vocabularyItemId = vocabItemId, answer = "could you please" } }
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("attemptId", out _), "attemptId missing");
        Assert.True(body.TryGetProperty("score", out var score));
        Assert.Equal(JsonValueKind.Number, score.ValueKind);

        // Verify attempt persisted
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var attempt = db.ActivityAttempts.Where(a => a.StudentProfileId == profile.Id && a.LearningActivityId == activityId).FirstOrDefault();
        Assert.NotNull(attempt);
    }

    // ── Correct answers give high score ───────────────────────────────────────

    [Fact]
    public async Task SubmitVocabAttempt_AllCorrect_GivesHighScore()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vp_correct_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var (activityId, vocabItemId) = await CreateVocabActivityForStudent(userId);

        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt", new
        {
            submittedContent = "",
            answers = new[] { new { vocabularyItemId = vocabItemId, answer = "could you please" } }
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var score = body.GetProperty("score").GetDouble();
        Assert.True(score >= 80, $"Expected score >= 80 for correct answer, got {score}");
    }

    // ── Incorrect answers give lower score ────────────────────────────────────

    [Fact]
    public async Task SubmitVocabAttempt_AllWrong_GivesLowScore()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vp_wrong_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var (activityId, vocabItemId) = await CreateVocabActivityForStudent(userId);

        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt", new
        {
            submittedContent = "",
            answers = new[] { new { vocabularyItemId = vocabItemId, answer = "completely wrong answer xyz" } }
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var score = body.GetProperty("score").GetDouble();
        Assert.True(score < 50, $"Expected score < 50 for wrong answer, got {score}");
    }

    // ── SeenCount increments after practice ───────────────────────────────────

    [Fact]
    public async Task SubmitVocabAttempt_IncrementsSeenCount()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vp_seen_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        Guid vocabItemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = db.StudentProfiles.First(p => p.UserId == userId);
            var item = AddVocabItem(db, profile.Id, "could you please", "polite_request");
            await db.SaveChangesAsync();
            vocabItemId = item.Id;
        }

        var (activityId, _) = await CreateVocabActivityForStudent(userId, vocabItemId);

        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt", new
        {
            submittedContent = "",
            answers = new[] { new { vocabularyItemId = vocabItemId, answer = "could you please" } }
        });

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var updated = db2.StudentVocabularyItems.First(v => v.Id == vocabItemId);
        Assert.True(updated.SeenCount >= 2, $"Expected SeenCount >= 2, got {updated.SeenCount}");
    }

    // ── New → Practising after correct practice ───────────────────────────────

    [Fact]
    public async Task SubmitVocabAttempt_NewItem_BecomesPractisingAfterCorrect()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vp_status_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        Guid vocabItemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = db.StudentProfiles.First(p => p.UserId == userId);
            var item = AddVocabItem(db, profile.Id, "could you please", "polite_request");
            await db.SaveChangesAsync();
            vocabItemId = item.Id;
            Assert.Equal(VocabularyItemStatus.New, item.Status);
        }

        var (activityId, _) = await CreateVocabActivityForStudent(userId, vocabItemId);

        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt", new
        {
            submittedContent = "",
            answers = new[] { new { vocabularyItemId = vocabItemId, answer = "could you please" } }
        });

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var updated = db2.StudentVocabularyItems.First(v => v.Id == vocabItemId);
        Assert.Equal(VocabularyItemStatus.Practising, updated.Status);
    }

    // ── StrengthScore increases for correct answers ───────────────────────────

    [Fact]
    public async Task SubmitVocabAttempt_CorrectAnswer_IncreasesStrengthScore()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vp_strength_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        Guid vocabItemId;
        int initialStrength;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = db.StudentProfiles.First(p => p.UserId == userId);
            var item = AddVocabItem(db, profile.Id, "could you please", "polite_request");
            await db.SaveChangesAsync();
            vocabItemId = item.Id;
            initialStrength = item.StrengthScore;
        }

        var (activityId, _) = await CreateVocabActivityForStudent(userId, vocabItemId);

        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt", new
        {
            submittedContent = "",
            answers = new[] { new { vocabularyItemId = vocabItemId, answer = "could you please" } }
        });

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var updated = db2.StudentVocabularyItems.First(v => v.Id == vocabItemId);
        Assert.True(updated.StrengthScore > initialStrength, $"Expected StrengthScore to increase from {initialStrength}, got {updated.StrengthScore}");
    }

    // ── StrengthScore clamped at 0 for repeated wrong ─────────────────────────

    [Fact]
    public async Task StrengthScore_DoesNotGoBelowZero()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First();
        var item = AddVocabItem(db, profile.Id, $"test_clamp_{Guid.NewGuid():N}", "workplace_phrase");
        await db.SaveChangesAsync();

        // Multiple wrong answers via domain method
        for (int i = 0; i < 10; i++)
            item.RecordPractice(correct: false);

        Assert.Equal(0, item.StrengthScore);
    }

    // ── Activity history for VocabularyPractice returns safely ───────────────

    [Fact]
    public async Task ActivityHistory_VocabPractice_ReturnsSafely()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vp_hist_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var (activityId, vocabItemId) = await CreateVocabActivityForStudent(userId);

        // Submit an attempt
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt", new
        {
            submittedContent = "",
            answers = new[] { new { vocabularyItemId = vocabItemId, answer = "could you please" } }
        });

        // Get attempt history
        var histResp = await client.GetAsync($"/api/activity/{activityId}/attempts");
        Assert.Equal(HttpStatusCode.OK, histResp.StatusCode);

        var histBody = await histResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(histBody.TryGetProperty("activityId", out _), "activityId missing");
        Assert.True(histBody.TryGetProperty("attempts", out var attempts));
        Assert.True(attempts.GetArrayLength() >= 1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(Guid ActivityId, Guid VocabItemId)> CreateVocabActivityForStudent(
        Guid userId, Guid? specificVocabItemId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);

        Guid vocabItemId;
        if (specificVocabItemId.HasValue)
        {
            vocabItemId = specificVocabItemId.Value;
        }
        else
        {
            var item = AddVocabItem(db, profile.Id, "could you please", "polite_request");
            await db.SaveChangesAsync();
            vocabItemId = item.Id;
        }

        var term = "could you please";
        var contentJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "Vocabulary practice",
            instructions = "Fill in the blank.",
            practiceMode = "fill_blank",
            items = new[]
            {
                new
                {
                    vocabularyItemId = vocabItemId,
                    term,
                    prompt = $"_____ send me the updated file?",
                    expectedAnswer = term,
                    hint = "Polite request phrase.",
                    explanation = "A polite workplace request phrase.",
                }
            }
        });

        var activity = new LearningActivity(
            activityType: ActivityType.VocabularyPractice,
            source: ActivitySource.AiGenerated,
            title: "Vocabulary practice",
            difficulty: "B1",
            aiGeneratedContentJson: contentJson,
            learningModuleId: null);

        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();

        return (activity.Id, vocabItemId);
    }

    private static StudentVocabularyItem AddVocabItem(LinguaCoachDbContext db, Guid profileId, string term, string category)
    {
        var item = new StudentVocabularyItem(
            studentProfileId: profileId,
            term: term,
            suggestedPhrase: $"{term} send me the updated file?",
            meaningOrExplanation: "A polite way to make a workplace request.",
            exampleSentence: $"{term} confirm the meeting time?",
            category: category,
            source: VocabularyItemSource.AiExtractedFromWritingAttempt);
        db.StudentVocabularyItems.Add(item);
        return item;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
