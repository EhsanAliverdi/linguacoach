using LinguaCoach.Application.SkillGraph;

namespace LinguaCoach.Infrastructure.SkillGraph;

/// <summary>
/// Sprint 1 — deterministic (no AI) validation of the skill graph: duplicate keys and circular
/// prerequisite chains via DFS. Mirrors <c>CurriculumValidationService</c>'s discipline, adapted for
/// a real Guid-keyed edge table rather than a JSON array of string keys.
/// </summary>
public sealed class SkillGraphValidationService : ISkillGraphValidationService
{
    public SkillGraphValidationResult Validate(
        IReadOnlyList<SkillGraphNodeSummary> nodes,
        IReadOnlyList<SkillGraphEdgeSummary> edges)
    {
        var errors = new List<SkillGraphValidationIssue>();
        var warnings = new List<SkillGraphValidationIssue>();

        var byId = new Dictionary<Guid, string>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            byId[node.Id] = node.Key;
            if (!seenKeys.Add(node.Key))
                errors.Add(new(node.Key, SkillGraphValidationCodes.DuplicateKey,
                    $"Node key '{node.Key}' appears more than once in the set."));
        }

        DetectCircularPrerequisites(nodes, edges, byId, errors);

        return new SkillGraphValidationResult(errors, warnings);
    }

    private static void DetectCircularPrerequisites(
        IReadOnlyList<SkillGraphNodeSummary> nodes,
        IReadOnlyList<SkillGraphEdgeSummary> edges,
        Dictionary<Guid, string> byId,
        List<SkillGraphValidationIssue> errors)
    {
        var adj = new Dictionary<Guid, List<Guid>>();
        foreach (var node in nodes)
            adj[node.Id] = [];
        foreach (var edge in edges)
        {
            // Only include edges where both ends are in the current candidate set — an edge
            // referencing a node outside this batch is out of scope for this validation pass.
            if (adj.ContainsKey(edge.NodeId) && byId.ContainsKey(edge.PrerequisiteNodeId))
                adj[edge.NodeId].Add(edge.PrerequisiteNodeId);
        }

        var state = new Dictionary<Guid, int>(); // 0=unvisited, 1=in-stack, 2=done
        var reported = new HashSet<Guid>();

        foreach (var id in adj.Keys)
            DfsVisit(id, adj, byId, state, reported, errors);
    }

    private static void DfsVisit(
        Guid node,
        Dictionary<Guid, List<Guid>> adj,
        Dictionary<Guid, string> byId,
        Dictionary<Guid, int> state,
        HashSet<Guid> reported,
        List<SkillGraphValidationIssue> errors)
    {
        if (!state.TryGetValue(node, out var s)) s = 0;
        if (s == 2) return;
        if (s == 1)
        {
            if (reported.Add(node))
                errors.Add(new(byId.GetValueOrDefault(node, node.ToString()), SkillGraphValidationCodes.PrereqCircular,
                    $"Circular prerequisite chain detected involving node '{byId.GetValueOrDefault(node, node.ToString())}'."));
            return;
        }

        state[node] = 1;
        if (adj.TryGetValue(node, out var prereqs))
        {
            foreach (var prereq in prereqs)
                DfsVisit(prereq, adj, byId, state, reported, errors);
        }
        state[node] = 2;
    }
}
