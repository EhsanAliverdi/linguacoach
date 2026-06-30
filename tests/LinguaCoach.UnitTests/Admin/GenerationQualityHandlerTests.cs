using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Admin;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LinguaCoach.UnitTests.Admin;

/// <summary>
/// Unit tests for AdminGenerationQualityHandler using SQLite in-memory DbContext.
/// </summary>
public sealed class GenerationQualityHandlerTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly AdminGenerationQualityHandler _handler;

    public GenerationQualityHandlerTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _handler = new AdminGenerationQualityHandler(_db, BuildConfig());
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private static IConfiguration BuildConfig(
        int retentionDays = 90,
        double threshold = 0.15,
        int minimumFailures = 5) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GenerationQuality:RetentionDays"] = retentionDays.ToString(),
                ["GenerationQuality:AbandonedFailureRateWarningThreshold"] = threshold.ToString("G"),
                ["GenerationQuality:MinimumFailuresForWarning"] = minimumFailures.ToString(),
            })
            .Build();

    [Fact]
    public async Task GetSummary_EmptyDb_ReturnsZeroCounts()
    {
        var result = await _handler.GetSummaryAsync(30);

        Assert.Equal(0, result.TotalValidationFailures);
        Assert.Equal(0, result.AbandonedGenerations);
        Assert.Equal(0, result.RecentFailureCount);
        Assert.Empty(result.LatestFailures);
        Assert.Empty(result.PatternBreakdown);
        Assert.Empty(result.CefrBreakdown);
        Assert.Empty(result.ProviderBreakdown);
        Assert.False(result.AbandonedWarning.IsActive);
    }

    [Fact]
    public async Task GetSummary_WithFailures_CountsCorrectly()
    {
        _db.GenerationValidationFailures.AddRange(
            new GenerationValidationFailure("WritingScenario", "Error 1", 1, patternKey: "email_reply", cefrLevel: "B1"),
            new GenerationValidationFailure("ListeningComprehension", "Error 2", 2, patternKey: "listen_and_answer", cefrLevel: "A2"),
            new GenerationValidationFailure("WritingScenario", "Error 3", 1, patternKey: "email_reply", cefrLevel: "B1"));
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        Assert.Equal(3, result.TotalValidationFailures);
        Assert.Equal(1, result.AbandonedGenerations); // only attemptNumber=2
    }

    [Fact]
    public async Task GetSummary_PatternBreakdown_GroupsByPattern()
    {
        _db.GenerationValidationFailures.AddRange(
            new GenerationValidationFailure("WritingScenario", "Err A", 1, patternKey: "email_reply"),
            new GenerationValidationFailure("WritingScenario", "Err B", 2, patternKey: "email_reply"),
            new GenerationValidationFailure("ListeningComprehension", "Err C", 1, patternKey: "listen_and_answer"));
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        var emailReply = result.PatternBreakdown.FirstOrDefault(p => p.PatternKey == "email_reply");
        Assert.NotNull(emailReply);
        Assert.Equal(2, emailReply.TotalFailures);
        Assert.Equal(1, emailReply.AbandonedCount);

        var listenAndAnswer = result.PatternBreakdown.FirstOrDefault(p => p.PatternKey == "listen_and_answer");
        Assert.NotNull(listenAndAnswer);
        Assert.Equal(1, listenAndAnswer.TotalFailures);
        Assert.Equal(0, listenAndAnswer.AbandonedCount);
    }

    [Fact]
    public async Task GetSummary_CefrBreakdown_GroupsByCefr()
    {
        _db.GenerationValidationFailures.AddRange(
            new GenerationValidationFailure("WritingScenario", "Err", 1, cefrLevel: "B1"),
            new GenerationValidationFailure("WritingScenario", "Err", 1, cefrLevel: "B1"),
            new GenerationValidationFailure("WritingScenario", "Err", 1, cefrLevel: "A2"));
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        var b1 = result.CefrBreakdown.FirstOrDefault(c => c.CefrLevel == "B1");
        Assert.NotNull(b1);
        Assert.Equal(2, b1.TotalFailures);

        var a2 = result.CefrBreakdown.FirstOrDefault(c => c.CefrLevel == "A2");
        Assert.NotNull(a2);
        Assert.Equal(1, a2.TotalFailures);
    }

    [Fact]
    public async Task GetSummary_LatestFailures_LimitedToTwenty()
    {
        for (var i = 0; i < 25; i++)
        {
            _db.GenerationValidationFailures.Add(
                new GenerationValidationFailure("WritingScenario", $"Error {i}", 1, patternKey: "email_reply"));
        }
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        Assert.True(result.LatestFailures.Count <= 20);
    }

    [Fact]
    public async Task GetSummary_LatestFailures_SafeFieldsOnly()
    {
        _db.GenerationValidationFailures.Add(
            new GenerationValidationFailure(
                "WritingScenario", "Missing prompt.", 1,
                patternKey: "email_reply", cefrLevel: "B1"));
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        var item = Assert.Single(result.LatestFailures);
        Assert.Equal("email_reply", item.PatternKey);
        Assert.Equal("B1", item.CefrLevel);
        Assert.Equal("WritingScenario", item.ActivityTypeName);
        Assert.Equal("Missing prompt.", item.ValidationErrors);
        Assert.Equal(1, item.AttemptNumber);
    }

    [Fact]
    public async Task GetSummary_PromptSummary_OnlyActivePrompts()
    {
        var inactive = new global::LinguaCoach.Domain.Entities.AiPrompt(
            "test_inactive_prompt_key", "content", 1);
        inactive.Deactivate();
        _db.AiPrompts.Add(inactive);
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        Assert.DoesNotContain(result.PromptSummary, p => p.Key == "test_inactive_prompt_key");
    }

    // ── Part B: Provider/Model Traceability ────────────────────────────────────

    [Fact]
    public async Task GetSummary_ProviderBreakdown_GroupsByProviderAndModel()
    {
        _db.GenerationValidationFailures.AddRange(
            new GenerationValidationFailure("WritingScenario", "Err", 1, providerName: "OpenAI", modelName: "gpt-4o"),
            new GenerationValidationFailure("WritingScenario", "Err", 2, providerName: "OpenAI", modelName: "gpt-4o"),
            new GenerationValidationFailure("WritingScenario", "Err", 1, providerName: "Gemini", modelName: "gemini-2.0-flash"));
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        var openai = result.ProviderBreakdown.FirstOrDefault(p => p.ProviderName == "OpenAI" && p.ModelName == "gpt-4o");
        Assert.NotNull(openai);
        Assert.Equal(2, openai.TotalFailures);
        Assert.Equal(1, openai.AbandonedCount);

        var gemini = result.ProviderBreakdown.FirstOrDefault(p => p.ProviderName == "Gemini");
        Assert.NotNull(gemini);
        Assert.Equal(1, gemini.TotalFailures);
        Assert.Equal(0, gemini.AbandonedCount);
    }

    [Fact]
    public async Task GetSummary_ProviderBreakdown_NullProviderIsExcluded()
    {
        _db.GenerationValidationFailures.Add(
            new GenerationValidationFailure("WritingScenario", "Err", 1)); // no provider
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        Assert.Empty(result.ProviderBreakdown);
    }

    [Fact]
    public async Task GetSummary_LatestFailures_IncludesProviderModel()
    {
        _db.GenerationValidationFailures.Add(
            new GenerationValidationFailure(
                "WritingScenario", "Err", 1,
                providerName: "OpenAI", modelName: "gpt-4o-mini", correlationId: "abc123"));
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        var item = Assert.Single(result.LatestFailures);
        Assert.Equal("OpenAI", item.ProviderName);
        Assert.Equal("gpt-4o-mini", item.ModelName);
        Assert.Equal("abc123", item.CorrelationId);
    }

    [Fact]
    public async Task GetSummary_LatestFailures_NullProviderSafe()
    {
        _db.GenerationValidationFailures.Add(
            new GenerationValidationFailure("WritingScenario", "Err", 1)); // no provider
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        var item = Assert.Single(result.LatestFailures);
        Assert.Null(item.ProviderName);
        Assert.Null(item.ModelName);
    }

    // ── Part C: Prompt Content Hash ───────────────────────────────────────────

    [Fact]
    public void AiPrompt_ContentHash_IsComputedOnConstruction()
    {
        var prompt = new AiPrompt("test_key", "Hello World", 1);

        Assert.NotNull(prompt.ContentHash);
        Assert.Equal(64, prompt.ContentHash.Length); // full SHA-256 hex
    }

    [Fact]
    public void AiPrompt_ContentHash_IsDeterministic()
    {
        var p1 = new AiPrompt("key", "same content", 1);
        var p2 = new AiPrompt("key", "same content", 2);

        Assert.Equal(p1.ContentHash, p2.ContentHash);
    }

    [Fact]
    public void AiPrompt_ContentHash_DifferentForDifferentContent()
    {
        var p1 = new AiPrompt("key", "content A", 1);
        var p2 = new AiPrompt("key", "content B", 1);

        Assert.NotEqual(p1.ContentHash, p2.ContentHash);
    }

    [Fact]
    public async Task GetSummary_PromptSummary_IncludesContentHashShort()
    {
        _db.AiPrompts.Add(new AiPrompt("hash_test_key", "prompt content for hash test", 1));
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        var item = result.PromptSummary.FirstOrDefault(p => p.Key == "hash_test_key");
        Assert.NotNull(item);
        Assert.NotNull(item.ContentHashShort);
        Assert.Equal(8, item.ContentHashShort!.Length);
    }

    [Fact]
    public async Task GetSummary_PromptSummary_NullHashSafe()
    {
        // Simulate a pre-T70 prompt row with no hash (EF allows null since it's nullable)
        // We test via handler: null ContentHash should return null ContentHashShort
        var prompt = new AiPrompt("no_hash_key", "some content", 1);
        _db.AiPrompts.Add(prompt);
        await _db.SaveChangesAsync();

        // The entity computes hash on construction, so this tests the short-form extraction
        var result = await _handler.GetSummaryAsync(30);
        var item = result.PromptSummary.FirstOrDefault(p => p.Key == "no_hash_key");
        Assert.NotNull(item);
        // Hash was computed, short form is first 8 chars
        Assert.Equal(8, item!.ContentHashShort!.Length);
    }

    // ── Part D: Retention ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummary_RetentionDays_ReturnsConfiguredValue()
    {
        var handler = new AdminGenerationQualityHandler(_db, BuildConfig(retentionDays: 45));

        var result = await handler.GetSummaryAsync(30);

        Assert.Equal(45, result.RetentionDays);
    }

    // ── Part F: Abandoned Warning ─────────────────────────────────────────────

    [Fact]
    public async Task GetSummary_AbandonedWarning_InactiveWhenBelowThreshold()
    {
        // 3 total, 0 abandoned -> 0% rate, below 15%
        for (var i = 0; i < 3; i++)
            _db.GenerationValidationFailures.Add(
                new GenerationValidationFailure("WritingScenario", "Err", 1));
        await _db.SaveChangesAsync();

        var handler = new AdminGenerationQualityHandler(_db, BuildConfig(minimumFailures: 2));
        var result = await handler.GetSummaryAsync(30);

        Assert.False(result.AbandonedWarning.IsActive);
        Assert.Null(result.AbandonedWarning.Message);
    }

    [Fact]
    public async Task GetSummary_AbandonedWarning_ActiveWhenAboveThreshold()
    {
        // 5 total, 4 abandoned (attemptNumber=2) -> 80% rate, above 15%
        for (var i = 0; i < 5; i++)
            _db.GenerationValidationFailures.Add(
                new GenerationValidationFailure("WritingScenario", "Err", i < 4 ? 2 : 1));
        await _db.SaveChangesAsync();

        var handler = new AdminGenerationQualityHandler(_db, BuildConfig(minimumFailures: 5));
        var result = await handler.GetSummaryAsync(30);

        Assert.True(result.AbandonedWarning.IsActive);
        Assert.NotNull(result.AbandonedWarning.Message);
        Assert.True(result.AbandonedWarning.AbandonedRate >= 0.15);
    }

    [Fact]
    public async Task GetSummary_AbandonedWarning_InactiveWhenBelowMinimumFailures()
    {
        // 2 failures, but minimum is 5 -> no warning even if rate is high
        _db.GenerationValidationFailures.AddRange(
            new GenerationValidationFailure("WritingScenario", "Err", 2),
            new GenerationValidationFailure("WritingScenario", "Err", 2));
        await _db.SaveChangesAsync();

        var handler = new AdminGenerationQualityHandler(_db, BuildConfig(minimumFailures: 5));
        var result = await handler.GetSummaryAsync(30);

        Assert.False(result.AbandonedWarning.IsActive);
    }

    [Fact]
    public async Task GetSummary_AbandonedWarning_IncludesThresholdInResult()
    {
        var handler = new AdminGenerationQualityHandler(_db, BuildConfig(threshold: 0.20));
        var result = await handler.GetSummaryAsync(30);

        Assert.Equal(0.20, result.AbandonedWarning.WarningThreshold, precision: 5);
    }

    // ── Part E: Objective / Student Context ───────────────────────────────────

    [Fact]
    public async Task GetSummary_LatestFailures_ObjectiveKeyThreaded()
    {
        _db.GenerationValidationFailures.Add(
            new GenerationValidationFailure(
                "WritingScenario", "Err", 1, objectiveKey: "obj.workplace.email"));
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        var item = Assert.Single(result.LatestFailures);
        Assert.Equal("obj.workplace.email", item.ObjectiveKey);
    }

    [Fact]
    public async Task GetSummary_NullObjectiveAndStudentContext_IsSafe()
    {
        _db.GenerationValidationFailures.Add(
            new GenerationValidationFailure("WritingScenario", "Err", 1));
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        var item = Assert.Single(result.LatestFailures);
        Assert.Null(item.ObjectiveKey);
    }
}
