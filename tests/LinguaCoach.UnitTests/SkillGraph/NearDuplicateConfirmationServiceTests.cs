using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.SkillGraph;
using LinguaCoach.Persistence;
using LinguaCoach.UnitTests.ResourceImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.SkillGraph;

/// <summary>
/// Skill Graph rebuild Phase 6.3f — mirrors NodeGraphPlacementSuggestionServiceTests' fake-provider
/// infrastructure. Never calls a real AI provider.
/// </summary>
public sealed class NearDuplicateConfirmationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly SwappableFakeAiProvider _provider = new();
    private readonly NearDuplicateConfirmationService _sut;

    public NearDuplicateConfirmationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _db.AiPrompts.Add(new AiPrompt(
            NearDuplicateConfirmationService.ConfirmNearDuplicatePromptKey,
            "Confirm: {{nodeATitle}} {{nodeADescription}} {{nodeBTitle}} {{nodeBDescription}}"));
        _db.SaveChanges();

        var aiExecution = new AiExecutionService(
            _db, new FakeAiProviderResolver(_provider), new NeverCalledUsageQuotaService(), NullLogger<AiExecutionService>.Instance);

        _sut = new NearDuplicateConfirmationService(
            new DbPromptAiContextBuilder(_db), aiExecution, NullLogger<NearDuplicateConfirmationService>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private static NearDuplicateConfirmationRequest Request() => new(
        "Present simple affirmative", "Practice forming affirmative sentences in the present simple tense.",
        "Present simple affirmatve", "Practice forming affirmative sentences in the present simple tense.");

    [Fact]
    public async Task Valid_ai_response_confirming_a_duplicate_is_returned()
    {
        _provider.NextResponses.Enqueue("""{"isDuplicate": true, "reasoning": "Same content, one is a typo of the other."}""");

        var result = await _sut.ConfirmAsync(Request());

        Assert.True(result.Success);
        Assert.True(result.IsLikelyDuplicate);
        Assert.Equal("Same content, one is a typo of the other.", result.Reasoning);
    }

    [Fact]
    public async Task Valid_ai_response_rejecting_a_duplicate_is_returned()
    {
        _provider.NextResponses.Enqueue("""{"isDuplicate": false, "reasoning": "Different reading texts, not duplicates."}""");

        var result = await _sut.ConfirmAsync(Request());

        Assert.True(result.Success);
        Assert.False(result.IsLikelyDuplicate);
    }

    [Fact]
    public async Task Missing_reasoning_defaults_to_empty_string()
    {
        _provider.NextResponses.Enqueue("""{"isDuplicate": true}""");

        var result = await _sut.ConfirmAsync(Request());

        Assert.True(result.Success);
        Assert.Equal("", result.Reasoning);
    }

    [Fact]
    public async Task Missing_isDuplicate_property_is_treated_as_unparsable_and_retried()
    {
        _provider.NextResponses.Enqueue("""{"reasoning": "no verdict field"}""");
        _provider.NextResponses.Enqueue("""{"isDuplicate": false, "reasoning": "ok on retry"}""");

        var result = await _sut.ConfirmAsync(Request());

        Assert.True(result.Success);
        Assert.Equal(2, _provider.CallCount);
    }

    [Fact]
    public async Task Invalid_json_response_is_retried_once_then_succeeds()
    {
        _provider.NextResponses.Enqueue("not json");
        _provider.NextResponses.Enqueue("""{"isDuplicate": true, "reasoning": "ok"}""");

        var result = await _sut.ConfirmAsync(Request());

        Assert.True(result.Success);
        Assert.Equal(2, _provider.CallCount);
    }

    [Fact]
    public async Task Invalid_json_on_both_attempts_fails_gracefully_without_throwing()
    {
        _provider.NextResponses.Enqueue("not json");
        _provider.NextResponses.Enqueue("still not json");

        var result = await _sut.ConfirmAsync(Request());

        Assert.False(result.Success);
        Assert.Null(result.IsLikelyDuplicate);
        Assert.Contains("could not be parsed", result.ErrorMessage);
    }

    [Fact]
    public async Task Ai_provider_unavailable_fails_gracefully_without_throwing()
    {
        _provider.ThrowUnavailable = true;

        var result = await _sut.ConfirmAsync(Request());

        Assert.False(result.Success);
        Assert.Contains("unavailable", result.ErrorMessage);
        Assert.Null(result.IsLikelyDuplicate);
    }
}
