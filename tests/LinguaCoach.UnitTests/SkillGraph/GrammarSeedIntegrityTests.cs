using System.Text.Json;
using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Infrastructure.SkillGraph;

namespace LinguaCoach.UnitTests.SkillGraph;

/// <summary>
/// Grammar Skill Graph Phase GSG-1 (2026-07-31) — DB-free validators over
/// <c>data/seed-json/grammar-seed.json</c> and <c>grammar-prerequisites-seed.json</c>, permanently
/// enforcing the 2026-07-30 seed audit's hard-failure rules
/// (docs/reviews/2026-07-30-grammar-skill-graph-seed-audit.md §19) so the methodology behind that
/// one-time report becomes a standing CI check rather than a snapshot that can silently drift.
/// Distinct from <see cref="SkillGraphSeedDataTests"/>, which validates the older
/// src/LinguaCoach.Persistence/Seed/SkillGraph/*.json per-CEFR-level format
/// (<c>AdminSkillGraphController.ImportNodes</c>'s contract) — this file targets the
/// containers/leaves + separate prerequisite-edges format <c>ContentSeeder</c> consumes instead.
/// </summary>
public sealed class GrammarSeedIntegrityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private sealed record SeedNode(
        string Key, string Title, string CefrLevel, int DifficultyBand, string? ParentKey,
        string? CefrConfidence, string? CefrSource, string? NodeType, bool RoutingEligible);

    private sealed record ContainerJson(
        string Key, string Title, string CefrLevel, int DifficultyBand, string? ParentKey, string? Description,
        string? CefrConfidence, string? CefrSource, string? NodeType, bool RoutingEligible);

    private sealed record LeafJson(
        string Key, string Title, string CefrLevel, int DifficultyBand, string? ParentKey,
        string GrammarPoint, string Explanation, string? Description,
        string? CefrConfidence, string? CefrSource, string? NodeType, bool RoutingEligible);

    private sealed record SeedFile(int Version, List<ContainerJson> Containers, List<LeafJson> Leaves, List<string>? VersionNotes);

    private sealed record EdgeJson(string Node, string Prerequisite, string Reason);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LinguaCoach.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root (LinguaCoach.slnx) from test output directory.");
    }

    private static (List<SeedNode> Nodes, SeedFile Raw) LoadNodes()
    {
        var path = Path.Combine(FindRepoRoot(), "data", "seed-json", "grammar-seed.json");
        var raw = JsonSerializer.Deserialize<SeedFile>(File.ReadAllText(path), JsonOptions)!;
        var nodes = raw.Containers
            .Select(c => new SeedNode(c.Key, c.Title, c.CefrLevel, c.DifficultyBand, c.ParentKey, c.CefrConfidence, c.CefrSource, c.NodeType, c.RoutingEligible))
            .Concat(raw.Leaves.Select(l => new SeedNode(l.Key, l.Title, l.CefrLevel, l.DifficultyBand, l.ParentKey, l.CefrConfidence, l.CefrSource, l.NodeType, l.RoutingEligible)))
            .ToList();
        return (nodes, raw);
    }

    private static List<EdgeJson> LoadEdges()
    {
        var path = Path.Combine(FindRepoRoot(), "data", "seed-json", "grammar-prerequisites-seed.json");
        // Leading `//` version-note comment (2026-07-30) — JsonCommentHandling.Skip (set in
        // JsonOptions above) parses `//` line comments natively, same as ContentSeeder's own
        // SeedPrerequisitesAsync reader.
        return JsonSerializer.Deserialize<List<EdgeJson>>(File.ReadAllText(path), JsonOptions)!;
    }

    // ── Hard failures ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoDuplicateNodeKeys()
    {
        var (nodes, _) = LoadNodes();
        var duplicates = nodes.GroupBy(n => n.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void NoDuplicateEdgePairs()
    {
        var edges = LoadEdges();
        var duplicates = edges.GroupBy(e => (e.Node, e.Prerequisite)).Where(g => g.Count() > 1).ToList();
        Assert.Empty(duplicates);
    }

    [Fact]
    public void NoMissingParentReferences()
    {
        var (nodes, _) = LoadNodes();
        var keys = nodes.Select(n => n.Key).ToHashSet();
        var dangling = nodes.Where(n => n.ParentKey is not null && !keys.Contains(n.ParentKey)).Select(n => n.Key).ToList();
        Assert.Empty(dangling);
    }

    [Fact]
    public void NoMissingEdgeEndpoints()
    {
        var (nodes, _) = LoadNodes();
        var keys = nodes.Select(n => n.Key).ToHashSet();
        var edges = LoadEdges();
        var missing = edges.Where(e => !keys.Contains(e.Node) || !keys.Contains(e.Prerequisite))
            .Select(e => $"{e.Node} -> {e.Prerequisite}").ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void NoSelfReferencingEdges()
    {
        var edges = LoadEdges();
        var selfLoops = edges.Where(e => e.Node == e.Prerequisite).ToList();
        Assert.Empty(selfLoops);
    }

    [Fact]
    public void GraphIsAcyclic()
    {
        // Reuses the real production DFS cycle detector (same one AdminSkillGraphController and
        // SkillGraphSeedDataTests trust), rather than a second hand-rolled implementation.
        var (nodes, _) = LoadNodes();
        var edges = LoadEdges();
        var idByKey = nodes.ToDictionary(n => n.Key, _ => Guid.NewGuid());
        var nodeSummaries = nodes.Select(n => new SkillGraphNodeSummary(idByKey[n.Key], n.Key)).ToList();
        var edgeSummaries = edges
            .Where(e => idByKey.ContainsKey(e.Node) && idByKey.ContainsKey(e.Prerequisite))
            .Select(e => new SkillGraphEdgeSummary(idByKey[e.Node], idByKey[e.Prerequisite]))
            .ToList();

        var result = new SkillGraphValidationService().Validate(nodeSummaries, edgeSummaries);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    // ── Provenance-shape hard failures (Phase GSG-1) ───────────────────────────────────────────────

    [Fact]
    public void EveryNodeHasANodeTypeClassification()
    {
        var (nodes, _) = LoadNodes();
        var unclassified = nodes.Where(n => string.IsNullOrWhiteSpace(n.NodeType)).Select(n => n.Key).ToList();
        Assert.True(unclassified.Count == 0,
            $"{unclassified.Count} node(s) have no nodeType — every grammar node must be classified after the GSG-1 backfill. First few: {string.Join(", ", unclassified.Take(10))}");
    }

    [Fact]
    public void RoutingEligibleNodesAreOnlySkillOrVariantWithAttestedOrCuratedConfidence()
    {
        // The concrete, permanent enforcement of "unknown/unreviewed nodes are not routing
        // eligible" — fails the build if a future edit sets routingEligible:true without the
        // node type/confidence to back it up, rather than relying on a human remembering the rule.
        var (nodes, _) = LoadNodes();
        var violations = nodes
            .Where(n => n.RoutingEligible)
            .Where(n =>
                n.NodeType is not ("skill" or "variant") ||
                n.CefrConfidence is not ("attested" or "curated"))
            .Select(n => $"{n.Key} (nodeType={n.NodeType}, cefrConfidence={n.CefrConfidence})")
            .ToList();

        Assert.True(violations.Count == 0,
            $"{violations.Count} routing-eligible node(s) don't meet the Skill/Variant + Attested/Curated bar: {string.Join(", ", violations.Take(10))}");
    }

    // ── Ratchet-style regression guards for the audit's warning-level metrics ─────────────────────
    // These assert "no worse than the 2026-07-30 audit's baseline," not zero — the underlying content
    // defects are Phase GSG-2/GSG-4 work, not GSG-1's job to fix. The point is catching a future
    // change that makes one of these *worse* without pre-blocking on the pre-existing baseline.

    [Fact]
    public void MixedLevelContainerCount_DoesNotRegressPastAuditBaseline()
    {
        var (nodes, _) = LoadNodes();
        var byKey = nodes.ToDictionary(n => n.Key);
        var childrenByParent = nodes.Where(n => n.ParentKey is not null)
            .GroupBy(n => n.ParentKey!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var levelRank = new Dictionary<string, int> { ["A1"] = 0, ["A2"] = 1, ["B1"] = 2, ["B2"] = 3, ["C1"] = 4, ["C2"] = 5 };
        var mixedCount = 0;
        foreach (var (parentKey, children) in childrenByParent)
        {
            if (!byKey.ContainsKey(parentKey)) continue; // already caught by NoMissingParentReferences
            var levels = children.Select(c => levelRank.GetValueOrDefault(c.CefrLevel, -1)).Where(l => l >= 0).ToList();
            if (levels.Count > 0 && levels.Min() != levels.Max()) mixedCount++;
        }

        Assert.True(mixedCount <= 56,
            $"Mixed-level container count grew to {mixedCount} (2026-07-30 audit baseline: 56). If this is expected (e.g. a real GSG-4 curation fix), update this baseline deliberately rather than just widening it.");
    }

    [Fact]
    public void BackwardCefrEdgeCount_DoesNotRegressPastAuditBaseline()
    {
        var (nodes, _) = LoadNodes();
        var byKey = nodes.ToDictionary(n => n.Key);
        var edges = LoadEdges();
        var levelRank = new Dictionary<string, int> { ["A1"] = 0, ["A2"] = 1, ["B1"] = 2, ["B2"] = 3, ["C1"] = 4, ["C2"] = 5 };

        var backwardCount = edges.Count(e =>
        {
            if (!byKey.TryGetValue(e.Node, out var node) || !byKey.TryGetValue(e.Prerequisite, out var prereq)) return false;
            if (!levelRank.TryGetValue(node.CefrLevel, out var nodeRank) || !levelRank.TryGetValue(prereq.CefrLevel, out var prereqRank)) return false;
            return prereqRank > nodeRank;
        });

        Assert.True(backwardCount <= 120,
            $"Backward-CEFR prerequisite edge count grew to {backwardCount} (2026-07-30 audit baseline: 120).");
    }

    [Fact]
    public void TransitivelyRedundantEdgeCount_DoesNotRegressPastAuditBaseline()
    {
        var edges = LoadEdges();
        var adjacency = new Dictionary<string, HashSet<string>>();
        foreach (var e in edges)
        {
            if (!adjacency.TryGetValue(e.Node, out var set)) adjacency[e.Node] = set = [];
            set.Add(e.Prerequisite);
        }

        int redundantCount = 0;
        foreach (var e in edges)
        {
            // Reachable from e.Node to e.Prerequisite via any OTHER edge (BFS skipping the direct edge).
            var visited = new HashSet<string>();
            var queue = new Queue<string>(adjacency.GetValueOrDefault(e.Node, []).Where(v => v != e.Prerequisite));
            var found = false;
            while (queue.Count > 0 && !found)
            {
                var u = queue.Dequeue();
                if (!visited.Add(u)) continue;
                if (u == e.Prerequisite) { found = true; break; }
                foreach (var v in adjacency.GetValueOrDefault(u, [])) queue.Enqueue(v);
            }
            if (found) redundantCount++;
        }

        Assert.True(redundantCount <= 274,
            $"Transitively redundant edge count grew to {redundantCount} (2026-07-30 audit baseline: 274).");
    }
}
