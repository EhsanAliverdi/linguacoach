using LinguaCoach.Application.SkillGraph;

namespace LinguaCoach.Infrastructure.SkillGraph;

/// <summary>
/// Skill Graph rebuild Phase 6.3a — pure, deterministic redundant-edge detection (no AI, no
/// database access). An edge PrerequisiteNodeId→NodeId is "redundant" if NodeId is still
/// reachable from PrerequisiteNodeId through some OTHER path once that one direct edge is
/// excluded — i.e. the edge doesn't add any real ordering constraint the rest of the graph
/// doesn't already enforce, so it's safe to remove (this is the classic "transitive reduction"
/// check, applied per-edge via BFS rather than computed for the whole graph at once, since the
/// service also needs to support a cheap targeted check after a single new edge is added).
/// </summary>
public sealed class GraphChangeSuggestionService : IGraphChangeSuggestionService
{
    public IReadOnlyList<GraphChangeSuggestion> DetectRedundantEdges(
        IReadOnlyList<SkillGraphEdgeSummary> edges, IReadOnlyList<Guid>? restrictToNodeIds = null)
    {
        if (edges.Count == 0) return [];

        var adjacency = new Dictionary<Guid, List<Guid>>();
        foreach (var edge in edges)
        {
            if (!adjacency.TryGetValue(edge.PrerequisiteNodeId, out var list))
                adjacency[edge.PrerequisiteNodeId] = list = [];
            list.Add(edge.NodeId);
        }

        var restrictSet = restrictToNodeIds is null ? null : new HashSet<Guid>(restrictToNodeIds);
        var suggestions = new List<GraphChangeSuggestion>();

        foreach (var edge in edges)
        {
            if (restrictSet is not null && !restrictSet.Contains(edge.NodeId) && !restrictSet.Contains(edge.PrerequisiteNodeId))
                continue;

            if (IsReachableViaAlternatePath(edge, adjacency))
            {
                suggestions.Add(new GraphChangeSuggestion(
                    GraphSuggestionType.RedundantEdge,
                    "This prerequisite edge is already implied by a longer existing path — safe to remove.",
                    ProposedEdgesToAdd: [],
                    ProposedEdgesToRemove: [edge]));
            }
        }

        return suggestions;
    }

    /// <summary>BFS from the edge's prerequisite node to its dependent node, skipping the one
    /// direct edge under test — returns true if some other path still connects them.</summary>
    private static bool IsReachableViaAlternatePath(SkillGraphEdgeSummary excludedEdge, Dictionary<Guid, List<Guid>> adjacency)
    {
        var start = excludedEdge.PrerequisiteNodeId;
        var target = excludedEdge.NodeId;

        var visited = new HashSet<Guid> { start };
        var queue = new Queue<Guid>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbors)) continue;

            foreach (var next in neighbors)
            {
                if (current == start && next == target) continue; // this is the excluded direct edge itself
                if (next == target) return true; // reached via some other path
                if (visited.Add(next)) queue.Enqueue(next);
            }
        }

        return false;
    }
}
