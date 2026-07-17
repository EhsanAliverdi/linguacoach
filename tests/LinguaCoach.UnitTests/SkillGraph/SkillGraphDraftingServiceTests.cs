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
/// Adaptive Curriculum Sprint 1 — mirrors ResourceImportColumnMappingServiceTests' fake-provider
/// infrastructure (SwappableFakeAiProvider/FakeAiProviderResolver/NeverCalledUsageQuotaService) —
/// never calls a real AI provider.
/// </summary>
public sealed class SkillGraphDraftingServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly SwappableFakeAiProvider _provider = new();
    private readonly SkillGraphDraftingService _sut;

    public SkillGraphDraftingServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _db.AiPrompts.Add(new AiPrompt(
            SkillGraphDraftingService.ProposeNodesPromptKey,
            "Draft: {{cefrLevel}} {{skill}} {{subskills}} {{existingTitles}}"));
        _db.SaveChanges();

        var aiExecution = new AiExecutionService(
            _db, new FakeAiProviderResolver(_provider), new NeverCalledUsageQuotaService(), NullLogger<AiExecutionService>.Instance);

        _sut = new SkillGraphDraftingService(
            new DbPromptAiContextBuilder(_db), aiExecution, NullLogger<SkillGraphDraftingService>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task Valid_ai_response_returns_proposed_nodes()
    {
        _provider.NextResponses.Enqueue("""
            {"nodes": [
              {"title": "Present simple for daily routines", "description": "Habitual actions.", "subskill": "grammar.tense_aspect", "difficultyBand": 1, "prerequisiteTitles": []}
            ]}
            """);

        var result = await _sut.ProposeBatchAsync(new SkillGraphDraftRequest("A1", "grammar", []));

        Assert.True(result.Success);
        var node = Assert.Single(result.Nodes);
        Assert.Equal("Present simple for daily routines", node.Title);
        Assert.Equal("A1", node.CefrLevel);
        Assert.Equal("grammar", node.Skill);
        Assert.Equal("grammar.tense_aspect", node.Subskill);
    }

    [Fact]
    public async Task Unrecognized_subskill_is_dropped_never_trusted()
    {
        _provider.NextResponses.Enqueue("""
            {"nodes": [
              {"title": "T", "description": "D", "subskill": "not_a_real_subskill", "difficultyBand": 2}
            ]}
            """);

        var result = await _sut.ProposeBatchAsync(new SkillGraphDraftRequest("A1", "grammar", []));

        Assert.True(result.Success);
        Assert.Null(result.Nodes.Single().Subskill);
    }

    [Fact]
    public async Task Subskill_belonging_to_a_different_skill_is_dropped()
    {
        _provider.NextResponses.Enqueue("""
            {"nodes": [
              {"title": "T", "description": "D", "subskill": "reading.gist", "difficultyBand": 1}
            ]}
            """);

        // Requested skill is "grammar" — "reading.gist" belongs to "reading", not a valid subskill here.
        var result = await _sut.ProposeBatchAsync(new SkillGraphDraftRequest("A1", "grammar", []));

        Assert.True(result.Success);
        Assert.Null(result.Nodes.Single().Subskill);
    }

    [Fact]
    public async Task DifficultyBand_out_of_range_is_clamped()
    {
        _provider.NextResponses.Enqueue("""
            {"nodes": [ {"title": "T", "description": "D", "difficultyBand": 99} ]}
            """);

        var result = await _sut.ProposeBatchAsync(new SkillGraphDraftRequest("A1", "grammar", []));

        Assert.True(result.Success);
        Assert.Equal(5, result.Nodes.Single().DifficultyBand);
    }

    [Fact]
    public async Task Duplicate_titles_within_batch_are_deduplicated()
    {
        _provider.NextResponses.Enqueue("""
            {"nodes": [
              {"title": "Same Title", "description": "D1"},
              {"title": "Same Title", "description": "D2"}
            ]}
            """);

        var result = await _sut.ProposeBatchAsync(new SkillGraphDraftRequest("A1", "grammar", []));

        Assert.True(result.Success);
        Assert.Single(result.Nodes);
    }

    [Fact]
    public async Task Invalid_json_response_is_retried_once_then_succeeds()
    {
        _provider.NextResponses.Enqueue("not json");
        _provider.NextResponses.Enqueue("""{"nodes": [ {"title": "T", "description": "D"} ]}""");

        var result = await _sut.ProposeBatchAsync(new SkillGraphDraftRequest("A1", "grammar", []));

        Assert.True(result.Success);
        Assert.Equal(2, _provider.CallCount);
    }

    [Fact]
    public async Task Ai_provider_unavailable_fails_gracefully_without_throwing()
    {
        _provider.ThrowUnavailable = true;

        var result = await _sut.ProposeBatchAsync(new SkillGraphDraftRequest("A1", "grammar", []));

        Assert.False(result.Success);
        Assert.Contains("unavailable", result.ErrorMessage);
        Assert.Empty(result.Nodes);
    }

    [Fact]
    public async Task Invalid_cefr_level_fails_without_calling_ai()
    {
        var result = await _sut.ProposeBatchAsync(new SkillGraphDraftRequest("Z9", "grammar", []));

        Assert.False(result.Success);
        Assert.Equal(0, _provider.CallCount);
    }

    [Fact]
    public async Task Invalid_skill_fails_without_calling_ai()
    {
        var result = await _sut.ProposeBatchAsync(new SkillGraphDraftRequest("A1", "not_a_skill", []));

        Assert.False(result.Success);
        Assert.Equal(0, _provider.CallCount);
    }
}
