namespace LinguaCoach.Application.SkillGraph;

/// <summary>Skill Graph container/leaf Phase 2 (2026-07-27) — deterministic (no AI) import of the
/// real CEFR-J Grammar Profile CSV into leaf-level <c>SkillGraphNode</c> rows under container
/// families, per the 2026-07-24 senior pipeline audit's approved schema recommendation. Pure
/// parsing + fuzzy matching against the existing Grammar-skill nodes; never writes to the database
/// itself — the caller (controller) turns the returned preview into the existing bulk
/// <c>POST nodes/import</c> payload once an admin has reviewed it, same "propose, never auto-apply"
/// discipline as every other skill-graph suggestion service.</summary>
public interface ICefrJGrammarImportService
{
    /// <summary>Parses the raw CEFR-J CSV text and proposes a full node/parent mapping against the
    /// given existing Grammar-skill nodes. Deterministic and pure — same input always produces the
    /// same output, no AI, no database access.</summary>
    CefrJGrammarImportPreview ParseAndProposeMapping(
        string csvContent,
        IReadOnlyList<CefrJExistingGrammarNodeCandidate> existingGrammarNodes);
}

/// <summary>An existing Grammar-skill node the importer may match a CEFR-J family container onto,
/// instead of creating a new container node.</summary>
public sealed record CefrJExistingGrammarNodeCandidate(Guid Id, string Key, string Title, string CefrLevel);

/// <summary>Full proposed mapping for one CEFR-J import run. <c>Warnings</c> covers rows that fell
/// back to a default CEFR level (CEFR-J Level was blank and no usable fallback column existed) or
/// otherwise need a human's attention before the admin applies the import — never silently dropped.</summary>
public sealed record CefrJGrammarImportPreview(
    IReadOnlyList<CefrJProposedContainer> Containers,
    IReadOnlyList<CefrJProposedLeaf> StandaloneLeaves,
    IReadOnlyList<string> Warnings)
{
    public int TotalLeafCount => Containers.Sum(c => c.Leaves.Count) + StandaloneLeaves.Count;
}

/// <summary>One CEFR-J grammar "family" (a bare-integer CSV row plus its hyphenated AFF/NEG/INT
/// sibling rows). <c>MatchedExistingNodeKey</c> non-null means the importer found a good title match
/// among the existing 600 canonical nodes and every leaf should nest under THAT node (no new
/// container created); null means <c>Key</c>/<c>Title</c> describe a brand-new container node to
/// create first.</summary>
public sealed record CefrJProposedContainer(
    string Key,
    string Title,
    string CefrLevel,
    int DifficultyBand,
    Guid? MatchedExistingNodeId,
    string? MatchedExistingNodeKey,
    double? MatchConfidence,
    IReadOnlyList<CefrJProposedLeaf> Leaves);

/// <summary>One CEFR-J CSV row → one leaf node proposal. <c>SourceRowId</c> is the CSV's own
/// hierarchical ID (e.g. <c>"8-1"</c>) kept for traceability in the review screen, not used as the
/// node Key.</summary>
public sealed record CefrJProposedLeaf(
    string Key,
    string Title,
    string CefrLevel,
    int DifficultyBand,
    string SourceRowId);
