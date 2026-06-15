using LinguaCoach.Domain;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Sessions;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Sessions;

/// <summary>
/// Phase 1 integration tests for the Exercise Pattern Engine.
/// Covers: seeder idempotency, correct metadata for all 8 MVP patterns,
/// repository query methods, and LearningActivity.ExercisePatternKey persistence.
///
/// xUnit creates a new class instance per test method, so each test gets
/// its own in-memory SQLite database.
/// </summary>
public sealed class ExercisePatternPhase1Tests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

    public ExercisePatternPhase1Tests()
    {
        // Use a fresh in-memory connection per test instance (xUnit creates one instance per test).
        // Keeping the connection open holds the in-memory database alive for the test duration.
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.EnsureCreated();
        ExercisePatternSeeder.SeedAsync(_db, NullLogger.Instance).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── Seeder ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Seeder_Seeds_AllTenMvpPatterns()
    {
        var count = await _db.ExercisePatterns.CountAsync();
        Assert.Equal(18, count);
    }

    [Fact]
    public async Task Seeder_IsIdempotent_RunningTwiceDoesNotDuplicate()
    {
        await ExercisePatternSeeder.SeedAsync(_db, NullLogger.Instance);
        var count = await _db.ExercisePatterns.CountAsync();
        Assert.Equal(18, count);
    }

    [Fact]
    public async Task Seeder_AllPatternsAreActive()
    {
        var inactive = await _db.ExercisePatterns.CountAsync(p => !p.IsActive);
        Assert.Equal(0, inactive);
    }

    // ── Pattern metadata correctness ──────────────────────────────────────────

    [Theory]
    [InlineData(ExercisePatternKey.PhraseMatch,             "Vocabulary",  (int)InteractionMode.MatchingPairs,    (int)MarkingMode.KeyedSelection, (int)ActivityType.VocabularyPractice)]
    [InlineData(ExercisePatternKey.GapFillWorkplacePhrase,  "Vocabulary",  (int)InteractionMode.GapFill,          (int)MarkingMode.ExactMatch,      (int)ActivityType.VocabularyPractice)]
    [InlineData(ExercisePatternKey.ListenAndAnswer,         "Listening",   (int)InteractionMode.AudioAndFreeText, (int)MarkingMode.AiStructured,    (int)ActivityType.ListeningComprehension)]
    [InlineData(ExercisePatternKey.ListenAndGapFill,        "Listening",   (int)InteractionMode.AudioAndGapFill,  (int)MarkingMode.ExactMatch,      (int)ActivityType.ListeningComprehension)]
    [InlineData(ExercisePatternKey.EmailReply,              "Writing",     (int)InteractionMode.EmailReply,       (int)MarkingMode.AiStructured,    (int)ActivityType.WritingScenario)]
    [InlineData(ExercisePatternKey.TeamsChatSimulation,     "Writing",     (int)InteractionMode.ChatReply,        (int)MarkingMode.AiStructured,    (int)ActivityType.WritingScenario)]
    [InlineData(ExercisePatternKey.SpokenResponseFromPrompt,"Speaking",    (int)InteractionMode.FreeTextEntry,    (int)MarkingMode.AiOpenEnded,     (int)ActivityType.SpeakingRolePlay)]
    [InlineData(ExercisePatternKey.LessonReflection,        "Reflection",  (int)InteractionMode.ReadOnly,         (int)MarkingMode.NoMarking,       (int)ActivityType.WritingScenario)]
    public async Task Pattern_HasCorrectMetadata(
        string key, string primarySkill,
        int interactionMode, int markingMode, int activityType)
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == key);

        Assert.Equal(primarySkill, pattern.PrimarySkill);
        Assert.Equal((InteractionMode)interactionMode, pattern.InteractionMode);
        Assert.Equal((MarkingMode)markingMode, pattern.MarkingMode);
        Assert.Equal((ActivityType)activityType, pattern.ActivityType);
        Assert.False(string.IsNullOrEmpty(pattern.AiGeneratePromptKey));
        Assert.False(string.IsNullOrEmpty(pattern.AiEvaluatePromptKey));
        Assert.False(string.IsNullOrEmpty(pattern.TeachingPurpose));
        Assert.True(pattern.EstimatedMinutes > 0);
    }

    [Theory]
    [InlineData(ExercisePatternKey.ListenAndAnswer,  true)]
    [InlineData(ExercisePatternKey.ListenAndGapFill, true)]
    [InlineData(ExercisePatternKey.PhraseMatch,      false)]
    [InlineData(ExercisePatternKey.EmailReply,       false)]
    public async Task Pattern_RequiresAudio_MatchesExpected(string key, bool expected)
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == key);
        Assert.Equal(expected, pattern.RequiresAudio);
    }

    // ── CompatibleKinds ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(ExercisePatternKey.PhraseMatch,             ExerciseKind.VocabularyWarmup, true)]
    [InlineData(ExercisePatternKey.PhraseMatch,             ExerciseKind.WritingTask,      false)]
    [InlineData(ExercisePatternKey.GapFillWorkplacePhrase,  ExerciseKind.VocabularyWarmup, true)]
    [InlineData(ExercisePatternKey.GapFillWorkplacePhrase,  ExerciseKind.ContextInput,     true)]
    [InlineData(ExercisePatternKey.ListenAndAnswer,         ExerciseKind.ListeningInput,   true)]
    [InlineData(ExercisePatternKey.ListenAndAnswer,         ExerciseKind.ContextInput,     true)]
    [InlineData(ExercisePatternKey.ListenAndGapFill,        ExerciseKind.ListeningInput,   true)]
    [InlineData(ExercisePatternKey.EmailReply,              ExerciseKind.WritingTask,      true)]
    [InlineData(ExercisePatternKey.TeamsChatSimulation,     ExerciseKind.WritingTask,      true)]
    [InlineData(ExercisePatternKey.SpokenResponseFromPrompt,ExerciseKind.SpeakingTask,     true)]
    [InlineData(ExercisePatternKey.LessonReflection,        ExerciseKind.Review,           true)]
    [InlineData(ExercisePatternKey.LessonReflection,        ExerciseKind.WritingTask,      false)]
    public async Task Pattern_CompatibleKinds_ContainsExpectedKind(
        string key, ExerciseKind kind, bool shouldContain)
    {
        var repo = new ExercisePatternRepository(_db);
        var results = await repo.GetByKindAsync(kind);
        Assert.Equal(shouldContain, results.Any(p => p.Key == key));
    }

    // ── Repository ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByKeyAsync_ReturnsPattern_WhenExists()
    {
        var repo = new ExercisePatternRepository(_db);
        var pattern = await repo.GetByKeyAsync(ExercisePatternKey.EmailReply);
        Assert.NotNull(pattern);
        Assert.Equal(ExercisePatternKey.EmailReply, pattern.Key);
    }

    [Fact]
    public async Task GetByKeyAsync_ReturnsNull_WhenNotFound()
    {
        var repo = new ExercisePatternRepository(_db);
        var pattern = await repo.GetByKeyAsync("nonexistent_pattern");
        Assert.Null(pattern);
    }

    [Fact]
    public async Task GetAllActiveAsync_ReturnsAllTenPatterns()
    {
        var repo = new ExercisePatternRepository(_db);
        var all = await repo.GetAllActiveAsync();
        Assert.Equal(18, all.Count);
    }

    [Fact]
    public async Task GetAllActiveAsync_ExcludesDeactivatedPatterns()
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == ExercisePatternKey.PhraseMatch);
        pattern.Deactivate();
        await _db.SaveChangesAsync();

        var repo = new ExercisePatternRepository(_db);
        var all = await repo.GetAllActiveAsync();
        Assert.Equal(17, all.Count);
        Assert.DoesNotContain(all, p => p.Key == ExercisePatternKey.PhraseMatch);
    }

    [Fact]
    public async Task GetByKindAsync_VocabularyWarmup_ReturnsPhraseMatchAndGapFill()
    {
        var repo = new ExercisePatternRepository(_db);
        var results = await repo.GetByKindAsync(ExerciseKind.VocabularyWarmup);

        Assert.Contains(results, p => p.Key == ExercisePatternKey.PhraseMatch);
        Assert.Contains(results, p => p.Key == ExercisePatternKey.GapFillWorkplacePhrase);
    }

    [Fact]
    public async Task GetByKindAsync_WritingTask_ReturnsEmailReplyAndTeamsChat()
    {
        var repo = new ExercisePatternRepository(_db);
        var results = await repo.GetByKindAsync(ExerciseKind.WritingTask);

        Assert.Contains(results, p => p.Key == ExercisePatternKey.EmailReply);
        Assert.Contains(results, p => p.Key == ExercisePatternKey.TeamsChatSimulation);
        Assert.DoesNotContain(results, p => p.Key == ExercisePatternKey.PhraseMatch);
    }

    [Fact]
    public async Task GetByKindAsync_Review_ReturnsOnlyLessonReflection()
    {
        var repo = new ExercisePatternRepository(_db);
        var results = await repo.GetByKindAsync(ExerciseKind.Review);

        Assert.Single(results);
        Assert.Equal(ExercisePatternKey.LessonReflection, results[0].Key);
    }

    [Fact]
    public async Task GetByKindAsync_SpeakingTask_ReturnsSpokenResponseAndRoleplayTurn()
    {
        var repo = new ExercisePatternRepository(_db);
        var results = await repo.GetByKindAsync(ExerciseKind.SpeakingTask);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, p => p.Key == ExercisePatternKey.SpokenResponseFromPrompt);
        Assert.Contains(results, p => p.Key == ExercisePatternKey.SpeakingRoleplayTurn);
    }

    // ── LearningActivity.ExercisePatternKey persistence ───────────────────────

    [Fact]
    public async Task LearningActivity_ExercisePatternKey_PersistedAndLoaded()
    {
        var activity = new LearningActivity(
            activityType: ActivityType.WritingScenario,
            source: ActivitySource.AiGenerated,
            title: "Email Reply Exercise",
            difficulty: "B1",
            aiGeneratedContentJson: "{}",
            exercisePatternKey: ExercisePatternKey.EmailReply);

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.LearningActivities.SingleAsync(a => a.Id == activity.Id);
        Assert.Equal(ExercisePatternKey.EmailReply, loaded.ExercisePatternKey);
    }

    [Fact]
    public async Task LearningActivity_ExercisePatternKey_NullWhenNotSet()
    {
        var activity = new LearningActivity(
            activityType: ActivityType.WritingScenario,
            source: ActivitySource.AiGenerated,
            title: "Writing Exercise",
            difficulty: "B1",
            aiGeneratedContentJson: "{}");

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.LearningActivities.SingleAsync(a => a.Id == activity.Id);
        Assert.Null(loaded.ExercisePatternKey);
    }

    [Fact]
    public async Task ExistingLearningActivity_WithNullPatternKey_LoadsCorrectly()
    {
        var activity = new LearningActivity(
            ActivityType.ListeningComprehension, ActivitySource.SystemFallback,
            "Old activity", "A2", "{}");
        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.LearningActivities.SingleAsync(a => a.Id == activity.Id);
        Assert.Null(loaded.ExercisePatternKey);
        Assert.Equal(ActivityType.ListeningComprehension, loaded.ActivityType);
    }
}
