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
/// Adaptive Curriculum Sprint 2 — mirrors SkillGraphDraftingServiceTests' fake-provider
/// infrastructure. Never calls a real AI provider.
/// </summary>
public sealed class ModuleSkillGraphTaggingServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly SwappableFakeAiProvider _provider = new();
    private readonly ModuleSkillGraphTaggingService _sut;

    public ModuleSkillGraphTaggingServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _db.AiPrompts.Add(new AiPrompt(
            ModuleSkillGraphTaggingService.ProposeCoveragePromptKey,
            "Match: {{moduleTitle}} {{moduleDescription}} {{cefrLevel}} {{skill}} {{candidateNodesJson}}"));
        _db.SaveChanges();

        var aiExecution = new AiExecutionService(
            _db, new FakeAiProviderResolver(_provider), new NeverCalledUsageQuotaService(), NullLogger<AiExecutionService>.Instance);

        _sut = new ModuleSkillGraphTaggingService(
            new DbPromptAiContextBuilder(_db), aiExecution, NullLogger<ModuleSkillGraphTaggingService>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private static readonly Guid NodeA = Guid.NewGuid();
    private static readonly Guid NodeB = Guid.NewGuid();

    private static readonly IReadOnlyList<SkillGraphNodeCandidate> Candidates =
    [
        new SkillGraphNodeCandidate(NodeA, "grammar.present_simple.a1", "Present simple for daily routines"),
        new SkillGraphNodeCandidate(NodeB, "grammar.prepositions.a1", "Basic prepositions of place"),
    ];

    private static ModuleSkillGraphTaggingRequest Request(IReadOnlyList<SkillGraphNodeCandidate>? candidates = null) =>
        new(Guid.NewGuid(), "Daily Routines", "A module about daily routines.", "A1", "grammar", candidates ?? Candidates);

    [Fact]
    public async Task Valid_ai_response_returns_matched_nodes()
    {
        _provider.NextResponses.Enqueue($$"""
            {"matches": [ {"key": "grammar.present_simple.a1", "confidence": 0.9} ]}
            """);

        var result = await _sut.ProposeCoverageAsync(Request());

        Assert.True(result.Success);
        var match = Assert.Single(result.Matches);
        Assert.Equal(NodeA, match.NodeId);
        Assert.Equal(0.9, match.Confidence);
    }

    [Fact]
    public async Task Unrecognized_key_is_dropped_never_trusted()
    {
        _provider.NextResponses.Enqueue("""
            {"matches": [ {"key": "not_a_real_key", "confidence": 0.99} ] }
            """);

        var result = await _sut.ProposeCoverageAsync(Request());

        Assert.True(result.Success);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task Missing_confidence_defaults_to_0_7()
    {
        _provider.NextResponses.Enqueue("""
            {"matches": [ {"key": "grammar.present_simple.a1"} ] }
            """);

        var result = await _sut.ProposeCoverageAsync(Request());

        Assert.True(result.Success);
        Assert.Equal(0.7, result.Matches.Single().Confidence);
    }

    [Fact]
    public async Task Duplicate_matched_keys_are_deduplicated()
    {
        _provider.NextResponses.Enqueue("""
            {"matches": [
              {"key": "grammar.present_simple.a1", "confidence": 0.9},
              {"key": "grammar.present_simple.a1", "confidence": 0.5}
            ]}
            """);

        var result = await _sut.ProposeCoverageAsync(Request());

        Assert.True(result.Success);
        Assert.Single(result.Matches);
    }

    [Fact]
    public async Task Empty_matches_is_a_valid_success_result()
    {
        _provider.NextResponses.Enqueue("""{"matches": []}""");

        var result = await _sut.ProposeCoverageAsync(Request());

        Assert.True(result.Success);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task No_candidate_nodes_returns_success_without_calling_ai()
    {
        var result = await _sut.ProposeCoverageAsync(Request([]));

        Assert.True(result.Success);
        Assert.Empty(result.Matches);
        Assert.Equal(0, _provider.CallCount);
    }

    [Fact]
    public async Task Invalid_json_response_is_retried_once_then_succeeds()
    {
        _provider.NextResponses.Enqueue("not json");
        _provider.NextResponses.Enqueue("""{"matches": [ {"key": "grammar.present_simple.a1"} ] }""");

        var result = await _sut.ProposeCoverageAsync(Request());

        Assert.True(result.Success);
        Assert.Equal(2, _provider.CallCount);
    }

    [Fact]
    public async Task Ai_provider_unavailable_fails_gracefully_without_throwing()
    {
        _provider.ThrowUnavailable = true;

        var result = await _sut.ProposeCoverageAsync(Request());

        Assert.False(result.Success);
        Assert.Contains("unavailable", result.ErrorMessage);
        Assert.Empty(result.Matches);
    }
}
