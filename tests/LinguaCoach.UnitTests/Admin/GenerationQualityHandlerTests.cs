using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Admin;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

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
        _handler = new AdminGenerationQualityHandler(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

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
        // Add an inactive prompt — should not appear
        var inactive = new global::LinguaCoach.Domain.Entities.AiPrompt(
            "test_inactive_prompt_key", "content", 1);
        inactive.Deactivate();
        _db.AiPrompts.Add(inactive);
        await _db.SaveChangesAsync();

        var result = await _handler.GetSummaryAsync(30);

        Assert.DoesNotContain(result.PromptSummary, p => p.Key == "test_inactive_prompt_key");
    }
}
