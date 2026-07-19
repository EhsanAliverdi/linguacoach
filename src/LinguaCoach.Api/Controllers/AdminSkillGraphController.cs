using System.Security.Claims;
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

    public AdminSkillGraphController(
        LinguaCoachDbContext db, ISkillGraphDraftingService drafting, ISkillGraphValidationService validation,
        IModuleSkillGraphTaggingService tagging)
    {
        _db = db;
        _drafting = drafting;
        _validation = validation;
        _tagging = tagging;
    }

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
        var items = await query
            .OrderBy(n => n.CefrLevel).ThenBy(n => n.Skill).ThenBy(n => n.Title)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new
            {
                n.Id, n.Key, n.Title, n.Description, n.CefrLevel, n.Skill, n.Subskill,
                n.DifficultyBand, n.ReviewStatus, n.IsActive, n.RejectionReason, n.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new
        {
            items,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            page, pageSize,
        });
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

        return Ok(new
        {
            node.Id, node.Key, node.Title, node.Description, node.CefrLevel, node.Skill, node.Subskill,
            node.DifficultyBand, node.DescriptionForAi, node.ReviewStatus, node.IsActive,
            node.RejectionReason, node.ReviewedByUserId, node.ApprovedAtUtc, node.RejectedAtUtc,
            prerequisites,
        });
    }

    /// <summary>Triggers one bounded AI-drafting call for a single CEFR level x skill combination,
    /// persists every proposal as a PendingReview node, then resolves PrerequisiteTitles into real
    /// edges — an edge is only created when both ends resolve to a real node (within this batch or
    /// an existing one for the same CEFR/skill) and the resulting edge set has no cycle; edges that
    /// would introduce a cycle are dropped and reported, never silently applied.</summary>
    [HttpPost("draft")]
    public async Task<IActionResult> Draft([FromBody] DraftSkillGraphRequest request, CancellationToken ct)
    {
        if (!CefrLevelConstants.IsValid(request.CefrLevel))
            return BadRequest(new { error = $"Invalid CEFR level '{request.CefrLevel}'." });
        if (!CurriculumSkillConstants.IsValid(request.Skill))
            return BadRequest(new { error = $"Invalid skill '{request.Skill}'." });

        var existingTitles = await _db.SkillGraphNodes.AsNoTracking()
            .Where(n => n.CefrLevel == request.CefrLevel.ToUpperInvariant() && n.Skill == request.Skill.ToLowerInvariant())
            .Select(n => n.Title)
            .ToListAsync(ct);

        var draft = await _drafting.ProposeBatchAsync(
            new SkillGraphDraftRequest(request.CefrLevel, request.Skill, existingTitles), ct);

        if (!draft.Success)
            return Ok(new { queued = false, createdCount = 0, error = draft.ErrorMessage });

        if (draft.Nodes.Count == 0)
            return Ok(new { queued = true, createdCount = 0, error = (string?)null });

        // Persist nodes first (title → entity), so PrerequisiteTitles can resolve against both the
        // fresh batch and pre-existing nodes for this CEFR/skill in one lookup.
        var existingByTitle = await _db.SkillGraphNodes
            .Where(n => n.CefrLevel == request.CefrLevel.ToUpperInvariant() && n.Skill == request.Skill.ToLowerInvariant())
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
                proposal.Subskill, proposal.DifficultyBand, proposal.DescriptionForAi);
            _db.SkillGraphNodes.Add(node);
            created.Add(node);
            byTitle[proposal.Title] = node;
        }

        await _db.SaveChangesAsync(ct); // assigns Ids before edge resolution

        // Resolve prerequisite titles → edges, validate for cycles against the full candidate set
        // (existing + newly created), drop any edge that would introduce a cycle.
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

        var allNodesInScope = byTitle.Values.DistinctBy(n => n.Id).ToList();
        var existingEdges = await _db.SkillGraphPrerequisiteEdges.AsNoTracking()
            .Where(e => allNodesInScope.Select(n => n.Id).Contains(e.NodeId))
            .Select(e => new SkillGraphEdgeSummary(e.NodeId, e.PrerequisiteNodeId))
            .ToListAsync(ct);

        var droppedEdgeCount = 0;
        foreach (var (node, prereq) in candidateEdges)
        {
            var trialEdges = existingEdges.Append(new SkillGraphEdgeSummary(node.Id, prereq.Id)).ToList();
            var validation = _validation.Validate(
                allNodesInScope.Select(n => new SkillGraphNodeSummary(n.Id, n.Key)).ToList(), trialEdges);

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
        await _db.SaveChangesAsync(ct);

        return Ok(new { requestedCount = ids.Count, succeeded, failed = ids.Count - succeeded, limitReached });
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
            var candidateNodes = await _db.SkillGraphNodes.AsNoTracking()
                .Where(n => n.ReviewStatus == AdminReviewStatus.Approved && n.IsActive
                    && n.CefrLevel == module.CefrLevel && n.Skill == module.Skill)
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

        return Ok(new { sweptCount = candidateModules.Count, results });
    }

    /// <summary>Sprint 2 — per-node content coverage: how many approved Modules are actually linked
    /// to each approved node. Distinct from <see cref="GetCoverage"/> (which counts nodes
    /// themselves, not content linked to them) — this is the gap that matters once the graph is
    /// approved: an approved node with zero linked Modules has no real content behind it yet.</summary>
    [HttpGet("content-coverage")]
    public async Task<IActionResult> GetContentCoverage(CancellationToken ct)
    {
        var linkedCounts = await _db.ModuleSkillGraphNodeLinks.AsNoTracking()
            .GroupBy(l => l.SkillGraphNodeId)
            .Select(g => new { NodeId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var linkedCountsByNode = linkedCounts.ToDictionary(c => c.NodeId, c => c.Count);

        var approvedNodes = await _db.SkillGraphNodes.AsNoTracking()
            .Where(n => n.ReviewStatus == AdminReviewStatus.Approved && n.IsActive)
            .Select(n => new { n.Id, n.Key, n.Title, n.CefrLevel, n.Skill })
            .ToListAsync(ct);

        var nodesWithoutContent = approvedNodes
            .Where(n => !linkedCountsByNode.ContainsKey(n.Id))
            .Select(n => new { n.Id, n.Key, n.Title, n.CefrLevel, n.Skill })
            .ToList();

        return Ok(new
        {
            totalApprovedNodes = approvedNodes.Count,
            nodesWithContent = approvedNodes.Count - nodesWithoutContent.Count,
            nodesWithoutContentCount = nodesWithoutContent.Count,
            nodesWithoutContent,
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
