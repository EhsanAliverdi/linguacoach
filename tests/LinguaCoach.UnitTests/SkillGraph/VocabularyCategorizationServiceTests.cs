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
/// Full content reseed (2026-07-28) — mirrors SkillGraphDraftingServiceTests' fake-provider
/// infrastructure exactly; never calls a real AI provider.
/// </summary>
public sealed class VocabularyCategorizationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly SwappableFakeAiProvider _provider = new();
    private readonly VocabularyCategorizationService _sut;

    public VocabularyCategorizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _db.AiPrompts.Add(new AiPrompt(
            VocabularyCategorizationService.CategorizeWordsPromptKey,
            "Categorize: {{existingCategories}} {{words}} {{contextTags}}"));
        _db.SaveChanges();

        var aiExecution = new AiExecutionService(
            _db, new FakeAiProviderResolver(_provider), new NeverCalledUsageQuotaService(), NullLogger<AiExecutionService>.Instance);

        _sut = new VocabularyCategorizationService(
            new DbPromptAiContextBuilder(_db), aiExecution, NullLogger<VocabularyCategorizationService>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private static VocabularyWordRow Word(string headword, string pos = "noun", string cefr = "B1") =>
        new(headword, pos, cefr, Category: null);

    [Fact]
    public async Task Empty_batch_returns_success_without_calling_ai()
    {
        var result = await _sut.CategorizeBatchAsync(new VocabularyCategorizationRequest([], []));

        Assert.True(result.Success);
        Assert.Empty(result.Words);
    }

    [Fact]
    public async Task Valid_ai_response_returns_categorizations()
    {
        _provider.NextResponses.Enqueue("""
            {"words": [
              {"headword": "abandon", "pos": "verb", "category": "Actions", "description": "To leave something.", "contextTags": ["day_to_day"], "focusTags": []}
            ]}
            """);

        var result = await _sut.CategorizeBatchAsync(new VocabularyCategorizationRequest([Word("abandon", "verb")], []));

        Assert.True(result.Success);
        var word = Assert.Single(result.Words);
        Assert.Equal("abandon", word.Headword);
        Assert.Equal("Actions", word.Category);
        Assert.Equal(["day_to_day"], word.ContextTags);
    }

    [Fact]
    public async Task Response_for_a_word_not_in_the_request_is_dropped()
    {
        _provider.NextResponses.Enqueue("""
            {"words": [
              {"headword": "hallucinated", "pos": "noun", "category": "X", "description": "Not asked about.", "contextTags": [], "focusTags": []}
            ]}
            """);

        var result = await _sut.CategorizeBatchAsync(new VocabularyCategorizationRequest([Word("abandon")], []));

        Assert.True(result.Success);
        Assert.Empty(result.Words);
    }

    [Fact]
    public async Task Unrecognized_context_tag_is_dropped_but_word_is_kept()
    {
        _provider.NextResponses.Enqueue("""
            {"words": [
              {"headword": "abandon", "pos": "noun", "category": "Actions", "description": "D.", "contextTags": ["not_a_real_tag"], "focusTags": []}
            ]}
            """);

        var result = await _sut.CategorizeBatchAsync(new VocabularyCategorizationRequest([Word("abandon", "noun")], []));

        var word = Assert.Single(result.Words);
        Assert.Empty(word.ContextTags);
    }

    [Fact]
    public async Task Category_is_trusted_as_free_text_even_when_new()
    {
        _provider.NextResponses.Enqueue("""
            {"words": [
              {"headword": "abandon", "pos": "verb", "category": "A Brand New Topic", "description": "D.", "contextTags": [], "focusTags": []}
            ]}
            """);

        var result = await _sut.CategorizeBatchAsync(new VocabularyCategorizationRequest([Word("abandon", "verb")], ["Existing Topic"]));

        var word = Assert.Single(result.Words);
        Assert.Equal("A Brand New Topic", word.Category);
    }

    [Fact]
    public async Task Duplicate_entries_for_the_same_word_keep_only_the_first()
    {
        _provider.NextResponses.Enqueue("""
            {"words": [
              {"headword": "abandon", "pos": "verb", "category": "Actions", "description": "First.", "contextTags": [], "focusTags": []},
              {"headword": "abandon", "pos": "verb", "category": "Other", "description": "Second.", "contextTags": [], "focusTags": []}
            ]}
            """);

        var result = await _sut.CategorizeBatchAsync(new VocabularyCategorizationRequest([Word("abandon", "verb")], []));

        var word = Assert.Single(result.Words);
        Assert.Equal("Actions", word.Category);
    }

    [Fact]
    public async Task Invalid_json_is_retried_once_then_fails()
    {
        _provider.NextResponses.Enqueue("not json");
        _provider.NextResponses.Enqueue("still not json");

        var result = await _sut.CategorizeBatchAsync(new VocabularyCategorizationRequest([Word("abandon")], []));

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Invalid_json_first_then_valid_on_retry_succeeds()
    {
        _provider.NextResponses.Enqueue("not json");
        _provider.NextResponses.Enqueue("""
            {"words": [
              {"headword": "abandon", "pos": "verb", "category": "Actions", "description": "D.", "contextTags": [], "focusTags": []}
            ]}
            """);

        var result = await _sut.CategorizeBatchAsync(new VocabularyCategorizationRequest([Word("abandon", "verb")], []));

        Assert.True(result.Success);
        Assert.Single(result.Words);
    }
}
