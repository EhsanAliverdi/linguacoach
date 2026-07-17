namespace LinguaCoach.Application.SkillGraph;

/// <summary>Sprint 1 — deterministic (no AI) validation of the skill graph. Mirrors
/// <c>ICurriculumValidationService</c>'s shape/discipline for the new graph.</summary>
public interface ISkillGraphValidationService
{
    /// <summary>Validates a candidate set of nodes + edges (may include not-yet-approved,
    /// freshly AI-drafted rows) for duplicate keys and circular prerequisite chains before they
    /// can be approved. Pure/read-only.</summary>
    SkillGraphValidationResult Validate(
        IReadOnlyList<SkillGraphNodeSummary> nodes,
        IReadOnlyList<SkillGraphEdgeSummary> edges);
}

/// <summary>Minimal projection of a <c>SkillGraphNode</c> row needed for validation — avoids a
/// domain-layer dependency in the validation service's public surface.</summary>
public sealed record SkillGraphNodeSummary(Guid Id, string Key);

/// <summary>Minimal projection of a <c>SkillGraphPrerequisiteEdge</c> row needed for validation.</summary>
public sealed record SkillGraphEdgeSummary(Guid NodeId, Guid PrerequisiteNodeId);

public sealed record SkillGraphValidationIssue(string NodeKey, string Code, string Message);

public sealed record SkillGraphValidationResult(
    IReadOnlyList<SkillGraphValidationIssue> Errors,
    IReadOnlyList<SkillGraphValidationIssue> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

public static class SkillGraphValidationCodes
{
    public const string DuplicateKey = "duplicate_key";
    public const string PrereqCircular = "prereq_circular";
}

/// <summary>Sprint 1 — AI-drafts skill-graph nodes + prerequisite edges from the CEFR/skill/subskill
/// taxonomy and existing bank content. Advisory only: every proposed node's CEFR level/skill/
/// subskill is validated against the real recognized-value constants before being trusted (an AI
/// hallucination is dropped, never applied) — mirrors
/// <c>ResourceImportColumnMappingService</c>/<c>ResourceCandidateAnalysisService</c>'s convention.
/// Never throws; degrades to a failure result on any AI/parse error so an admin-triggered draft run
/// is never a hard failure.</summary>
public interface ISkillGraphDraftingService
{
    Task<SkillGraphDraftResult> ProposeBatchAsync(SkillGraphDraftRequest request, CancellationToken ct = default);
}

/// <summary>Bounds one AI-drafting call to a single CEFR level x skill combination — keeps each
/// call small/bounded per AGENTS.md, and lets an admin re-run just the combinations that need more
/// nodes rather than one unbounded whole-graph call.</summary>
public sealed record SkillGraphDraftRequest(
    string CefrLevel,
    string Skill,
    /// <summary>Existing node titles/keys already approved or pending for this CEFR/skill
    /// combination, so the AI doesn't propose near-duplicates.</summary>
    IReadOnlyList<string> ExistingNodeTitles);

public sealed record SkillGraphDraftResult(
    bool Success,
    IReadOnlyList<SkillGraphNodeDraftProposal> Nodes,
    string? ErrorMessage);

/// <summary>One AI-proposed node. <c>PrerequisiteTitles</c> references other proposed nodes by
/// title within the same batch (resolved to real Guids only after the batch is persisted) — the AI
/// never invents a prerequisite reference outside its own proposed batch or the existing-titles
/// list it was given.</summary>
public sealed record SkillGraphNodeDraftProposal(
    string Title,
    string Description,
    string CefrLevel,
    string Skill,
    string? Subskill,
    int DifficultyBand,
    string? DescriptionForAi,
    IReadOnlyList<string> PrerequisiteTitles);
