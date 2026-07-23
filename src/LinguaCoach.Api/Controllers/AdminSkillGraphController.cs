using System.Security.Claims;
using System.Text.Json;
using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Adaptive Curriculum Sprint 1 — admin surface for the skill/prerequisite graph: trigger AI
/// drafting for one CEFR level x skill combination, browse/filter nodes, batch approve/reject
/// (never per-node — see docs/architecture/adaptive-curriculum-skill-graph.md), and a coverage
/// matrix. Deliberately additive only in Sprint 1: nothing outside this controller reads
/// <c>SkillGraphNode</c>/<c>SkillGraphPrerequisiteEdge</c> yet.
/// </summary>
[ApiController]
[Route("api/admin/skill-graph")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminSkillGraphController : ControllerBase
{
    private const int MaxBatchSize = 200;
    // Sprint 2 — bounds one re-tag sweep call to a small number of Modules per call (real AI calls,
    // one per Module), mirroring the per-combination bounding SkillGraphDraftingService already
    // uses. An admin can call the endpoint again to sweep the next batch.
    private const int MaxModulesPerRetagSweep = 20;

    private readonly LinguaCoachDbContext _db;
    private readonly ISkillGraphDraftingService _drafting;
    private readonly ISkillGraphValidationService _validation;
    private readonly IModuleSkillGraphTaggingService _tagging;
    private readonly ISkillGraphNodeRepairService _repair;

    public AdminSkillGraphController(
        LinguaCoachDbContext db, ISkillGraphDraftingService drafting, ISkillGraphValidationService validation,
        IModuleSkillGraphTaggingService tagging, ISkillGraphNodeRepairService repair)
    {
        _db = db;
        _drafting = drafting;
        _validation = validation;
        _tagging = tagging;
        _repair = repair;
    }

    // ── Sprint 14.1 — node tag diagnose+AI-repair, mirrors Resource Bank's
    // issues-summary/with-issues/{id}/repair/repair-all shape exactly so the frontend can reuse
    // the existing AdminBulkRepairService (client-driven loop + toast progress) unchanged. ──

    [HttpGet("nodes/issues-summary")]
    public async Task<IActionResult> GetNodeIssuesSummary(CancellationToken ct)
        => Ok(await _repair.GetIssuesSummaryAsync(ct));

    [HttpGet("nodes/with-issues")]
    public async Task<IActionResult> ListNodesWithIssues(CancellationToken ct)
        => Ok(await _repair.ListWithIssuesAsync(ct));

    [HttpPost("nodes/{id:guid}/repair")]
    public async Task<IActionResult> RepairNode(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _repair.RepairAsync(id, ct));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("nodes/repair-all")]
    public async Task<IActionResult> RepairAllNodes(CancellationToken ct)
        => Ok(await _repair.RepairAllAsync(ct));

    [HttpGet("taxonomy")]
    public IActionResult GetTaxonomy()
    {
        return Ok(new
        {
            cefrLevels = CefrLevelConstants.All,
            skills = CurriculumSkillConstants.All,
            subskillsBySkill = CurriculumSkillConstants.All.ToDictionary(
                s => s, s => CurriculumSubskillConstants.ForSkill(s)),
        });
    }

    [HttpGet("nodes")]
    public async Task<IActionResult> GetNodes(
        [FromQuery] string? cefrLevel, [FromQuery] string? skill, [FromQuery] string? reviewStatus,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.SkillGraphNodes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(cefrLevel)) query = query.Where(n => n.CefrLevel == cefrLevel.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(skill)) query = query.Where(n => n.Skill == skill.ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(reviewStatus) && Enum.TryParse<AdminReviewStatus>(reviewStatus, true, out var status))
            query = query.Where(n => n.ReviewStatus == status);

        var totalCount = await query.CountAsync(ct);
        var rawItems = await query
            .OrderBy(n => n.CefrLevel).ThenBy(n => n.Skill).ThenBy(n => n.Title)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new
            {
                n.Id, n.Key, n.Title, n.Description, n.CefrLevel, n.Skill, n.Subskill,
                n.DifficultyBand, n.ReviewStatus, n.IsActive, n.RejectionReason, n.CreatedAt,
                n.ContextTagsJson, n.FocusTagsJson,
            })
            .ToListAsync(ct);

        // Content-coverage merge (2026-07-23) — the Nodes table and the old separate "Content
        // coverage" table showed near-identical data; folding LinkedModuleCount in here (bounded to
        // this page's ids only) is what let the Content coverage card be deleted outright instead
        // of duplicating the same node list in two places.
        var pageIds = rawItems.Select(n => n.Id).ToList();
        var linkCounts = await _db.ModuleSkillGraphNodeLinks.AsNoTracking()
            .Where(l => pageIds.Contains(l.SkillGraphNodeId))
            .GroupBy(l => l.SkillGraphNodeId)
            .Select(g => new { NodeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.NodeId, g => g.Count, ct);

        var items = rawItems.Select(n => new
        {
            n.Id, n.Key, n.Title, n.Description, n.CefrLevel, n.Skill, n.Subskill,
            n.DifficultyBand, n.ReviewStatus, n.IsActive, n.RejectionReason, n.CreatedAt,
            ContextTags = ParseTags(n.ContextTagsJson), FocusTags = ParseTags(n.FocusTagsJson),
            LinkedModuleCount = linkCounts.GetValueOrDefault(n.Id, 0),
        });

        return Ok(new
        {
            items,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            page, pageSize,
        });
    }

    /// <summary>Sprint 13 — bulk payload for the Cytoscape/Dagre graph view: every active node
    /// (219/219 at last count, cheap regardless of ReviewStatus so a PendingReview node is visible
    /// pre-approval too) plus every prerequisite edge in one call — the paginated <see cref="GetNodes"/>
    /// never includes edges, and <see cref="GetNode"/> only resolves one node's own prerequisites.</summary>
    [HttpGet("graph")]
    public async Task<IActionResult> GetGraph(CancellationToken ct)
    {
        var rawNodes = await _db.SkillGraphNodes.AsNoTracking()
            .Where(n => n.IsActive)
            .Select(n => new
            {
                n.Id, n.Key, n.Title, n.CefrLevel, n.Skill, n.Subskill, n.DifficultyBand, n.ReviewStatus,
                n.ContextTagsJson, n.FocusTagsJson,
            })
            .ToListAsync(ct);

        var nodes = rawNodes.Select(n => new
        {
            n.Id, n.Key, n.Title, n.CefrLevel, n.Skill, n.Subskill, n.DifficultyBand, n.ReviewStatus,
            ContextTags = ParseTags(n.ContextTagsJson), FocusTags = ParseTags(n.FocusTagsJson),
        });

        var edges = await _db.SkillGraphPrerequisiteEdges.AsNoTracking()
            .Select(e => new { e.NodeId, e.PrerequisiteNodeId })
            .ToListAsync(ct);

        return Ok(new { nodes, edges });
    }

    private static List<string> ParseTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    [HttpGet("nodes/{id:guid}")]
    public async Task<IActionResult> GetNode(Guid id, CancellationToken ct)
    {
        var node = await _db.SkillGraphNodes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
        if (node is null) return NotFound();

        var prerequisites = await _db.SkillGraphPrerequisiteEdges.AsNoTracking()
            .Where(e => e.NodeId == id)
            .Join(_db.SkillGraphNodes.AsNoTracking(), e => e.PrerequisiteNodeId, n => n.Id, (e, n) => new { n.Id, n.Key, n.Title })
            .ToListAsync(ct);

        // Editability audit (2026-07-23) — "Unlocks": nodes that list this one as a prerequisite,
        // needed by the admin UI's Prerequisites/Unlocks pair (Phase 1).
        var dependents = await _db.SkillGraphPrerequisiteEdges.AsNoTracking()
            .Where(e => e.PrerequisiteNodeId == id)
            .Join(_db.SkillGraphNodes.AsNoTracking(), e => e.NodeId, n => n.Id, (e, n) => new { n.Id, n.Key, n.Title })
            .ToListAsync(ct);

        // Content-coverage merge (2026-07-23) — real Modules linked to this node, previously only
        // visible via the separate "Content coverage" table's own slide-over.
        var linkedModules = await _db.ModuleSkillGraphNodeLinks.AsNoTracking()
            .Where(l => l.SkillGraphNodeId == id)
            .Join(_db.Modules.AsNoTracking(), l => l.ModuleId, m => m.Id, (l, m) => new { m.Id, m.Title })
            .ToListAsync(ct);

        return Ok(new
        {
            node.Id, node.Key, node.Title, node.Description, node.CefrLevel, node.Skill, node.Subskill,
            node.DifficultyBand, node.DescriptionForAi, node.ReviewStatus, node.IsActive,
            node.RejectionReason, node.ReviewedByUserId, node.ApprovedAtUtc, node.RejectedAtUtc,
            ContextTags = ParseTags(node.ContextTagsJson), FocusTags = ParseTags(node.FocusTagsJson),
            prerequisites,
            dependents,
            linkedModules,
        });
    }

    // ── Editability audit (2026-07-23) — manual node CRUD + manual edge management. Prior to this,
    // AdminSkillGraphController.Draft() was the only way to create a node or a prerequisite edge,
    // and nothing could ever edit a node's core fields or manually link/unlink two existing nodes. ──

    /// <summary>Create node UX audit (2026-07-23) — a node's "place in the graph" is exactly as
    /// important as its content, so creation accepts an optional set of prerequisite node ids up
    /// front rather than forcing a create-then-separately-link two-step. Each requested prerequisite
    /// is added through the same cycle-validated <see cref="TryAddPrerequisiteEdgeAsync"/> path
    /// manual edge management already uses — a bad request here can never corrupt the graph, it
    /// just reports which prerequisites were dropped and why.</summary>
    [HttpPost("nodes")]
    public async Task<IActionResult> CreateNode([FromBody] CreateSkillGraphNodeRequest request, CancellationToken ct)
    {
        if (!CefrLevelConstants.IsValid(request.CefrLevel))
            return BadRequest(new { error = $"Invalid CEFR level '{request.CefrLevel}'." });
        if (!CurriculumSkillConstants.IsValid(request.Skill))
            return BadRequest(new { error = $"Invalid skill '{request.Skill}'." });

        var key = BuildKey(request.CefrLevel, request.Skill, request.Title);
        if (await _db.SkillGraphNodes.AnyAsync(n => n.Key == key, ct))
            return Conflict(new { error = $"A node with key '{key}' already exists (same CEFR level, skill, and a matching title slug)." });

        SkillGraphNode node;
        try
        {
            node = new SkillGraphNode(
                key, request.Title, request.Description, request.CefrLevel, request.Skill,
                request.Subskill, request.DifficultyBand, request.DescriptionForAi,
                contextTagsJson: request.ContextTags?.Count > 0 ? JsonSerializer.Serialize(request.ContextTags) : null,
                focusTagsJson: request.FocusTags?.Count > 0 ? JsonSerializer.Serialize(request.FocusTags) : null);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        _db.SkillGraphNodes.Add(node);
        await _db.SaveChangesAsync(ct); // assigns node.Id before any edge can reference it

        var droppedPrerequisites = new List<object>();
        foreach (var prereqId in (request.PrerequisiteNodeIds ?? []).Distinct())
        {
            var (added, error) = await TryAddPrerequisiteEdgeAsync(node.Id, prereqId, ct);
            if (!added) droppedPrerequisites.Add(new { prerequisiteNodeId = prereqId, error });
        }

        // Editability follow-up (2026-07-23) — the graph is genuinely many-to-many in both
        // directions (one node can have several prerequisites, and be the prerequisite for
        // several nodes), so creation accepts "what this node unlocks" symmetrically to "what
        // this node depends on" — same helper, arguments swapped (the dependent becomes NodeId,
        // this new node becomes PrerequisiteNodeId).
        var droppedDependents = new List<object>();
        foreach (var dependentId in (request.DependentNodeIds ?? []).Distinct())
        {
            var (added, error) = await TryAddPrerequisiteEdgeAsync(dependentId, node.Id, ct);
            if (!added) droppedDependents.Add(new { dependentNodeId = dependentId, error });
        }

        return Ok(new { node.Id, node.Key, droppedPrerequisites, droppedDependents });
    }

    [HttpPut("nodes/{id:guid}")]
    public async Task<IActionResult> UpdateNode(Guid id, [FromBody] UpdateSkillGraphNodeRequest request, CancellationToken ct)
    {
        var node = await _db.SkillGraphNodes.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (node is null) return NotFound();

        try
        {
            node.UpdateCore(
                request.Title, request.Description, request.CefrLevel, request.Skill,
                request.Subskill, request.DifficultyBand, request.DescriptionForAi);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { node.Id, node.Key });
    }

    /// <summary>Manually links two existing nodes as prerequisite→node. Validated for cycles
    /// against the FULL active node/edge set (not a batch subset) before committing — unlike
    /// <see cref="Draft"/>'s validation, which only ever saw its own drafted batch.</summary>
    [HttpPost("nodes/{id:guid}/prerequisites")]
    public async Task<IActionResult> AddPrerequisite(Guid id, [FromBody] AddSkillGraphPrerequisiteRequest request, CancellationToken ct)
    {
        var nodeExists = await _db.SkillGraphNodes.AnyAsync(n => n.Id == id, ct);
        if (!nodeExists) return NotFound(new { error = "Node not found." });

        var (added, error) = await TryAddPrerequisiteEdgeAsync(id, request.PrerequisiteNodeId, ct);
        if (!added)
            return error == "This prerequisite edge already exists."
                ? Conflict(new { error })
                : error == "Prerequisite node not found."
                    ? NotFound(new { error })
                    : Conflict(new { error });

        return Ok(new { added = true });
    }

    /// <summary>Shared cycle-validated edge-creation path, used by both <see cref="AddPrerequisite"/>
    /// and <see cref="CreateNode"/>'s optional up-front prerequisites. Always validates against the
    /// FULL active node/edge set before committing — never trusts a caller-supplied id blindly.
    /// An already-existing identical edge is treated as a successful no-op (idempotent).</summary>
    private async Task<(bool Added, string? Error)> TryAddPrerequisiteEdgeAsync(Guid nodeId, Guid prerequisiteNodeId, CancellationToken ct)
    {
        if (nodeId == prerequisiteNodeId)
            return (false, "A node cannot be its own prerequisite.");

        var prereqExists = await _db.SkillGraphNodes.AnyAsync(n => n.Id == prerequisiteNodeId, ct);
        if (!prereqExists)
            return (false, "Prerequisite node not found.");

        var alreadyExists = await _db.SkillGraphPrerequisiteEdges
            .AnyAsync(e => e.NodeId == nodeId && e.PrerequisiteNodeId == prerequisiteNodeId, ct);
        if (alreadyExists)
            return (false, "This prerequisite edge already exists.");

        var allNodes = await _db.SkillGraphNodes.AsNoTracking()
            .Select(n => new SkillGraphNodeSummary(n.Id, n.Key)).ToListAsync(ct);
        var allEdges = await _db.SkillGraphPrerequisiteEdges.AsNoTracking()
            .Select(e => new SkillGraphEdgeSummary(e.NodeId, e.PrerequisiteNodeId)).ToListAsync(ct);
        var trialEdges = allEdges.Append(new SkillGraphEdgeSummary(nodeId, prerequisiteNodeId)).ToList();

        var validation = _validation.Validate(allNodes, trialEdges);
        if (!validation.IsValid)
            return (false, "Adding this edge would create a circular prerequisite chain.");

        _db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(nodeId, prerequisiteNodeId));
        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    [HttpDelete("nodes/{id:guid}/prerequisites/{prerequisiteId:guid}")]
    public async Task<IActionResult> RemovePrerequisite(Guid id, Guid prerequisiteId, CancellationToken ct)
    {
        var edge = await _db.SkillGraphPrerequisiteEdges
            .FirstOrDefaultAsync(e => e.NodeId == id && e.PrerequisiteNodeId == prerequisiteId, ct);
        if (edge is null) return NotFound();

        _db.SkillGraphPrerequisiteEdges.Remove(edge);
        await _db.SaveChangesAsync(ct);
        return Ok(new { removed = true });
    }

    /// <summary>Isolated-node connectivity metric: nodes with zero edges on BOTH sides (no
    /// prerequisites AND no dependents) — the real defect signal the 2026-07-23 audit's screenshot
    /// surfaced. A node with only one side populated (a foundational root, or a terminal leaf) is
    /// not flagged — only full isolation is.</summary>
    [HttpGet("nodes/isolated")]
    public async Task<IActionResult> GetIsolatedNodes(CancellationToken ct)
    {
        var edgeEndpoints = await _db.SkillGraphPrerequisiteEdges.AsNoTracking()
            .Select(e => new { e.NodeId, e.PrerequisiteNodeId })
            .ToListAsync(ct);
        var nodeIdsWithEdges = edgeEndpoints.SelectMany(e => new[] { e.NodeId, e.PrerequisiteNodeId }).Distinct().ToList();

        var isolated = await _db.SkillGraphNodes.AsNoTracking()
            .Where(n => n.IsActive && !nodeIdsWithEdges.Contains(n.Id))
            .OrderBy(n => n.CefrLevel).ThenBy(n => n.Skill).ThenBy(n => n.Title)
            .Select(n => new { n.Id, n.Key, n.Title, n.CefrLevel, n.Skill, n.ReviewStatus })
            .ToListAsync(ct);

        return Ok(new { isolatedCount = isolated.Count, isolated });
    }

    /// <summary>Bulk canonical-import endpoint (Phase 4 of the 2026-07-23 rebuild plan): upserts a
    /// structured node+edge dataset by Key (idempotent — re-running the same file is safe). Every
    /// node is validated against the real taxonomy exactly like manual/AI creation; prerequisite
    /// keys are resolved against the FULL active graph (this import's own batch plus every
    /// already-imported node), not scoped to one CEFR/skill combination like <see cref="Draft"/> —
    /// this is what actually allows cross-skill/cross-level edges. Imported nodes always start
    /// PendingReview, batch-approved afterward via the existing approve endpoint, same discipline
    /// as Sprint 1's original 219-node sweep.</summary>
    [HttpPost("nodes/import")]
    public async Task<IActionResult> ImportNodes([FromBody] ImportSkillGraphRequest request, CancellationToken ct)
    {
        var existingByKey = await _db.SkillGraphNodes.ToDictionaryAsync(n => n.Key, ct);
        var createdCount = 0;
        var updatedCount = 0;
        var errors = new List<string>();

        foreach (var item in request.Nodes)
        {
            try
            {
                if (existingByKey.TryGetValue(item.Key, out var existing))
                {
                    existing.UpdateCore(
                        item.Title, item.Description, item.CefrLevel, item.Skill,
                        item.Subskill, item.DifficultyBand, item.DescriptionForAi);
                    existing.UpdateTags(
                        item.ContextTags.Count > 0 ? JsonSerializer.Serialize(item.ContextTags) : null,
                        item.FocusTags.Count > 0 ? JsonSerializer.Serialize(item.FocusTags) : null);
                    updatedCount++;
                }
                else
                {
                    var node = new SkillGraphNode(
                        item.Key, item.Title, item.Description, item.CefrLevel, item.Skill,
                        item.Subskill, item.DifficultyBand, item.DescriptionForAi,
                        contextTagsJson: item.ContextTags.Count > 0 ? JsonSerializer.Serialize(item.ContextTags) : null,
                        focusTagsJson: item.FocusTags.Count > 0 ? JsonSerializer.Serialize(item.FocusTags) : null);
                    _db.SkillGraphNodes.Add(node);
                    existingByKey[item.Key] = node;
                    createdCount++;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                errors.Add($"{item.Key}: {ex.Message}");
            }
        }

        await _db.SaveChangesAsync(ct); // assigns Ids to newly-created nodes before edge resolution

        var existingEdges = await _db.SkillGraphPrerequisiteEdges.AsNoTracking()
            .Select(e => new SkillGraphEdgeSummary(e.NodeId, e.PrerequisiteNodeId))
            .ToListAsync(ct);
        var allNodeSummaries = existingByKey.Values.Select(n => new SkillGraphNodeSummary(n.Id, n.Key)).ToList();

        var addedEdgeCount = 0;
        var droppedEdgeCount = 0;
        foreach (var item in request.Nodes)
        {
            if (!existingByKey.TryGetValue(item.Key, out var node)) continue;
            foreach (var prereqKey in item.PrerequisiteKeys)
            {
                if (!existingByKey.TryGetValue(prereqKey, out var prereqNode) || prereqNode.Id == node.Id) continue;
                if (existingEdges.Any(e => e.NodeId == node.Id && e.PrerequisiteNodeId == prereqNode.Id)) continue; // already linked

                var trialEdges = existingEdges.Append(new SkillGraphEdgeSummary(node.Id, prereqNode.Id)).ToList();
                var validation = _validation.Validate(allNodeSummaries, trialEdges);
                if (!validation.IsValid) { droppedEdgeCount++; continue; }

                _db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(node.Id, prereqNode.Id));
                existingEdges.Add(new SkillGraphEdgeSummary(node.Id, prereqNode.Id));
                addedEdgeCount++;
            }
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new { createdCount, updatedCount, addedEdgeCount, droppedEdgeCount, errors });
    }

    // Rebuild Phase 2 (2026-07-23) — bounds how many other-combination titles are offered to the AI
    // as cross-link prerequisite candidates, mirroring MaxExistingTitles' bounded-call discipline.
    private const int MaxCrossLinkCandidates = 40;

    /// <summary>Triggers one bounded AI-drafting call for a single CEFR level x skill combination,
    /// persists every proposal as a PendingReview node, then resolves PrerequisiteTitles into real
    /// edges. Rebuild Phase 2 (2026-07-23) — an edge can now resolve against three sources: (a)
    /// another node in this same batch, (b) an existing node for the same CEFR/skill, or (c) an
    /// approved node from a DIFFERENT CEFR level (same skill) or a DIFFERENT skill (same CEFR
    /// level) — the "cross-link candidates" the AI was given. Previously only (a)/(b) were possible,
    /// which is the confirmed root cause of the 2026-07-23 audit's isolated-category-islands
    /// finding: no code path could ever create a cross-Skill or cross-CEFR-level edge. Every
    /// resulting edge set — regardless of source — is still validated against the FULL active graph
    /// (not just this batch) before being applied; edges that would introduce a cycle are dropped
    /// and reported, never silently applied.</summary>
    [HttpPost("draft")]
    public async Task<IActionResult> Draft([FromBody] DraftSkillGraphRequest request, CancellationToken ct)
    {
        if (!CefrLevelConstants.IsValid(request.CefrLevel))
            return BadRequest(new { error = $"Invalid CEFR level '{request.CefrLevel}'." });
        if (!CurriculumSkillConstants.IsValid(request.Skill))
            return BadRequest(new { error = $"Invalid skill '{request.Skill}'." });

        var cefrLevel = request.CefrLevel.ToUpperInvariant();
        var skill = request.Skill.ToLowerInvariant();

        var existingTitles = await _db.SkillGraphNodes.AsNoTracking()
            .Where(n => n.CefrLevel == cefrLevel && n.Skill == skill)
            .Select(n => n.Title)
            .ToListAsync(ct);

        // Rebuild Phase 2 — real, already-approved nodes from other CEFR levels of this same skill,
        // or other skills at this same CEFR level: exactly the two cross-link shapes the audit's
        // examples named (grammar A1 → speaking A1; grammar A1 → grammar A2).
        var crossLinkNodes = await _db.SkillGraphNodes.AsNoTracking()
            .Where(n => n.ReviewStatus == AdminReviewStatus.Approved && n.IsActive
                && ((n.Skill == skill && n.CefrLevel != cefrLevel) || (n.CefrLevel == cefrLevel && n.Skill != skill)))
            .OrderBy(n => n.CefrLevel).ThenBy(n => n.Skill)
            .Take(MaxCrossLinkCandidates)
            .Select(n => new { n.Id, n.Title })
            .ToListAsync(ct);

        var draft = await _drafting.ProposeBatchAsync(
            new SkillGraphDraftRequest(request.CefrLevel, request.Skill, existingTitles,
                crossLinkNodes.Select(n => n.Title).ToList()),
            ct);

        if (!draft.Success)
            return Ok(new { queued = false, createdCount = 0, error = draft.ErrorMessage });

        if (draft.Nodes.Count == 0)
            return Ok(new { queued = true, createdCount = 0, error = (string?)null });

        // Persist nodes first (title → entity), so PrerequisiteTitles can resolve against both the
        // fresh batch and pre-existing nodes for this CEFR/skill in one lookup.
        var existingByTitle = await _db.SkillGraphNodes
            .Where(n => n.CefrLevel == cefrLevel && n.Skill == skill)
            .ToDictionaryAsync(n => n.Title, StringComparer.OrdinalIgnoreCase, ct);

        var created = new List<SkillGraphNode>();
        var byTitle = new Dictionary<string, SkillGraphNode>(existingByTitle, StringComparer.OrdinalIgnoreCase);

        foreach (var proposal in draft.Nodes)
        {
            if (byTitle.ContainsKey(proposal.Title)) continue; // already exists (or duplicate within batch)

            var key = BuildKey(request.CefrLevel, request.Skill, proposal.Title);
            if (await _db.SkillGraphNodes.AnyAsync(n => n.Key == key, ct)) continue;

            var node = new SkillGraphNode(
                key, proposal.Title, proposal.Description, proposal.CefrLevel, proposal.Skill,
                proposal.Subskill, proposal.DifficultyBand, proposal.DescriptionForAi,
                contextTagsJson: proposal.ContextTags.Count > 0 ? JsonSerializer.Serialize(proposal.ContextTags) : null);
            _db.SkillGraphNodes.Add(node);
            created.Add(node);
            byTitle[proposal.Title] = node;
        }

        await _db.SaveChangesAsync(ct); // assigns Ids before edge resolution

        // Rebuild Phase 2 — only titles actually offered to the AI as cross-link candidates may
        // resolve into a cross-combination edge (never an arbitrary title match elsewhere in the
        // graph) — same "only from the given candidate list" discipline ModuleSkillGraphTaggingService
        // already uses for Module-to-node matches.
        var crossLinkById = await _db.SkillGraphNodes.AsNoTracking()
            .Where(n => crossLinkNodes.Select(c => c.Id).Contains(n.Id))
            .ToDictionaryAsync(n => n.Id, ct);
        foreach (var candidate in crossLinkNodes)
        {
            if (byTitle.ContainsKey(candidate.Title)) continue; // same-combo title takes priority on collision
            if (crossLinkById.TryGetValue(candidate.Id, out var crossLinkNode))
                byTitle[candidate.Title] = crossLinkNode;
        }

        // Resolve prerequisite titles → edges, validate for cycles.
        var candidateEdges = new List<(SkillGraphNode Node, SkillGraphNode Prerequisite)>();
        foreach (var proposal in draft.Nodes)
        {
            if (!byTitle.TryGetValue(proposal.Title, out var node)) continue;
            foreach (var prereqTitle in proposal.PrerequisiteTitles)
            {
                if (byTitle.TryGetValue(prereqTitle, out var prereqNode) && prereqNode.Id != node.Id)
                    candidateEdges.Add((node, prereqNode));
            }
        }

        // Rebuild Phase 2 — validate against the FULL active graph (every node/edge), not just this
        // batch's own candidate set: a cross-combination edge could close a cycle through a node
        // this batch never touched directly, which a batch-scoped validation could miss.
        var allNodeSummaries = await _db.SkillGraphNodes.AsNoTracking()
            .Select(n => new SkillGraphNodeSummary(n.Id, n.Key)).ToListAsync(ct);
        var existingEdges = await _db.SkillGraphPrerequisiteEdges.AsNoTracking()
            .Select(e => new SkillGraphEdgeSummary(e.NodeId, e.PrerequisiteNodeId))
            .ToListAsync(ct);

        var droppedEdgeCount = 0;
        foreach (var (node, prereq) in candidateEdges)
        {
            if (existingEdges.Any(e => e.NodeId == node.Id && e.PrerequisiteNodeId == prereq.Id)) continue; // already linked

            var trialEdges = existingEdges.Append(new SkillGraphEdgeSummary(node.Id, prereq.Id)).ToList();
            var validation = _validation.Validate(allNodeSummaries, trialEdges);

            if (!validation.IsValid)
            {
                droppedEdgeCount++;
                continue; // would introduce a cycle — drop, never applied
            }

            _db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(node.Id, prereq.Id));
            existingEdges.Add(new SkillGraphEdgeSummary(node.Id, prereq.Id));
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new { queued = true, createdCount = created.Count, droppedEdgeCount, error = (string?)null });
    }

    [HttpPost("nodes/batch/approve")]
    public async Task<IActionResult> BatchApprove([FromBody] BatchSkillGraphIdsRequest request, CancellationToken ct)
    {
        var (ids, limitReached) = Bound(request.Ids);
        var nodes = await _db.SkillGraphNodes.Where(n => ids.Contains(n.Id)).ToListAsync(ct);

        var succeeded = 0;
        foreach (var node in nodes)
        {
            node.Approve(GetCurrentUserId());
            succeeded++;
        }
        await _db.SaveChangesAsync(ct);

        return Ok(new { requestedCount = ids.Count, succeeded, failed = ids.Count - succeeded, limitReached });
    }

    [HttpPost("nodes/batch/reject")]
    public async Task<IActionResult> BatchReject([FromBody] BatchSkillGraphRejectRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "Reason is required to reject skill-graph nodes." });

        var (ids, limitReached) = Bound(request.Ids);
        var nodes = await _db.SkillGraphNodes.Where(n => ids.Contains(n.Id)).ToListAsync(ct);

        var succeeded = 0;
        foreach (var node in nodes)
        {
            node.Reject(request.Reason, GetCurrentUserId());
            succeeded++;
        }

        // Editability audit (2026-07-23) — a rejected node is no longer a valid prerequisite/
        // dependent; cascade-remove any edge touching it rather than leaving a dangling reference
        // the graph would otherwise treat as still-live.
        var rejectedIds = nodes.Select(n => n.Id).ToList();
        var danglingEdges = await _db.SkillGraphPrerequisiteEdges
            .Where(e => rejectedIds.Contains(e.NodeId) || rejectedIds.Contains(e.PrerequisiteNodeId))
            .ToListAsync(ct);
        _db.SkillGraphPrerequisiteEdges.RemoveRange(danglingEdges);

        await _db.SaveChangesAsync(ct);

        return Ok(new { requestedCount = ids.Count, succeeded, failed = ids.Count - succeeded, limitReached, edgesRemoved = danglingEdges.Count });
    }

    /// <summary>Coverage matrix: approved+active node count per CEFR level x skill, following the
    /// same pattern as the Delivery Health coverage-gap dashboard (bank-first-admin-backend-surface-
    /// audit.md's established convention) — flags combinations with zero coverage.</summary>
    [HttpGet("coverage")]
    public async Task<IActionResult> GetCoverage(CancellationToken ct)
    {
        var counts = await _db.SkillGraphNodes.AsNoTracking()
            .Where(n => n.ReviewStatus == AdminReviewStatus.Approved && n.IsActive)
            .GroupBy(n => new { n.CefrLevel, n.Skill })
            .Select(g => new { g.Key.CefrLevel, g.Key.Skill, Count = g.Count() })
            .ToListAsync(ct);

        var pendingCounts = await _db.SkillGraphNodes.AsNoTracking()
            .Where(n => n.ReviewStatus == AdminReviewStatus.PendingReview)
            .GroupBy(n => new { n.CefrLevel, n.Skill })
            .Select(g => new { g.Key.CefrLevel, g.Key.Skill, Count = g.Count() })
            .ToListAsync(ct);

        var matrix = new List<object>();
        foreach (var cefrLevel in CefrLevelConstants.All)
        {
            foreach (var skill in CurriculumSkillConstants.All)
            {
                var approvedCount = counts.FirstOrDefault(c => c.CefrLevel == cefrLevel && c.Skill == skill)?.Count ?? 0;
                var pendingCount = pendingCounts.FirstOrDefault(c => c.CefrLevel == cefrLevel && c.Skill == skill)?.Count ?? 0;
                matrix.Add(new { cefrLevel, skill, approvedCount, pendingCount, hasGap = approvedCount == 0 });
            }
        }

        return Ok(new { matrix });
    }

    /// <summary>Sprint 2 — AI re-tags up to <see cref="MaxModulesPerRetagSweep"/> approved Modules
    /// that have no skill-graph coverage links yet: for each, proposes matches against the approved
    /// nodes for that Module's own CEFR level/skill, and auto-applies every validated match (per the
    /// explicit "auto-apply, spot-checked via coverage dashboard" decision — no per-link approval
    /// step). Call again to sweep the next batch of untagged Modules.</summary>
    [HttpPost("retag-modules")]
    public async Task<IActionResult> RetagModules(CancellationToken ct)
    {
        var taggedModuleIds = await _db.ModuleSkillGraphNodeLinks.AsNoTracking()
            .Select(l => l.ModuleId).Distinct().ToListAsync(ct);

        var candidateModules = await _db.Modules.AsNoTracking()
            .Where(m => m.ReviewStatus == AdminReviewStatus.Approved && !m.IsArchived && !taggedModuleIds.Contains(m.Id)
                && m.CefrLevel != null && m.Skill != null)
            .OrderBy(m => m.CreatedAt)
            .Take(MaxModulesPerRetagSweep)
            .ToListAsync(ct);

        var results = new List<object>();
        foreach (var module in candidateModules)
        {
            // Reseed verification (2026-07-23) — found a real, pre-existing bug live: SkillGraphNode.Skill
            // is always stored lower-invariant (see the constructor), but Module.Skill preserves the
            // caller's casing ("Vocabulary"), so this comparison silently matched 0 candidates for
            // every Module whose Skill wasn't already lowercase — confirmed live against real seeded
            // Modules (title "zoo", CefrLevel A1, Skill "Vocabulary") before this fix.
            var moduleSkillLower = module.Skill!.ToLowerInvariant();
            var moduleCefrUpper = module.CefrLevel!.ToUpperInvariant();
            var candidateNodes = await _db.SkillGraphNodes.AsNoTracking()
                .Where(n => n.ReviewStatus == AdminReviewStatus.Approved && n.IsActive
                    && n.CefrLevel == moduleCefrUpper && n.Skill == moduleSkillLower)
                .Select(n => new SkillGraphNodeCandidate(n.Id, n.Key, n.Title))
                .ToListAsync(ct);

            if (candidateNodes.Count == 0)
            {
                results.Add(new { moduleId = module.Id, moduleTitle = module.Title, matchedCount = 0, error = (string?)null });
                continue;
            }

            var tagging = await _tagging.ProposeCoverageAsync(
                new ModuleSkillGraphTaggingRequest(module.Id, module.Title, module.Description ?? "", module.CefrLevel!, module.Skill!, candidateNodes), ct);

            if (!tagging.Success)
            {
                results.Add(new { moduleId = module.Id, moduleTitle = module.Title, matchedCount = 0, error = tagging.ErrorMessage });
                continue;
            }

            foreach (var match in tagging.Matches)
                _db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(module.Id, match.NodeId, match.Confidence));

            results.Add(new { moduleId = module.Id, moduleTitle = module.Title, matchedCount = tagging.Matches.Count, error = (string?)null });
        }

        await _db.SaveChangesAsync(ct);

        // Sprint 14.2 — "Re-tag next batch" previously gave no sense of progress (this sweeps
        // untagged Modules, a different set from whatever nodes the content-coverage table
        // currently shows as gaps, so a report of "0 links applied" looked like nothing happened
        // even when it correctly matched every candidate). Reporting how many untagged Modules
        // remain (re-queried fresh, after this sweep's own links were just saved) makes "next
        // batch" mean something concrete.
        var stillTaggedModuleIds = await _db.ModuleSkillGraphNodeLinks.AsNoTracking()
            .Select(l => l.ModuleId).Distinct().ToListAsync(ct);
        var remainingUntaggedModuleCount = await _db.Modules.AsNoTracking()
            .Where(m => m.ReviewStatus == AdminReviewStatus.Approved && !m.IsArchived
                && m.CefrLevel != null && m.Skill != null && !stillTaggedModuleIds.Contains(m.Id))
            .CountAsync(ct);

        return Ok(new { sweptCount = candidateModules.Count, results, remainingUntaggedModuleCount });
    }

    /// <summary>Sprint 2 — per-node content coverage: how many approved Modules are actually linked
    /// to each approved node. Distinct from <see cref="GetCoverage"/> (which counts nodes
    /// themselves, not content linked to them) — this is the gap that matters once the graph is
    /// approved: an approved node with zero linked Modules has no real content behind it yet.</summary>
    [HttpGet("content-coverage")]
    public async Task<IActionResult> GetContentCoverage(CancellationToken ct)
    {
        // Sprint 14.2 — previously only returned the gap list (nodes with zero linked Modules),
        // which made the admin table a dead end (no way to browse the nodes that DO have
        // content, no module titles/links, no tags). Now returns every approved node with its
        // real linked-Module list, so the frontend can render one paginated, row-clickable table
        // instead of a bare gap dump.
        var links = await _db.ModuleSkillGraphNodeLinks.AsNoTracking()
            .Join(_db.Modules.AsNoTracking(), l => l.ModuleId, m => m.Id, (l, m) => new { l.SkillGraphNodeId, m.Id, m.Title })
            .ToListAsync(ct);
        var linksByNode = links.GroupBy(l => l.SkillGraphNodeId).ToDictionary(g => g.Key, g => g.ToList());

        var rawNodes = await _db.SkillGraphNodes.AsNoTracking()
            .Where(n => n.ReviewStatus == AdminReviewStatus.Approved && n.IsActive)
            .Select(n => new { n.Id, n.Key, n.Title, n.CefrLevel, n.Skill, n.ContextTagsJson, n.FocusTagsJson })
            .ToListAsync(ct);

        var nodes = rawNodes.Select(n =>
        {
            var linkedModules = linksByNode.TryGetValue(n.Id, out var l)
                ? l.Select(x => new { x.Id, x.Title }).ToList()
                : [];
            return new
            {
                n.Id, n.Key, n.Title, n.CefrLevel, n.Skill,
                ContextTags = ParseTags(n.ContextTagsJson), FocusTags = ParseTags(n.FocusTagsJson),
                LinkedModuleCount = linkedModules.Count,
                LinkedModules = linkedModules,
            };
        }).ToList();

        var nodesWithoutContentCount = nodes.Count(n => n.LinkedModuleCount == 0);

        return Ok(new
        {
            totalApprovedNodes = nodes.Count,
            nodesWithContent = nodes.Count - nodesWithoutContentCount,
            nodesWithoutContentCount,
            nodes,
        });
    }

    private static (List<Guid> Ids, bool LimitReached) Bound(IReadOnlyList<Guid> ids)
    {
        var distinct = ids.Distinct().ToList();
        return distinct.Count > MaxBatchSize
            ? (distinct.Take(MaxBatchSize).ToList(), true)
            : (distinct, false);
    }

    private static string BuildKey(string cefrLevel, string skill, string title)
    {
        var slug = new string(title.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray());
        while (slug.Contains("__")) slug = slug.Replace("__", "_");
        slug = slug.Trim('_');
        return $"{skill}.{slug}.{cefrLevel.ToLowerInvariant()}";
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

public sealed record DraftSkillGraphRequest(string CefrLevel, string Skill);
public sealed record BatchSkillGraphIdsRequest(List<Guid> Ids);
public sealed record BatchSkillGraphRejectRequest(List<Guid> Ids, string Reason);

public sealed record CreateSkillGraphNodeRequest(
    string Title, string Description, string CefrLevel, string Skill, string? Subskill,
    int DifficultyBand, string? DescriptionForAi, List<string>? ContextTags, List<string>? FocusTags,
    /// <summary>Create node UX audit (2026-07-23) — optional prerequisites chosen at creation time,
    /// so an admin places a new node in the graph in the same step as authoring it, instead of a
    /// create-then-separately-link two-step. Each id is cycle-validated against the full active
    /// graph the same way <see cref="AddSkillGraphPrerequisiteRequest"/> already is.</summary>
    List<Guid>? PrerequisiteNodeIds = null,
    /// <summary>Editability follow-up (2026-07-23) — the symmetric direction: existing nodes that
    /// this new node should become a prerequisite FOR (a node can be the prerequisite for several
    /// others, and can itself have several prerequisites — a genuine many-to-many both ways).</summary>
    List<Guid>? DependentNodeIds = null);

public sealed record UpdateSkillGraphNodeRequest(
    string Title, string Description, string CefrLevel, string Skill, string? Subskill,
    int DifficultyBand, string? DescriptionForAi);

public sealed record AddSkillGraphPrerequisiteRequest(Guid PrerequisiteNodeId);

public sealed record ImportSkillGraphNodeItem(
    string Key, string Title, string Description, string CefrLevel, string Skill, string? Subskill,
    int DifficultyBand, string? DescriptionForAi,
    List<string> ContextTags, List<string> FocusTags, List<string> PrerequisiteKeys);

public sealed record ImportSkillGraphRequest(List<ImportSkillGraphNodeItem> Nodes);
