using LinguaCoach.Application.AdminRepair;

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
    IReadOnlyList<string> ExistingNodeTitles,
    /// <summary>Phase 2 of the 2026-07-23 rebuild plan — real, already-approved node titles from
    /// OTHER CEFR levels (same skill) and OTHER skills (same CEFR level), so a proposed node can
    /// name one as a prerequisite via <see cref="SkillGraphNodeDraftProposal.PrerequisiteTitles"/>.
    /// Without this, drafting could structurally never produce a cross-Skill or cross-CEFR-level
    /// edge — the confirmed root cause of the 2026-07-23 audit's "isolated category islands"
    /// finding. Only titles given here (or in <see cref="ExistingNodeTitles"/>, or another node in
    /// the same proposed batch) are ever resolved into a real edge by the caller — an AI-invented
    /// title outside these three sources is dropped, never applied.</summary>
    IReadOnlyList<string> CrossLinkCandidateTitles = null!)
{
    public IReadOnlyList<string> CrossLinkCandidateTitles { get; init; } = CrossLinkCandidateTitles ?? [];
}

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
    IReadOnlyList<string> PrerequisiteTitles,
    // Sprint 14.1 — validated against CurriculumContextTagConstants.All before ever being trusted,
    // same policy as Subskill above.
    IReadOnlyList<string> ContextTags);

/// <summary>Sprint 2 — AI-proposes which approved <c>SkillGraphNode</c>s an existing
/// <c>Module</c> covers. Advisory only: every proposed node key is validated against the real
/// candidate list given in the request before being trusted — an AI-hallucinated key is dropped,
/// never applied. Unlike node drafting (Sprint 1, admin-batch-approved), this result is
/// auto-applied per the explicit "auto-apply, spot-checked via coverage dashboard" decision — there
/// is no separate approval step for individual tag links.</summary>
public interface IModuleSkillGraphTaggingService
{
    Task<ModuleSkillGraphTaggingResult> ProposeCoverageAsync(
        ModuleSkillGraphTaggingRequest request, CancellationToken ct = default);
}

/// <summary>One Module's re-tagging request. <c>CandidateNodes</c> is the real, already
/// CEFR/skill-filtered set of approved nodes the AI is allowed to choose from — bounded per
/// AGENTS.md and because a hallucinated node key must be structurally impossible to apply, not just
/// filtered after the fact.</summary>
public sealed record ModuleSkillGraphTaggingRequest(
    Guid ModuleId,
    string ModuleTitle,
    string ModuleDescription,
    string CefrLevel,
    string Skill,
    IReadOnlyList<SkillGraphNodeCandidate> CandidateNodes);

/// <summary>A candidate node the AI is allowed to match a Module against — Title is included so the
/// AI has enough context to judge relevance; Key is the value it must echo back exactly.</summary>
public sealed record SkillGraphNodeCandidate(Guid Id, string Key, string Title);

public sealed record ModuleSkillGraphTaggingResult(
    bool Success,
    IReadOnlyList<ModuleSkillGraphNodeMatch> Matches,
    string? ErrorMessage);

public sealed record ModuleSkillGraphNodeMatch(Guid NodeId, double Confidence);

// ── Sprint 14.1 — "diagnose then AI-repair" for SkillGraphNode, mirroring the same
// IModuleRepairService/ILessonRepairService/IExerciseRepairService/IResourceBankRepairService
// shape (see AdminRepairContracts). Diagnoses missing
// ContextTagsJson/FocusTagsJson and AI-fills them from CurriculumContextTagConstants.All — the
// same validated vocabulary Module/Sprint-3-goal-vector routing already use, so backfilled tags
// actually match real content-selection logic. ──

public sealed record SkillGraphNodeRepairResult(
    SkillGraphNodeDto Item,
    IReadOnlyList<DiagnosticIssue> IssuesFixed,
    IReadOnlyList<DiagnosticIssue> IssuesRemaining,
    string? ProviderName,
    string? ModelName);

public sealed record SkillGraphNodeDto(
    Guid Id, string Key, string Title, string CefrLevel, string Skill, string? Subskill,
    IReadOnlyList<string> ContextTags, IReadOnlyList<string> FocusTags);

public interface ISkillGraphNodeRepairService
{
    Task<IReadOnlyList<DiagnosticIssue>> DiagnoseAsync(Guid id, CancellationToken ct = default);
    Task<SkillGraphNodeRepairResult> RepairAsync(Guid id, CancellationToken ct = default);
    Task<IssuesSummary> GetIssuesSummaryAsync(CancellationToken ct = default);
    Task<BulkRepairResult> RepairAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RepairableItemSummary>> ListWithIssuesAsync(CancellationToken ct = default);
}
