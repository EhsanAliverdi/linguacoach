using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Domain.Constants;

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
///
/// Phase 6.3d — reparenting-on-edit review: when an edit moves a node to a different CEFR level
/// and/or Skill, its existing edges were chosen under the old placement and may no longer make
/// sense — flags genuine CEFR-ordering violations (a prerequisite at a later stage than the node,
/// or a dependent at an earlier one) for the admin to review, without ever removing anything
/// automatically.
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

    /// <summary>User-reported false positive (2026-07-24): title-only Jaro-Winkler flagged
    /// "Reading a short biography" vs "Reading a holiday blog post" as 89% similar — JW is a
    /// character-positional metric built for short strings like names, and rewards the two titles'
    /// shared common English letters/shared "Reading a " prefix even though the actual topics are
    /// unrelated. Bigram (character 2-gram) Dice coefficient is far more discriminative for
    /// sentence-length text, and factoring in description similarity (title carries most of the
    /// weight; description catches cases where near-identical titles cover genuinely different
    /// content) further reduces false positives.</summary>
    private const double TitleWeight = 0.7;
    private const double DescriptionWeight = 0.3;

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
                    var titleSimilarity = BigramDiceSimilarity(members[i].Title, members[j].Title);
                    var descriptionSimilarity = BigramDiceSimilarity(members[i].Description, members[j].Description);
                    var similarity = TitleWeight * titleSimilarity + DescriptionWeight * descriptionSimilarity;
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

    /// <summary>Sorensen-Dice coefficient over character bigrams, case-insensitive, counting
    /// repeated bigrams (a multiset intersection, not a plain set intersection — "aabb" vs "aabb"
    /// must score 1.0). Returns 1.0 for identical strings, 0.0 when either string is too short to
    /// form a bigram (or empty) and the strings aren't identical.</summary>
    private static double BigramDiceSimilarity(string s1, string s2)
    {
        s1 = s1.Trim().ToLowerInvariant();
        s2 = s2.Trim().ToLowerInvariant();
        if (s1 == s2) return 1.0;
        if (s1.Length < 2 || s2.Length < 2) return 0.0;

        var counts1 = BigramCounts(s1);
        var counts2 = BigramCounts(s2);

        var intersection = 0;
        foreach (var (bigram, count1) in counts1)
        {
            if (counts2.TryGetValue(bigram, out var count2))
                intersection += Math.Min(count1, count2);
        }

        var total1 = s1.Length - 1;
        var total2 = s2.Length - 1;
        return 2.0 * intersection / (total1 + total2);
    }

    private static Dictionary<string, int> BigramCounts(string s)
    {
        var counts = new Dictionary<string, int>();
        for (var i = 0; i < s.Length - 1; i++)
        {
            var bigram = s.Substring(i, 2);
            counts[bigram] = counts.GetValueOrDefault(bigram) + 1;
        }
        return counts;
    }

    public ReparentReviewResult? DetectReparentingReview(
        Guid nodeId, string oldCefrLevel, string oldSkill, string newCefrLevel, string newSkill,
        IReadOnlyList<ReparentEdgeNeighbor> neighbors)
    {
        var levelChanged = !string.Equals(oldCefrLevel, newCefrLevel, StringComparison.OrdinalIgnoreCase);
        var skillChanged = !string.Equals(oldSkill, newSkill, StringComparison.OrdinalIgnoreCase);
        if (!levelChanged && !skillChanged) return null; // nothing moved — no review needed
        if (neighbors.Count == 0) return null; // moved, but no edges exist to review

        var newLevelOrdinal = CefrOrdinal(newCefrLevel);

        var edges = neighbors.Select(n =>
        {
            var otherOrdinal = CefrOrdinal(n.OtherNodeCefrLevel);
            // A prerequisite should sit at the same or an earlier CEFR stage than the node it
            // unlocks; a dependent should sit at the same or a later one. Same-level is never
            // suspicious — a single CEFR band legitimately orders ~100 nodes internally.
            var suspicious = otherOrdinal >= 0 && newLevelOrdinal >= 0 &&
                (n.OtherNodeIsPrerequisite ? otherOrdinal > newLevelOrdinal : otherOrdinal < newLevelOrdinal);
            return new ReparentReviewEdge(n.OtherNodeId, n.OtherNodeIsPrerequisite, n.OtherNodeCefrLevel, suspicious);
        }).ToList();

        return new ReparentReviewResult(nodeId, oldCefrLevel, newCefrLevel, oldSkill, newSkill, edges);
    }

    private static int CefrOrdinal(string level)
    {
        for (var i = 0; i < CefrLevelConstants.All.Count; i++)
        {
            if (string.Equals(CefrLevelConstants.All[i], level, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }
}
