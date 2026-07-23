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

    // ── Phase 6.3b — reject-triggered reconnect suggestions ──────────────────────────────────

    [Fact]
    public void Rejecting_the_middle_node_of_a_chain_suggests_reconnecting_predecessor_to_dependent()
    {
        // A -> B -> C, B is rejected — suggest A -> C.
        SkillGraphEdgeSummary[] edgesBeforeRemoval =
        [
            new(NodeId: B, PrerequisiteNodeId: A),
            new(NodeId: C, PrerequisiteNodeId: B),
        ];

        var groups = _sut.DetectReconnectsAfterReject([B], edgesBeforeRemoval);

        var group = Assert.Single(groups);
        Assert.Equal(B, group.RejectedNodeId);
        Assert.Equal([A], group.OrphanedPredecessorIds);
        Assert.Equal([C], group.OrphanedDependentIds);
        var reconnect = Assert.Single(group.SuggestedReconnects);
        Assert.Equal(A, reconnect.PrerequisiteNodeId);
        Assert.Equal(C, reconnect.NodeId);
    }

    [Fact]
    public void Reconnect_is_not_suggested_when_the_direct_edge_already_exists()
    {
        // A -> B -> C, plus A -> C already exists directly — nothing new to suggest.
        SkillGraphEdgeSummary[] edgesBeforeRemoval =
        [
            new(NodeId: B, PrerequisiteNodeId: A),
            new(NodeId: C, PrerequisiteNodeId: B),
            new(NodeId: C, PrerequisiteNodeId: A),
        ];

        var groups = _sut.DetectReconnectsAfterReject([B], edgesBeforeRemoval);

        Assert.Empty(groups);
    }

    [Fact]
    public void Rejected_node_with_no_predecessors_or_no_dependents_has_nothing_to_reconnect()
    {
        // B has only a dependent (C), no predecessor — a root node, nothing to bridge.
        SkillGraphEdgeSummary[] edgesBeforeRemoval =
        [
            new(NodeId: C, PrerequisiteNodeId: B),
        ];

        var groups = _sut.DetectReconnectsAfterReject([B], edgesBeforeRemoval);

        Assert.Empty(groups);
    }

    [Fact]
    public void Multiple_predecessors_and_dependents_produce_the_full_cross_product()
    {
        // A1, A2 -> B -> C1, C2 — 4 candidate reconnects.
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();
        SkillGraphEdgeSummary[] edgesBeforeRemoval =
        [
            new(NodeId: B, PrerequisiteNodeId: a1),
            new(NodeId: B, PrerequisiteNodeId: a2),
            new(NodeId: c1, PrerequisiteNodeId: B),
            new(NodeId: c2, PrerequisiteNodeId: B),
        ];

        var groups = _sut.DetectReconnectsAfterReject([B], edgesBeforeRemoval);

        var group = Assert.Single(groups);
        Assert.Equal(4, group.SuggestedReconnects.Count);
    }

    [Fact]
    public void A_predecessor_or_dependent_that_is_also_rejected_in_the_same_batch_is_excluded()
    {
        // A -> B -> C, and A is ALSO being rejected in this same batch — no reconnect makes sense
        // through a node that's disappearing too.
        SkillGraphEdgeSummary[] edgesBeforeRemoval =
        [
            new(NodeId: B, PrerequisiteNodeId: A),
            new(NodeId: C, PrerequisiteNodeId: B),
        ];

        var groups = _sut.DetectReconnectsAfterReject([A, B], edgesBeforeRemoval);

        Assert.Empty(groups);
    }

    [Fact]
    public void Batch_rejecting_multiple_unrelated_nodes_returns_one_group_per_node()
    {
        var d = Guid.NewGuid();
        var e = Guid.NewGuid();
        var f = Guid.NewGuid();
        SkillGraphEdgeSummary[] edgesBeforeRemoval =
        [
            new(NodeId: B, PrerequisiteNodeId: A),
            new(NodeId: C, PrerequisiteNodeId: B),
            new(NodeId: e, PrerequisiteNodeId: d),
            new(NodeId: f, PrerequisiteNodeId: e),
        ];

        var groups = _sut.DetectReconnectsAfterReject([B, e], edgesBeforeRemoval);

        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.RejectedNodeId == B);
        Assert.Contains(groups, g => g.RejectedNodeId == e);
    }

    [Fact]
    public void No_rejected_ids_or_no_edges_returns_no_groups()
    {
        Assert.Empty(_sut.DetectReconnectsAfterReject([], [new(NodeId: B, PrerequisiteNodeId: A)]));
        Assert.Empty(_sut.DetectReconnectsAfterReject([B], []));
    }
}
