using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Sessions;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Sessions;

/// <summary>
/// Phase 2 integration tests for the Exercise Pattern Engine.
///
/// Group A — DB-only tests (no HTTP): verify seeder and repository behaviour
///   using SQLite in-memory, one connection per test (xUnit creates a new instance per test).
///
/// Phase I2B — Group B (HTTP endpoint tests exercising the /prepare action and
/// GET /api/sessions/{id}) was deleted along with those endpoints (the legacy generation
/// pipeline and its lesson-runner UI). See
/// docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md.
/// </summary>

// ── Group A: DB-only pattern tests ────────────────────────────────────────────

public sealed class ExercisePatternPhase2DbTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly SqliteConnection _connection;

    public ExercisePatternPhase2DbTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
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

    // ── EmailReply pattern ────────────────────────────────────────────────────

    [Fact]
    public async Task EmailReply_Pattern_HasWritingScenarioActivityType()
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == ExercisePatternKey.EmailReply);
        Assert.Equal(ActivityType.WritingScenario, pattern.ActivityType);
    }

    [Fact]
    public async Task EmailReply_Pattern_HasAiStructuredMarkingMode()
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == ExercisePatternKey.EmailReply);
        Assert.Equal(MarkingMode.AiStructured, pattern.MarkingMode);
    }

    [Fact]
    public async Task EmailReply_Pattern_HasEmailReplyInteractionMode()
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == ExercisePatternKey.EmailReply);
        Assert.Equal(InteractionMode.EmailReply, pattern.InteractionMode);
    }

    [Fact]
    public async Task EmailReply_Pattern_IsCompatibleWithWritingTask()
    {
        var repo = new ExercisePatternRepository(_db);
        var results = await repo.GetByKindAsync(ExerciseKind.WritingTask);
        Assert.Contains(results, p => p.Key == ExercisePatternKey.EmailReply);
    }

    // ── SpokenResponseFromPrompt pattern ──────────────────────────────────────

    [Fact]
    public async Task SpokenResponseFromPrompt_Pattern_HasSpeakingRolePlayActivityType()
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == ExercisePatternKey.SpokenResponseFromPrompt);
        Assert.Equal(ActivityType.SpeakingRolePlay, pattern.ActivityType);
    }

    [Fact]
    public async Task SpokenResponseFromPrompt_Pattern_IsCompatibleWithSpeakingTask()
    {
        var repo = new ExercisePatternRepository(_db);
        var results = await repo.GetByKindAsync(ExerciseKind.SpeakingTask);
        Assert.Contains(results, p => p.Key == ExercisePatternKey.SpokenResponseFromPrompt);
    }

    // ── All 8 patterns have non-empty prompt keys ─────────────────────────────

    [Theory]
    [InlineData(ExercisePatternKey.PhraseMatch)]
    [InlineData(ExercisePatternKey.GapFillWorkplacePhrase)]
    [InlineData(ExercisePatternKey.ListenAndAnswer)]
    [InlineData(ExercisePatternKey.ListenAndGapFill)]
    [InlineData(ExercisePatternKey.EmailReply)]
    [InlineData(ExercisePatternKey.TeamsChatSimulation)]
    [InlineData(ExercisePatternKey.SpokenResponseFromPrompt)]
    [InlineData(ExercisePatternKey.LessonReflection)]
    public async Task AllPatterns_HaveNonEmptyPromptKeys(string key)
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == key);
        Assert.False(string.IsNullOrWhiteSpace(pattern.AiGeneratePromptKey));
        Assert.False(string.IsNullOrWhiteSpace(pattern.AiEvaluatePromptKey));
    }

    // ── Template keys match seeded pattern keys ───────────────────────────────

    [Theory]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public async Task TemplatePatternKeys_AllExistInSeededPatterns(int duration)
    {
        var seededKeys = await _db.ExercisePatterns.Select(p => p.Key).ToHashSetAsync();
        var steps = SessionDurationTemplates.GetTemplate(duration);
        foreach (var step in steps)
        {
            Assert.Contains(step.PatternKey, seededKeys);
        }
    }

    // ── ActivityGenerationContext carries OverridePromptKey ───────────────────

    [Fact]
    public async Task ActivityGenerationContext_WithOverridePromptKey_StoresEmailReplyKey()
    {
        var pattern = await _db.ExercisePatterns.SingleAsync(p => p.Key == ExercisePatternKey.EmailReply);
        var ctx = new Application.Activity.ActivityGenerationContext(
            ActivityType: pattern.ActivityType,
            CefrLevel: "B1",
            CareerContext: "Tech",
            LanguagePairCode: "fa-en",
            SourceLanguageName: "Persian",
            TargetLanguageName: "English",
            OverridePromptKey: pattern.AiGeneratePromptKey,
            ExercisePatternKey: pattern.Key);

        Assert.Equal(pattern.AiGeneratePromptKey, ctx.OverridePromptKey);
        Assert.Equal(ExercisePatternKey.EmailReply, ctx.ExercisePatternKey);
    }

    // ── LearningActivity.ExercisePatternKey persists correctly ────────────────

    [Fact]
    public async Task LearningActivity_WithEmailReplyKey_RoundTripsCorrectly()
    {
        var activity = new Domain.Entities.LearningActivity(
            ActivityType.WritingScenario, ActivitySource.AiGenerated,
            "Email task", "B1", "{}", exercisePatternKey: ExercisePatternKey.EmailReply);

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.LearningActivities.SingleAsync(a => a.Id == activity.Id);
        Assert.Equal(ExercisePatternKey.EmailReply, loaded.ExercisePatternKey);
    }

    [Fact]
    public async Task LearningActivity_WithSpokenResponseKey_RoundTripsCorrectly()
    {
        var activity = new Domain.Entities.LearningActivity(
            ActivityType.SpeakingRolePlay, ActivitySource.AiGenerated,
            "Speaking task", "B1", "{}", exercisePatternKey: ExercisePatternKey.SpokenResponseFromPrompt);

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.LearningActivities.SingleAsync(a => a.Id == activity.Id);
        Assert.Equal(ExercisePatternKey.SpokenResponseFromPrompt, loaded.ExercisePatternKey);
    }

    [Fact]
    public async Task LearningActivity_LessonReflection_StoresPatternKey()
    {
        var activity = new Domain.Entities.LearningActivity(
            ActivityType.WritingScenario, ActivitySource.SystemFallback,
            "Reflection", "B1", "{}", exercisePatternKey: ExercisePatternKey.LessonReflection);

        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.LearningActivities.SingleAsync(a => a.Id == activity.Id);
        Assert.Equal(ExercisePatternKey.LessonReflection, loaded.ExercisePatternKey);
    }
}
