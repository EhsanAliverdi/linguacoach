using LinguaCoach.Application.SkillGraph;

namespace LinguaCoach.Infrastructure.SkillGraph;

/// <summary>
/// Skill Graph rebuild Phase 6.3 — pure, deterministic ("no AI, no database access") graph
/// change suggestions.
///
/// Phase 6.3a — redundant-edge detection: an edge PrerequisiteNodeId→NodeId is "redundant" if
/// NodeId is still reachable from PrerequisiteNodeId through some OTHER path once that one direct
/// edge is excluded — i.e. the edge doesn't add any real ordering constraint the rest of the graph
/// doesn't already enforce, so it's safe to remove (the classic "transitive reduction" check,
/// applied per-edge via BFS rather than computed for the whole graph at once, since the service
/// also needs to support a cheap targeted check after a single new edge is added).
///
/// Phase 6.3b — reject-triggered reconnect suggestions: when a node is rejected (and
/// AdminSkillGraphController.BatchReject cascade-deletes every edge touching it), its former
/// predecessors and former dependents lose their only connection through it — suggest bridging
/// them directly instead, per the approved plan's A→B→C / B-rejected / suggest-A→C scenario.
///
/// Phase 6.3c — near-duplicate node detection: flags node pairs (same CEFR level + Skill) whose
/// titles are highly similar (Jaro-Winkler ≥ <see cref="NearDuplicateSimilarityThreshold"/>), a
/// likely sign of accidental duplicate content — advisory only, merging is a separate explicit
/// admin action.
/// </summary>
public sealed class GraphChangeSuggestionService : IGraphChangeSuggestionService
{
    /// <summary>Fixed per the approved plan — not admin-tunable. Revisit only if real usage shows
    /// this value misfires (too many false positives/negatives).</summary>
    public const double NearDuplicateSimilarityThreshold = 0.85;

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

    public IReadOnlyList<RejectReconnectGroup> DetectReconnectsAfterReject(
        IReadOnlyList<Guid> rejectedNodeIds, IReadOnlyList<SkillGraphEdgeSummary> edgesBeforeRemoval)
    {
        if (rejectedNodeIds.Count == 0 || edgesBeforeRemoval.Count == 0) return [];

        var rejectedSet = new HashSet<Guid>(rejectedNodeIds);
        var existingEdgeKeys = new HashSet<(Guid Prerequisite, Guid Node)>(
            edgesBeforeRemoval.Select(e => (e.PrerequisiteNodeId, e.NodeId)));

        var groups = new List<RejectReconnectGroup>();
        foreach (var rejectedId in rejectedNodeIds)
        {
            // "A" nodes: real predecessors of the rejected node (excluding any that are
            // themselves also being rejected in this same batch — nothing to reconnect through).
            var predecessors = edgesBeforeRemoval
                .Where(e => e.NodeId == rejectedId && !rejectedSet.Contains(e.PrerequisiteNodeId))
                .Select(e => e.PrerequisiteNodeId).Distinct().ToList();
            // "C" nodes: real dependents of the rejected node.
            var dependents = edgesBeforeRemoval
                .Where(e => e.PrerequisiteNodeId == rejectedId && !rejectedSet.Contains(e.NodeId))
                .Select(e => e.NodeId).Distinct().ToList();

            if (predecessors.Count == 0 || dependents.Count == 0) continue; // nothing to bridge

            var reconnects = new List<SkillGraphEdgeSummary>();
            foreach (var predecessor in predecessors)
            {
                foreach (var dependent in dependents)
                {
                    if (predecessor == dependent) continue; // guards against a self-edge suggestion
                    if (existingEdgeKeys.Contains((predecessor, dependent))) continue; // already directly connected
                    reconnects.Add(new SkillGraphEdgeSummary(NodeId: dependent, PrerequisiteNodeId: predecessor));
                }
            }

            if (reconnects.Count == 0) continue; // every predecessor/dependent pair is already connected

            groups.Add(new RejectReconnectGroup(rejectedId, predecessors, dependents, reconnects));
        }

        return groups;
    }

    public IReadOnlyList<NearDuplicateNodeSuggestion> DetectNearDuplicateNodes(
        IReadOnlyList<NearDuplicateNodeCandidate> nodes)
    {
        if (nodes.Count < 2) return [];

        var suggestions = new List<NearDuplicateNodeSuggestion>();

        // Group by (CefrLevel, Skill) first so we never pay O(n^2) across the whole graph —
        // only within groups that could plausibly be duplicates of each other.
        var groups = nodes.GroupBy(n => (n.CefrLevel, n.Skill));
        foreach (var group in groups)
        {
            var members = group.ToList();
            for (var i = 0; i < members.Count; i++)
            {
                for (var j = i + 1; j < members.Count; j++)
                {
                    var similarity = JaroWinklerSimilarity(members[i].Title, members[j].Title);
                    if (similarity < NearDuplicateSimilarityThreshold) continue;

                    suggestions.Add(new NearDuplicateNodeSuggestion(
                        NodeAId: members[i].Id,
                        NodeBId: members[j].Id,
                        CefrLevel: group.Key.CefrLevel,
                        Skill: group.Key.Skill,
                        Similarity: similarity));
                }
            }
        }

        return suggestions;
    }

    /// <summary>Standard Jaro-Winkler string similarity, case-insensitive. Returns 1.0 for
    /// identical strings, 0.0 for completely dissimilar/empty strings.</summary>
    private static double JaroWinklerSimilarity(string s1, string s2)
    {
        s1 = s1.Trim().ToLowerInvariant();
        s2 = s2.Trim().ToLowerInvariant();
        if (s1 == s2) return 1.0;

        var len1 = s1.Length;
        var len2 = s2.Length;
        if (len1 == 0 || len2 == 0) return 0.0;

        var matchDistance = Math.Max(0, Math.Max(len1, len2) / 2 - 1);

        var s1Matches = new bool[len1];
        var s2Matches = new bool[len2];
        var matches = 0;

        for (var i = 0; i < len1; i++)
        {
            var start = Math.Max(0, i - matchDistance);
            var end = Math.Min(i + matchDistance + 1, len2);
            for (var j = start; j < end; j++)
            {
                if (s2Matches[j] || s1[i] != s2[j]) continue;
                s1Matches[i] = true;
                s2Matches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0.0;

        double transpositions = 0;
        var k = 0;
        for (var i = 0; i < len1; i++)
        {
            if (!s1Matches[i]) continue;
            while (!s2Matches[k]) k++;
            if (s1[i] != s2[k]) transpositions++;
            k++;
        }
        transpositions /= 2;

        var jaro = ((double)matches / len1 + (double)matches / len2 + (matches - transpositions) / matches) / 3.0;

        var prefixLength = 0;
        var maxPrefix = Math.Min(4, Math.Min(len1, len2));
        for (var i = 0; i < maxPrefix; i++)
        {
            if (s1[i] != s2[i]) break;
            prefixLength++;
        }

        const double scalingFactor = 0.1;
        return jaro + prefixLength * scalingFactor * (1 - jaro);
    }
}
