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
/// Skill Graph rebuild Phase 6.2 — mirrors ModuleSkillGraphTaggingServiceTests' fake-provider
/// infrastructure. Never calls a real AI provider.
/// </summary>
public sealed class NodeGraphPlacementSuggestionServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly SwappableFakeAiProvider _provider = new();
    private readonly NodeGraphPlacementSuggestionService _sut;

    public NodeGraphPlacementSuggestionServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _db.AiPrompts.Add(new AiPrompt(
            NodeGraphPlacementSuggestionService.SuggestPlacementPromptKey,
            "Suggest: {{nodeTitle}} {{nodeDescription}} {{cefrLevel}} {{skill}} {{candidateNodesJson}}"));
        _db.SaveChanges();

        var aiExecution = new AiExecutionService(
            _db, new FakeAiProviderResolver(_provider), new NeverCalledUsageQuotaService(), NullLogger<AiExecutionService>.Instance);

        _sut = new NodeGraphPlacementSuggestionService(
            new DbPromptAiContextBuilder(_db), aiExecution, NullLogger<NodeGraphPlacementSuggestionService>.Instance);
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
        new SkillGraphNodeCandidate(NodeB, "grammar.past_simple.a1", "Past simple for completed actions"),
    ];

    private static NodePlacementSuggestionRequest Request(IReadOnlyList<SkillGraphNodeCandidate>? candidates = null) =>
        new(Guid.NewGuid(), "Present continuous", "A node about present continuous.", "A1", "grammar", candidates ?? Candidates);

    [Fact]
    public async Task Valid_ai_response_returns_both_directions()
    {
        _provider.NextResponses.Enqueue("""
            {"prerequisites": [ {"key": "grammar.present_simple.a1", "confidence": 0.9} ],
             "dependents": [ {"key": "grammar.past_simple.a1", "confidence": 0.6} ]}
            """);

        var result = await _sut.SuggestPlacementAsync(Request());

        Assert.True(result.Success);
        var prereq = Assert.Single(result.PrerequisiteSuggestions);
        Assert.Equal(NodeA, prereq.NodeId);
        Assert.Equal(0.9, prereq.Confidence);
        var dependent = Assert.Single(result.DependentSuggestions);
        Assert.Equal(NodeB, dependent.NodeId);
        Assert.Equal(0.6, dependent.Confidence);
    }

    [Fact]
    public async Task Unrecognized_key_is_dropped_never_trusted()
    {
        _provider.NextResponses.Enqueue("""
            {"prerequisites": [ {"key": "not_a_real_key", "confidence": 0.99} ], "dependents": []}
            """);

        var result = await _sut.SuggestPlacementAsync(Request());

        Assert.True(result.Success);
        Assert.Empty(result.PrerequisiteSuggestions);
    }

    [Fact]
    public async Task Missing_confidence_defaults_to_0_7()
    {
        _provider.NextResponses.Enqueue("""
            {"prerequisites": [ {"key": "grammar.present_simple.a1"} ], "dependents": []}
            """);

        var result = await _sut.SuggestPlacementAsync(Request());

        Assert.Equal(0.7, result.PrerequisiteSuggestions.Single().Confidence);
    }

    [Fact]
    public async Task Duplicate_matched_keys_are_deduplicated()
    {
        _provider.NextResponses.Enqueue("""
            {"prerequisites": [
              {"key": "grammar.present_simple.a1", "confidence": 0.9},
              {"key": "grammar.present_simple.a1", "confidence": 0.5}
            ], "dependents": []}
            """);

        var result = await _sut.SuggestPlacementAsync(Request());

        Assert.Single(result.PrerequisiteSuggestions);
    }

    [Fact]
    public async Task A_node_can_appear_in_only_one_direction_per_response_as_given()
    {
        // The service trusts whichever list the AI put a key in — it doesn't need to defend
        // against a key appearing in both, since that's a prompt-instruction concern, not a
        // structural-safety one (either way, the id is real and validated against the candidates).
        _provider.NextResponses.Enqueue("""
            {"prerequisites": [ {"key": "grammar.present_simple.a1"} ],
             "dependents": [ {"key": "grammar.present_simple.a1"} ]}
            """);

        var result = await _sut.SuggestPlacementAsync(Request());

        Assert.Single(result.PrerequisiteSuggestions);
        Assert.Single(result.DependentSuggestions);
    }

    [Fact]
    public async Task Empty_arrays_is_a_valid_success_result()
    {
        _provider.NextResponses.Enqueue("""{"prerequisites": [], "dependents": []}""");

        var result = await _sut.SuggestPlacementAsync(Request());

        Assert.True(result.Success);
        Assert.Empty(result.PrerequisiteSuggestions);
        Assert.Empty(result.DependentSuggestions);
    }

    [Fact]
    public async Task No_candidate_nodes_returns_success_without_calling_ai()
    {
        var result = await _sut.SuggestPlacementAsync(Request([]));

        Assert.True(result.Success);
        Assert.Empty(result.PrerequisiteSuggestions);
        Assert.Empty(result.DependentSuggestions);
        Assert.Equal(0, _provider.CallCount);
    }

    [Fact]
    public async Task Invalid_json_response_is_retried_once_then_succeeds()
    {
        _provider.NextResponses.Enqueue("not json");
        _provider.NextResponses.Enqueue("""{"prerequisites": [ {"key": "grammar.present_simple.a1"} ], "dependents": []}""");

        var result = await _sut.SuggestPlacementAsync(Request());

        Assert.True(result.Success);
        Assert.Equal(2, _provider.CallCount);
    }

    [Fact]
    public async Task Ai_provider_unavailable_fails_gracefully_without_throwing()
    {
        _provider.ThrowUnavailable = true;

        var result = await _sut.SuggestPlacementAsync(Request());

        Assert.False(result.Success);
        Assert.Contains("unavailable", result.ErrorMessage);
        Assert.Empty(result.PrerequisiteSuggestions);
        Assert.Empty(result.DependentSuggestions);
    }
}
