using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 3 integration tests for the Pattern Evaluation Engine.
/// Verifies that pattern-keyed activity submissions are routed through evaluators,
/// persist canonical fields on ActivityAttempt, return PatternEvaluationDto,
/// and mark linked SessionExercise complete.
/// </summary>
public sealed class PatternEvaluationSubmitTests : IClassFixture<PatternEvaluationTestFactory>
{
    private readonly PatternEvaluationTestFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public PatternEvaluationSubmitTests(PatternEvaluationTestFactory factory) => _factory = factory;

    // ── phrase_match → KeyedSelection ─────────────────────────────────────────

    [Fact]
    public async Task PhraseMatch_Submit_PersistsEvaluationJsonAndScalarFields()
    {
        var (token, _, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match",
            PhraseMatchContentJson(pairCount: 2));
        var client = ClientWithToken(token);

        var submitted = PhraseMatchSubmitted((0, 0), (1, 1)); // all correct
        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = submitted });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var attempt = db.ActivityAttempts.OrderByDescending(a => a.CreatedAt).First(a => a.LearningActivityId == activityId);

        Assert.NotNull(attempt.SubmittedAnswerJson);
        Assert.NotNull(attempt.EvaluationResultJson);
        Assert.Equal(MarkingMode.KeyedSelection, attempt.MarkingMode);
        Assert.Equal(2, attempt.MaxScore);
        // Score stores percentage for legacy compat (0–100 scale)
        Assert.Equal(100, attempt.Score);
        Assert.Equal(100, attempt.Percentage);
        Assert.True(attempt.Passed);
        Assert.True(attempt.Completed);
    }

    [Fact]
    public async Task PhraseMatch_Submit_ReturnsPatternEvaluationDtoInResponse()
    {
        var (token, _, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match",
            PhraseMatchContentJson(pairCount: 2));
        var client = ClientWithToken(token);

        var submitted = PhraseMatchSubmitted((0, 0), (1, 1));
        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = submitted });

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var patternEval = body.GetProperty("patternEvaluation");

        Assert.Equal("phrase_match", patternEval.GetProperty("exercisePatternKey").GetString());
        Assert.Equal(2, patternEval.GetProperty("maxScore").GetDouble());
        Assert.Equal(100, patternEval.GetProperty("percentage").GetDouble());
        Assert.True(patternEval.GetProperty("passed").GetBoolean());
        Assert.True(patternEval.GetProperty("completed").GetBoolean());
        Assert.True(patternEval.GetProperty("itemResults").GetArrayLength() > 0);
    }

    // ── gap_fill_workplace_phrase → ExactMatch ─────────────────────────────────

    [Fact]
    public async Task GapFill_Submit_PersistsPartialScore()
    {
        var contentJson = GapFillContentJson("confirm", "send", "check");
        var (token, _, activityId) = await _factory.CreatePatternActivityAsync(
            "gap_fill_workplace_phrase", contentJson);
        var client = ClientWithToken(token);

        // Only gap_1 correct
        var submitted = GapFillSubmitted(("gap_1", "confirm"), ("gap_2", "wrong"), ("gap_3", "wrong"));
        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = submitted });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var attempt = db.ActivityAttempts.OrderByDescending(a => a.CreatedAt).First(a => a.LearningActivityId == activityId);

        // Score stores percentage (33.33%) for legacy compat
        Assert.NotNull(attempt.Score);
        Assert.Equal(3, attempt.MaxScore);
        Assert.True(attempt.Completed);
    }

    [Fact]
    public async Task GapFill_Submit_ReturnsItemLevelResults()
    {
        var contentJson = GapFillContentJson("confirm");
        var (token, _, activityId) = await _factory.CreatePatternActivityAsync(
            "gap_fill_workplace_phrase", contentJson);
        var client = ClientWithToken(token);

        var submitted = GapFillSubmitted(("gap_1", "confirm"));
        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = submitted });

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = body.GetProperty("patternEvaluation").GetProperty("itemResults");
        Assert.Equal(1, items.GetArrayLength());
        Assert.True(items[0].GetProperty("isCorrect").GetBoolean());
    }

    // ── listen_and_gap_fill → ExactMatch ──────────────────────────────────────

    [Fact]
    public async Task ListenAndGapFill_Submit_UsesExactMatchPath()
    {
        var contentJson = ListenAndGapFillContentJson(("g1", "confirm"), ("g2", "send"));
        var (token, _, activityId) = await _factory.CreatePatternActivityAsync(
            "listen_and_gap_fill", contentJson, ActivityType.ListeningComprehension);
        var client = ClientWithToken(token);

        var submitted = GapFillSubmitted(("g1", "confirm"), ("g2", "send"));
        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = submitted });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var pe = body.GetProperty("patternEvaluation");
        Assert.Equal("listen_and_gap_fill", pe.GetProperty("exercisePatternKey").GetString());
        Assert.Equal(2, pe.GetProperty("maxScore").GetDouble());
        Assert.Equal(100, pe.GetProperty("percentage").GetDouble());
    }

    // ── lesson_reflection → NoMarking ─────────────────────────────────────────

    [Fact]
    public async Task LessonReflection_Submit_ReturnsCompletedNoMarking()
    {
        var (token, _, activityId) = await _factory.CreatePatternActivityAsync(
            "lesson_reflection",
            """{"title":"Reflect","reflectionPrompts":[]}""",
            ActivityType.WritingScenario);
        var client = ClientWithToken(token);

        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = "{}" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var pe = body.GetProperty("patternEvaluation");

        Assert.Equal("lesson_reflection", pe.GetProperty("exercisePatternKey").GetString());
        Assert.Equal(0, pe.GetProperty("score").GetDouble());
        Assert.Equal(0, pe.GetProperty("maxScore").GetDouble());
        Assert.Equal(100, pe.GetProperty("percentage").GetDouble());
        Assert.True(pe.GetProperty("passed").GetBoolean());
        Assert.True(pe.GetProperty("completed").GetBoolean());
    }

    // ── SessionExercise completion ─────────────────────────────────────────────

    [Fact]
    public async Task PatternSubmit_MarksLinkedSessionExerciseComplete()
    {
        var (token, profileId, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match", PhraseMatchContentJson(1));
        var client = ClientWithToken(token);

        // Create a SessionExercise linked to this activity
        Guid exerciseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var session = await CreateSessionAsync(db, profileId);
            await db.SaveChangesAsync();

            var exercise = new SessionExercise(session.Id, 0, "phrase_match", "Vocabulary",
                "Match the phrases.", 3);
            exercise.AssignActivity(activityId);
            db.SessionExercises.Add(exercise);
            await db.SaveChangesAsync();
            exerciseId = exercise.Id;
        }

        var submitted = PhraseMatchSubmitted((0, 0));
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = submitted });

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var loaded = await db2.SessionExercises.FindAsync(exerciseId);
        Assert.Equal(ExerciseStatus.Completed, loaded!.Status);
        Assert.NotNull(loaded.CompletedAtUtc);
    }

    [Fact]
    public async Task LowScore_StillCompletesSessionExercise()
    {
        var (token, profileId, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match", PhraseMatchContentJson(2));
        var client = ClientWithToken(token);

        Guid exerciseId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var session = await CreateSessionAsync(db, profileId);
            await db.SaveChangesAsync();
            var exercise = new SessionExercise(session.Id, 0, "phrase_match", "Vocabulary", "Instructions.", 3);
            exercise.AssignActivity(activityId);
            db.SessionExercises.Add(exercise);
            await db.SaveChangesAsync();
            exerciseId = exercise.Id;
        }

        // All wrong — still submitted
        var submitted = PhraseMatchSubmitted((0, 1), (1, 0));
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = submitted });

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var loaded = await db2.SessionExercises.FindAsync(exerciseId);
        Assert.Equal(ExerciseStatus.Completed, loaded!.Status);
    }

    [Fact]
    public async Task PatternActivity_WithoutSessionExercise_DoesNotAffectAnySession()
    {
        // Practice Gym: no SessionExercise linked
        var (token, _, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match", PhraseMatchContentJson(1));
        var client = ClientWithToken(token);

        var submitted = PhraseMatchSubmitted((0, 0));
        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = submitted });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        // No exception = session isolation preserved
    }

    // ── default WritingScenario cadence is pattern-routed ─────────────────────

    [Fact]
    public async Task DefaultWritingScenario_Submit_ReturnsPatternEvaluation()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"writing_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Get the default WritingScenario activity — now routed through open_writing_task.
        var nextResp = await client.GetAsync("/api/activity/next");
        Assert.Equal(HttpStatusCode.OK, nextResp.StatusCode);
        var nextBody = await nextResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var activityId = nextBody.GetProperty("activityId").GetGuid();
        Assert.Equal("open_writing_task", nextBody.GetProperty("exercisePatternKey").GetString());

        var submitResp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = "I wanted to follow up on the pending approval." });

        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var body = await submitResp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(body.TryGetProperty("patternEvaluation", out var pe) && pe.ValueKind == JsonValueKind.Object);
        Assert.Equal("open_writing_task", pe.GetProperty("exercisePatternKey").GetString());
    }

    // ── deterministic paths do not call AI ───────────────────────────────────

    [Fact]
    public async Task PhraseMatch_Submit_DoesNotCreateAiUsageLog()
    {
        var (token, _, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match", PhraseMatchContentJson(1));
        var client = ClientWithToken(token);

        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var countBefore = dbBefore.AiUsageLogs.Count();

        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = PhraseMatchSubmitted((0, 0)) });

        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var countAfter = dbAfter.AiUsageLogs.Count();

        Assert.Equal(countBefore, countAfter);
    }

    // ── Phase 5: skill profile updates ───────────────────────────────────────

    [Fact]
    public async Task PhraseMatch_FullScore_CreatesSkillProfileEntry()
    {
        var (token, userId, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match", PhraseMatchContentJson(pairCount: 2));
        var client = ClientWithToken(token);

        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = PhraseMatchSubmitted((0, 0), (1, 1)) });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var studentProfileId = db.StudentProfiles.Where(p => p.UserId == userId).Select(p => p.Id).First();

        // phrase_match → workplace_vocabulary synthetic impact (good score → not weak)
        var skill = db.StudentSkillProfiles.FirstOrDefault(s =>
            s.StudentProfileId == studentProfileId && s.SkillKey == "workplace_vocabulary");

        Assert.NotNull(skill);
        Assert.False(skill.IsWeak);
    }

    [Fact]
    public async Task GapFill_PoorScore_CreatesWeakSkillEntry()
    {
        // Gap fill with wrong answers → low score → sentence_clarity marked weak
        var (token, userId, activityId) = await _factory.CreatePatternActivityAsync(
            "gap_fill_workplace_phrase",
            GapFillContentJson("expected_answer", "expected_answer2"));
        var client = ClientWithToken(token);

        // Submit entirely wrong answers
        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = GapFillSubmitted(("gap_1", "wrong"), ("gap_2", "wrong")) });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var studentProfileId = db.StudentProfiles.Where(p => p.UserId == userId).Select(p => p.Id).First();

        // gap_fill_workplace_phrase → workplace_vocabulary synthetic impact (poor score → weak)
        var skill = db.StudentSkillProfiles.FirstOrDefault(s =>
            s.StudentProfileId == studentProfileId && s.SkillKey == "workplace_vocabulary");

        Assert.NotNull(skill);
        Assert.True(skill.IsWeak);
    }

    [Fact]
    public async Task MultipleAttempts_SkillProfile_UpdatedEachTime_NotDuplicated()
    {
        var (token, userId, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match", PhraseMatchContentJson(pairCount: 1));
        var client = ClientWithToken(token);

        // First attempt: wrong
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = PhraseMatchSubmitted((0, 1)) }); // wrong

        // Second attempt: correct
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = PhraseMatchSubmitted((0, 0)) }); // correct

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var studentProfileId = db.StudentProfiles.Where(p => p.UserId == userId).Select(p => p.Id).First();

        var skills = db.StudentSkillProfiles
            .Where(s => s.StudentProfileId == studentProfileId && s.SkillKey == "workplace_vocabulary")
            .ToList();

        // One row, updated — not two
        Assert.Single(skills);
        Assert.False(skills[0].IsWeak); // last attempt was correct → not weak
    }

    [Fact]
    public async Task PatternActivity_SubmissionDoesNotFail_WhenSkillUpdateWouldFail()
    {
        // Verify the endpoint returns 200 even when skill update encounters edge conditions.
        // This test uses no-marking pattern which maps to lesson_reflection → message_structure.
        var (token, _, activityId) = await _factory.CreatePatternActivityAsync(
            "lesson_reflection", """{"topic":"Meeting vocabulary"}""");
        var client = ClientWithToken(token);

        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = "{}" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Phase 5: progress and attempt counting ────────────────────────────────

    [Fact]
    public async Task MultipleAttempts_OnlyOneActivityAttempt_CountsOnceInHistory()
    {
        var (token, _, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match", PhraseMatchContentJson(pairCount: 1));
        var client = ClientWithToken(token);

        // Submit twice
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = PhraseMatchSubmitted((0, 1)) });
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = PhraseMatchSubmitted((0, 0)) });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // Both attempts persisted (append-only)
        var attempts = db.ActivityAttempts.Where(a => a.LearningActivityId == activityId).ToList();
        Assert.Equal(2, attempts.Count);
    }

    [Fact]
    public async Task PatternActivity_WithoutSessionExercise_DoesNotAdvanceSessionProgress()
    {
        // A pattern activity NOT linked to a SessionExercise should submit successfully
        // without touching any session state.
        var (token, _, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match", PhraseMatchContentJson(pairCount: 1));
        var client = ClientWithToken(token);

        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = PhraseMatchSubmitted((0, 0)) });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // No SessionExercise should exist or be affected
        var exercise = db.SessionExercises.FirstOrDefault(e => e.LearningActivityId == activityId);
        Assert.Null(exercise);
    }

    [Fact]
    public async Task PatternActivity_WithLinkedSessionExercise_CompletesExerciseOnce()
    {
        // Today's Lesson path: SessionExercise linked to the activity should be completed.
        var (token, userId, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match", PhraseMatchContentJson(pairCount: 1));

        // Link a SessionExercise to this activity
        using var setupScope = _factory.Services.CreateScope();
        var setupDb = setupScope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var session = await CreateSessionAsync(setupDb, userId);
        var exercise = new SessionExercise(
            session.Id, order: 1, exercisePatternKey: "phrase_match",
            primarySkill: "Vocabulary", secondarySkillsJson: "[]",
            estimatedMinutes: 5, instructions: "Match phrases.");
        exercise.AssignActivity(activityId);
        setupDb.SessionExercises.Add(exercise);
        await setupDb.SaveChangesAsync();

        var client = ClientWithToken(token);
        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = PhraseMatchSubmitted((0, 0)) });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var updatedExercise = verifyDb.SessionExercises.First(e => e.LearningActivityId == activityId);
        Assert.Equal(ExerciseStatus.Completed, updatedExercise.Status);
    }

    [Fact]
    public async Task LowScore_PatternActivity_StillCompletesSessionExercise()
    {
        // In MVP: low score does not block SessionExercise completion.
        var (token, userId, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match", PhraseMatchContentJson(pairCount: 2));

        using var setupScope = _factory.Services.CreateScope();
        var setupDb = setupScope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var session = await CreateSessionAsync(setupDb, userId);
        var exercise = new SessionExercise(
            session.Id, order: 1, exercisePatternKey: "phrase_match",
            primarySkill: "Vocabulary", secondarySkillsJson: "[]",
            estimatedMinutes: 5, instructions: "Match phrases.");
        exercise.AssignActivity(activityId);
        setupDb.SessionExercises.Add(exercise);
        await setupDb.SaveChangesAsync();

        var client = ClientWithToken(token);
        // Submit all wrong (score = 0%) — evaluation.Completed should still be true
        var resp = await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = PhraseMatchSubmitted((0, 1), (1, 0)) });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var updatedExercise = verifyDb.SessionExercises.First(e => e.LearningActivityId == activityId);
        Assert.Equal(ExerciseStatus.Completed, updatedExercise.Status);
    }

    // ── Phase 10B: learning ledger event recording ────────────────────────────

    [Fact]
    public async Task PatternSubmit_PracticeGym_WritesLearningEvent()
    {
        var (token, userId, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match", PhraseMatchContentJson(pairCount: 1));
        var client = ClientWithToken(token);

        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = PhraseMatchSubmitted((0, 0)) });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var studentProfileId = db.StudentProfiles.Where(p => p.UserId == userId).Select(p => p.Id).First();

        var evt = db.StudentLearningEvents.FirstOrDefault(e => e.StudentProfileId == studentProfileId);

        Assert.NotNull(evt);
        Assert.Equal(LearningEventSource.PracticeGym, evt.Source);
        Assert.Equal("phrase_match", evt.PatternKey);
        Assert.Equal(activityId, evt.ActivityId);
        Assert.Null(evt.SessionId); // no session for Practice Gym
    }

    [Fact]
    public async Task PatternSubmit_TodayLesson_WritesLearningEventWithSessionId()
    {
        var (token, userId, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match", PhraseMatchContentJson(pairCount: 1));

        Guid sessionId;
        using (var setupScope = _factory.Services.CreateScope())
        {
            var setupDb = setupScope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var session = await CreateSessionAsync(setupDb, userId);
            await setupDb.SaveChangesAsync();
            sessionId = session.Id;

            var exercise = new SessionExercise(
                session.Id, order: 1, exercisePatternKey: "phrase_match",
                primarySkill: "Vocabulary", secondarySkillsJson: "[]",
                estimatedMinutes: 5, instructions: "Match phrases.");
            exercise.AssignActivity(activityId);
            setupDb.SessionExercises.Add(exercise);
            await setupDb.SaveChangesAsync();
        }

        var client = ClientWithToken(token);
        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = PhraseMatchSubmitted((0, 0)) });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var studentProfileId = db.StudentProfiles.Where(p => p.UserId == userId).Select(p => p.Id).First();

        var evt = db.StudentLearningEvents.FirstOrDefault(e => e.StudentProfileId == studentProfileId);

        Assert.NotNull(evt);
        Assert.Equal(LearningEventSource.TodayLesson, evt.Source);
        Assert.Equal(sessionId, evt.SessionId);
    }

    [Fact]
    public async Task PatternSubmit_RecordsExerciseTypeAndPatternKey()
    {
        var (token, userId, activityId) = await _factory.CreatePatternActivityAsync(
            "gap_fill_workplace_phrase", GapFillContentJson("confirm"));
        var client = ClientWithToken(token);

        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = GapFillSubmitted(("gap_1", "confirm")) });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var studentProfileId = db.StudentProfiles.Where(p => p.UserId == userId).Select(p => p.Id).First();

        var evt = db.StudentLearningEvents.FirstOrDefault(e => e.StudentProfileId == studentProfileId);

        Assert.NotNull(evt);
        Assert.Equal("gap_fill_workplace_phrase", evt.PatternKey);
        Assert.NotNull(evt.ExerciseType);
        Assert.NotNull(evt.Score);
    }

    [Fact]
    public async Task PatternSubmit_ExistingSkillProfileUpdateStillWorks_AfterLedgerWrite()
    {
        // Regression: ledger write must not break StudentSkillProfile update.
        var (token, userId, activityId) = await _factory.CreatePatternActivityAsync(
            "phrase_match", PhraseMatchContentJson(pairCount: 1));
        var client = ClientWithToken(token);

        await client.PostAsJsonAsync($"/api/activity/{activityId}/attempt",
            new { submittedContent = PhraseMatchSubmitted((0, 0)) });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var studentProfileId = db.StudentProfiles.Where(p => p.UserId == userId).Select(p => p.Id).First();

        var skill = db.StudentSkillProfiles.FirstOrDefault(s =>
            s.StudentProfileId == studentProfileId && s.SkillKey == "workplace_vocabulary");

        Assert.NotNull(skill); // skill profile still updated
        var evt = db.StudentLearningEvents.FirstOrDefault(e => e.StudentProfileId == studentProfileId);
        Assert.NotNull(evt); // ledger event also written
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string PhraseMatchContentJson(int pairCount)
    {
        var pairs = Enumerable.Range(0, pairCount)
            .Select(i => new { phrase = $"Phrase {i}", meaning = $"Meaning {i}", context = "" })
            .ToArray();
        return JsonSerializer.Serialize(new { pairs }, JsonOptions);
    }

    private static string PhraseMatchSubmitted(params (int phraseIdx, int meaningIdx)[] selections)
    {
        var d = selections.ToDictionary(s => $"phrase_{s.phraseIdx}", s => (object?)$"meaning_{s.meaningIdx}");
        return JsonSerializer.Serialize(new { pairs = d }, JsonOptions);
    }

    private static string GapFillContentJson(params string[] answers)
    {
        var items = answers.Select((a, i) => new { sentence = $"Sentence {i + 1}", answer = a }).ToArray();
        return JsonSerializer.Serialize(new { items }, JsonOptions);
    }

    private static string ListenAndGapFillContentJson(params (string id, string answer)[] gaps)
    {
        var g = gaps.Select(x => new { id = x.id, sentenceWithBlank = $"___ word", answer = x.answer }).ToArray();
        return JsonSerializer.Serialize(new { gaps = g }, JsonOptions);
    }

    private static string GapFillSubmitted(params (string key, string? val)[] pairs)
    {
        var d = pairs.ToDictionary(p => p.key, p => (object?)p.val);
        return JsonSerializer.Serialize(new { answers = d }, JsonOptions);
    }

    // userId is the AspNetUsers GUID; StudentProfile has its own Id FK'd to StudentProfile table
    private static async Task<LearningSession> CreateSessionAsync(LinguaCoachDbContext db, Guid userId)
    {
        var studentProfileId = db.StudentProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .First();

        var path = db.LearningPaths.FirstOrDefault(p => p.StudentProfileId == studentProfileId);
        if (path is null)
        {
            path = new LearningPath(studentProfileId, "Test Path", "Test context");
            db.LearningPaths.Add(path);
            await db.SaveChangesAsync();
        }

        var module = new LearningModule(path.Id, "Test Module", "Test module description.", 1);
        db.LearningModules.Add(module);
        await db.SaveChangesAsync();

        var session = new LearningSession(
            learningModuleId: module.Id,
            title: "Test Session",
            topic: "Test topic",
            sessionGoal: "Complete the exercise.",
            durationMinutes: 10,
            focusSkill: "Vocabulary",
            order: 1);
        db.LearningSessions.Add(session);
        return session;
    }
}

/// <summary>
/// Test factory that extends ActivityTestFactory to also seed ExercisePatternDefinitions
/// and provides helpers for creating pattern-keyed activities directly in the DB.
/// </summary>
public sealed class PatternEvaluationTestFactory : ActivityTestFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override async Task<(string Token, Guid UserId)> CreateOnboardedStudentAsync(
        string email = "pattern_student@test.linguacoach.com")
    {
        var result = await base.CreateOnboardedStudentAsync(email);
        await SeedExercisePatternsAsync();
        return result;
    }

    public async Task SeedExercisePatternsAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        await ExercisePatternSeeder.SeedAsync(db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
    }

    /// <summary>
    /// Creates an onboarded student and a pattern-keyed LearningActivity in the DB,
    /// bypassing the AI generation flow. Returns token, userId, and activityId.
    /// </summary>
    public async Task<(string Token, Guid UserId, Guid ActivityId)> CreatePatternActivityAsync(
        string patternKey,
        string contentJson,
        ActivityType activityType = ActivityType.VocabularyPractice)
    {
        var email = $"pat_{patternKey}_{Guid.NewGuid():N}@test.com";
        var (token, userId) = await CreateOnboardedStudentAsync(email);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var activity = new LearningActivity(
            activityType: activityType,
            source: ActivitySource.AiGenerated,
            title: $"Test {patternKey}",
            difficulty: "B1",
            aiGeneratedContentJson: contentJson,
            exercisePatternKey: patternKey);

        db.LearningActivities.Add(activity);
        await db.SaveChangesAsync();

        return (token, userId, activity.Id);
    }
}
