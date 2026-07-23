using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Infrastructure.SkillGraph;

namespace LinguaCoach.UnitTests.SkillGraph;

/// <summary>
/// Skill Graph rebuild Phase 6.3a — GraphChangeSuggestionService.DetectRedundantEdges is pure and
/// deterministic (no AI, no database), so every scenario from the approved plan is exhaustively
/// testable in isolation: (1) insert-node-between leaves a redundant direct edge, (3) a new edge
/// closing an already-covered path, (4) the on-demand whole-graph audit shape.
/// </summary>
public sealed class GraphChangeSuggestionServiceTests
{
    private readonly GraphChangeSuggestionService _sut = new();

    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid X = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void Direct_edge_spanned_by_a_longer_path_is_flagged_redundant()
    {
        // A→X, X→B, and a direct A→B that's now implied by the A→X→B path — scenario 1/3.
        SkillGraphEdgeSummary[] edges =
        [
            new(NodeId: X, PrerequisiteNodeId: A),
            new(NodeId: B, PrerequisiteNodeId: X),
            new(NodeId: B, PrerequisiteNodeId: A),
        ];

        var suggestions = _sut.DetectRedundantEdges(edges);

        var suggestion = Assert.Single(suggestions);
        Assert.Equal(GraphSuggestionType.RedundantEdge, suggestion.Type);
        Assert.Empty(suggestion.ProposedEdgesToAdd);
        var removed = Assert.Single(suggestion.ProposedEdgesToRemove);
        Assert.Equal(A, removed.PrerequisiteNodeId);
        Assert.Equal(B, removed.NodeId);
    }

    [Fact]
    public void Direct_edge_with_no_alternate_path_is_not_flagged()
    {
        SkillGraphEdgeSummary[] edges =
        [
            new(NodeId: B, PrerequisiteNodeId: A),
        ];

        var suggestions = _sut.DetectRedundantEdges(edges);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void Chain_without_a_direct_shortcut_has_nothing_redundant()
    {
        // A→X→B with no direct A→B edge at all — nothing to flag.
        SkillGraphEdgeSummary[] edges =
        [
            new(NodeId: X, PrerequisiteNodeId: A),
            new(NodeId: B, PrerequisiteNodeId: X),
        ];

        var suggestions = _sut.DetectRedundantEdges(edges);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void Longer_three_hop_path_also_flags_the_direct_edge()
    {
        // A→X→C→B, plus a direct A→B — still redundant even though the alternate path is 3 hops.
        SkillGraphEdgeSummary[] edges =
        [
            new(NodeId: X, PrerequisiteNodeId: A),
            new(NodeId: C, PrerequisiteNodeId: X),
            new(NodeId: B, PrerequisiteNodeId: C),
            new(NodeId: B, PrerequisiteNodeId: A),
        ];

        var suggestions = _sut.DetectRedundantEdges(edges);

        Assert.Single(suggestions);
    }

    [Fact]
    public void RestrictToNodeIds_only_checks_edges_touching_those_nodes()
    {
        // Two independent redundant triangles; restricting to one pair's node ids should only
        // surface that pair's suggestion — the cheap post-mutation check, not a full audit.
        var p = Guid.NewGuid();
        var q = Guid.NewGuid();
        var r = Guid.NewGuid();

        SkillGraphEdgeSummary[] edges =
        [
            new(NodeId: X, PrerequisiteNodeId: A),
            new(NodeId: B, PrerequisiteNodeId: X),
            new(NodeId: B, PrerequisiteNodeId: A), // redundant #1: A->B
            new(NodeId: q, PrerequisiteNodeId: p),
            new(NodeId: r, PrerequisiteNodeId: q),
            new(NodeId: r, PrerequisiteNodeId: p), // redundant #2: p->r
        ];

        var suggestions = _sut.DetectRedundantEdges(edges, restrictToNodeIds: [A, B]);

        var suggestion = Assert.Single(suggestions);
        var removed = Assert.Single(suggestion.ProposedEdgesToRemove);
        Assert.Equal(A, removed.PrerequisiteNodeId);
        Assert.Equal(B, removed.NodeId);
    }

    [Fact]
    public void Empty_edge_list_returns_no_suggestions()
    {
        var suggestions = _sut.DetectRedundantEdges([]);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void Disconnected_graph_regions_do_not_cross_contaminate()
    {
        var p = Guid.NewGuid();
        var q = Guid.NewGuid();

        SkillGraphEdgeSummary[] edges =
        [
            new(NodeId: B, PrerequisiteNodeId: A), // isolated pair, not redundant
            new(NodeId: q, PrerequisiteNodeId: p), // another isolated pair, not redundant
        ];

        var suggestions = _sut.DetectRedundantEdges(edges);

        Assert.Empty(suggestions);
    }
}
