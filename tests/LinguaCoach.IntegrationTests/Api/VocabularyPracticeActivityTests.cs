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

    // ── VocabularyPractice not returned when too few vocab items ──────────────

    [Fact]
    public async Task GetNext_WithFewVocabItems_ReturnsWritingScenario()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vp_notenough_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Get activity — no vocab items exist, so should be WritingScenario
        var resp = await client.GetAsync("/api/activity/next");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var actType = body.GetProperty("activityType").GetString();
        Assert.Equal("writingScenario", actType);
    }

    // ── VocabularyPractice returned with enough vocab + correct attempt count ─

    [Fact]
    public async Task GetNext_WithEnoughVocabAtEveryFourthAttempt_ReturnsVocabPractice()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vp_enough_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);

        // Add 3+ vocab items
        AddVocabItem(db, profile.Id, "could you please", "polite_request");
        AddVocabItem(db, profile.Id, "at your earliest convenience", "workplace_phrase");
        AddVocabItem(db, profile.Id, "please find attached", "workplace_phrase");
        await db.SaveChangesAsync();

        // Add exactly 4 attempts (totalAttempts % 4 == 0)
        var activity = db.LearningActivities.First();
        for (int i = 0; i < 4; i++)
        {
            db.ActivityAttempts.Add(new ActivityAttempt(
                profile.Id, activity.Id, $"Test attempt {i}", "{}", "activity_evaluate_writing", score: 70.0));
        }
        await db.SaveChangesAsync();

        var resp = await client.GetAsync("/api/activity/next");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("vocabularyPractice", body.GetProperty("activityType").GetString());

        // Should have vocabItems
        Assert.True(body.TryGetProperty("vocabItems", out var vocabItems));
        Assert.Equal(JsonValueKind.Array, vocabItems.ValueKind);
        Assert.True(vocabItems.GetArrayLength() >= 3);

        // Should have practiceMode
        Assert.True(body.TryGetProperty("practiceMode", out var mode));
        Assert.Equal("fill_blank", mode.GetString());

        // Should NOT expose expectedAnswer to frontend
        foreach (var item in vocabItems.EnumerateArray())
            Assert.False(item.TryGetProperty("expectedAnswer", out _), "expectedAnswer should not be in response");
    }

    // ── Generated VocabularyPractice items belong to current student only ────

    [Fact]
    public async Task GetNext_VocabPractice_ItemsBelongToCurrentStudent()
    {
        var (tokenA, userIdA) = await _factory.CreateOnboardedStudentAsync($"vp_iso_a_{Guid.NewGuid():N}@test.com");
        var (tokenB, userIdB) = await _factory.CreateOnboardedStudentAsync($"vp_iso_b_{Guid.NewGuid():N}@test.com");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profileA = db.StudentProfiles.First(p => p.UserId == userIdA);
        var profileB = db.StudentProfiles.First(p => p.UserId == userIdB);

        // Add vocab to student A only
        AddVocabItem(db, profileA.Id, "could you please", "polite_request");
        AddVocabItem(db, profileA.Id, "at your earliest convenience", "workplace_phrase");
        AddVocabItem(db, profileA.Id, "please find attached", "workplace_phrase");
        await db.SaveChangesAsync();

        // Add 4 attempts for student B (so B hits the vocab interval)
        var activity = db.LearningActivities.First();
        for (int i = 0; i < 4; i++)
        {
            db.ActivityAttempts.Add(new ActivityAttempt(
                profileB.Id, activity.Id, $"B test {i}", "{}", "activity_evaluate_writing", score: 70.0));
        }
        await db.SaveChangesAsync();

        // Student B should NOT get VocabularyPractice (B has no vocab items)
        var clientB = ClientWithToken(tokenB);
        var respB = await clientB.GetAsync("/api/activity/next");
        Assert.Equal(HttpStatusCode.OK, respB.StatusCode);
        var bodyB = await respB.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("writingScenario", bodyB.GetProperty("activityType").GetString());
    }

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
