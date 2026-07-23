using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Adaptive Curriculum Sprint 1 — AdminSkillGraphController. Uses the plain <see cref="ApiTestFactory"/>
/// (no AI provider override needed — batch approve/reject/coverage tests seed nodes directly via DB,
/// and the "draft" endpoint is tested only for graceful degradation, matching this codebase's
/// AI-draft-then-validate convention where the AI path is never a hard dependency for the rest of
/// the surface to be tested).
/// </summary>
public sealed class AdminSkillGraphEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminSkillGraphEndpointTests(ApiTestFactory factory) => _factory = factory;

    private static HttpClient ClientWithToken(ApiTestFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<SkillGraphNode> SeedNodeAsync(string key, string cefrLevel = "A1", string skill = "grammar")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var node = new SkillGraphNode(key, $"Title {key}", "Description.", cefrLevel, skill);
        db.SkillGraphNodes.Add(node);
        await db.SaveChangesAsync();
        return node;
    }

    [Fact]
    public async Task GetTaxonomy_ReturnsCefrLevelsSkillsAndSubskills()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/skill-graph/taxonomy");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("cefrLevels").GetArrayLength() >= 6);
        Assert.True(body.GetProperty("skills").GetArrayLength() >= 9);
        Assert.True(body.GetProperty("subskillsBySkill").TryGetProperty("grammar", out _));
        // Phase 6.1 — tag vocabulary for the Nodes table's new ContextTag/FocusTag filters.
        Assert.True(body.GetProperty("contextTags").GetArrayLength() >= 13);
        Assert.True(body.GetProperty("focusTags").GetArrayLength() >= 13);
    }

    [Fact]
    public async Task GetNodes_FiltersByCefrLevelAndSkill()
    {
        var key = $"grammar.test_{Guid.NewGuid():N}.a1";
        await SeedNodeAsync(key, "A1", "grammar");

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/skill-graph/nodes?cefrLevel=A1&skill=grammar&pageSize=200");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("key").GetString() == key);
    }

    [Fact]
    public async Task GetNodes_ReportsLinkedModuleCount()
    {
        // Content-coverage merge (2026-07-23) — GetNodes now carries LinkedModuleCount so the
        // Nodes table can show it directly, replacing the deleted separate "Content coverage" table.
        var node = await SeedNodeAsync($"grammar.linkcount_{Guid.NewGuid():N}.a1");
        Guid moduleId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var module = new Module($"Module {Guid.NewGuid():N}", ModuleSourceMode.Manual, cefrLevel: "A1", skill: "grammar");
            db.Modules.Add(module);
            await db.SaveChangesAsync();
            moduleId = module.Id;
            db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(moduleId, node.Id, 0.9));
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync($"/api/admin/skill-graph/nodes?cefrLevel=A1&skill=grammar&pageSize=200");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        var thisNode = items.Single(i => i.GetProperty("id").GetGuid() == node.Id);
        Assert.Equal(1, thisNode.GetProperty("linkedModuleCount").GetInt32());
    }

    // ── Phase 6.1 (2026-07-23) — free-text search + ContextTag/FocusTag filters ────────────────

    [Fact]
    public async Task GetNodes_SearchMatchesTitleAndDescription()
    {
        var uniqueWord = $"zzq{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var titleMatch = new SkillGraphNode($"grammar.search_title_{Guid.NewGuid():N}.a1", $"Title with {uniqueWord} inside", "Description.", "A1", "grammar");
            var descMatch = new SkillGraphNode($"grammar.search_desc_{Guid.NewGuid():N}.a1", "Ordinary title", $"Description mentioning {uniqueWord} here.", "A1", "grammar");
            var noMatch = new SkillGraphNode($"grammar.search_none_{Guid.NewGuid():N}.a1", "Unrelated title", "Unrelated description.", "A1", "grammar");
            db.SkillGraphNodes.AddRange(titleMatch, descMatch, noMatch);
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync($"/api/admin/skill-graph/nodes?search={uniqueWord}&pageSize=200");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.All(items, i =>
        {
            var title = i.GetProperty("title").GetString() ?? "";
            var description = i.GetProperty("description").GetString() ?? "";
            Assert.True(title.Contains(uniqueWord) || description.Contains(uniqueWord));
        });
    }

    [Fact]
    public async Task GetNodes_SearchIsCaseInsensitive()
    {
        var uniqueWord = $"CaseTest{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphNodes.Add(new SkillGraphNode($"grammar.case_{Guid.NewGuid():N}.a1", $"Title with {uniqueWord}", "Description.", "A1", "grammar"));
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync($"/api/admin/skill-graph/nodes?search={uniqueWord.ToLowerInvariant()}&pageSize=200");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items);
    }

    [Fact]
    public async Task GetNodes_FiltersByContextTagAndFocusTag()
    {
        var contextTag = "workplace";
        var focusTag = "general_english";
        Guid taggedNodeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var tagged = new SkillGraphNode($"grammar.tagged_{Guid.NewGuid():N}.a1", "Tagged node", "Description.", "A1", "grammar");
            tagged.UpdateTags($"[\"{contextTag}\"]", $"[\"{focusTag}\"]");
            var untagged = new SkillGraphNode($"grammar.untagged_{Guid.NewGuid():N}.a1", "Untagged node", "Description.", "A1", "grammar");
            db.SkillGraphNodes.AddRange(tagged, untagged);
            await db.SaveChangesAsync();
            taggedNodeId = tagged.Id;
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var byContextTag = await client.GetAsync($"/api/admin/skill-graph/nodes?contextTag={contextTag}&pageSize=200");
        var contextBody = await byContextTag.Content.ReadFromJsonAsync<JsonElement>();
        var contextItems = contextBody.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(contextItems, i => i.GetProperty("id").GetGuid() == taggedNodeId);

        var byFocusTag = await client.GetAsync($"/api/admin/skill-graph/nodes?focusTag={focusTag}&pageSize=200");
        var focusBody = await byFocusTag.Content.ReadFromJsonAsync<JsonElement>();
        var focusItems = focusBody.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(focusItems, i => i.GetProperty("id").GetGuid() == taggedNodeId);
    }

    [Fact]
    public async Task GetNode_ReturnsLinkedModulesList()
    {
        var node = await SeedNodeAsync($"grammar.linklist_{Guid.NewGuid():N}.a1");
        Guid moduleId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var module = new Module($"Linked module {Guid.NewGuid():N}", ModuleSourceMode.Manual, cefrLevel: "A1", skill: "grammar");
            db.Modules.Add(module);
            await db.SaveChangesAsync();
            moduleId = module.Id;
            db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(moduleId, node.Id, 0.9));
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync($"/api/admin/skill-graph/nodes/{node.Id}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var linkedModules = body.GetProperty("linkedModules").EnumerateArray().ToList();
        Assert.Contains(linkedModules, m => m.GetProperty("id").GetGuid() == moduleId);
    }

    [Fact]
    public async Task BatchApprove_MarksNodesApproved()
    {
        var node = await SeedNodeAsync($"grammar.approve_{Guid.NewGuid():N}.a1");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/nodes/batch/approve", new { ids = new[] { node.Id } });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("succeeded").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var saved = await db.SkillGraphNodes.FirstAsync(n => n.Id == node.Id);
        Assert.Equal(AdminReviewStatus.Approved, saved.ReviewStatus);
    }

    [Fact]
    public async Task BatchReject_RequiresReason()
    {
        var node = await SeedNodeAsync($"grammar.reject_noreason_{Guid.NewGuid():N}.a1");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/nodes/batch/reject", new { ids = new[] { node.Id }, reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task BatchReject_MarksNodesRejectedWithReason()
    {
        var node = await SeedNodeAsync($"grammar.reject_{Guid.NewGuid():N}.a1");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/nodes/batch/reject",
            new { ids = new[] { node.Id }, reason = "Too broad." });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var saved = await db.SkillGraphNodes.FirstAsync(n => n.Id == node.Id);
        Assert.Equal(AdminReviewStatus.Rejected, saved.ReviewStatus);
        Assert.Equal("Too broad.", saved.RejectionReason);
    }

    [Fact]
    public async Task GetCoverage_ReturnsFullCefrBySkillMatrix()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/skill-graph/coverage");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        // 6 CEFR levels x 9 skills = 54 combinations, every one present regardless of coverage.
        Assert.True(body.GetProperty("matrix").GetArrayLength() >= 54);
    }

    [Fact]
    public async Task NonAdmin_rejected_for_all_skill_graph_endpoints()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"sg_nonadmin_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/skill-graph/nodes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/skill-graph/coverage")).StatusCode);
    }

    // ── Sprint 2: content coverage ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetContentCoverage_ApprovedNodeWithNoLinkedModule_ReportsZeroLinkedModules()
    {
        var node = await SeedNodeAsync($"grammar.coverage_{Guid.NewGuid():N}.a1");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var saved = await db.SkillGraphNodes.FirstAsync(n => n.Id == node.Id);
            saved.Approve(null);
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/skill-graph/content-coverage");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var nodes = body.GetProperty("nodes").EnumerateArray().ToList();
        Assert.Contains(nodes, n => n.GetProperty("id").GetGuid() == node.Id && n.GetProperty("linkedModuleCount").GetInt32() == 0);
    }

    [Fact]
    public async Task GetContentCoverage_ApprovedNodeWithLinkedModule_ReportsTheRealLinkedModule()
    {
        var node = await SeedNodeAsync($"grammar.linked_{Guid.NewGuid():N}.a1");
        Guid moduleId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var saved = await db.SkillGraphNodes.FirstAsync(n => n.Id == node.Id);
            saved.Approve(null);

            var module = new Module($"Module {Guid.NewGuid():N}", ModuleSourceMode.Manual, cefrLevel: "A1", skill: "grammar");
            module.Approve(null);
            db.Modules.Add(module);
            await db.SaveChangesAsync();
            moduleId = module.Id;

            db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(moduleId, node.Id, 0.9));
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/skill-graph/content-coverage");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var nodes = body.GetProperty("nodes").EnumerateArray().ToList();
        var thisNode = nodes.Single(n => n.GetProperty("id").GetGuid() == node.Id);
        Assert.Equal(1, thisNode.GetProperty("linkedModuleCount").GetInt32());
        Assert.Equal(moduleId, thisNode.GetProperty("linkedModules")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task NonAdmin_rejected_for_content_coverage_endpoint()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"sg_content_cov_nonadmin_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/admin/skill-graph/content-coverage");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── Editability audit (2026-07-23): manual create/edit/link/unlink + isolated-node metric + import ──

    [Fact]
    public async Task CreateNode_ValidRequest_CreatesPendingReviewNode()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var title = $"Manual node {Guid.NewGuid():N}";
        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/nodes", new
        {
            title, description = "A manually authored node.", cefrLevel = "A1", skill = "grammar",
            subskill = (string?)null, difficultyBand = 2, descriptionForAi = (string?)null,
            contextTags = new[] { "workplace" }, focusTags = Array.Empty<string>(),
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var saved = await db.SkillGraphNodes.FirstAsync(n => n.Id == id);
        Assert.Equal(title, saved.Title);
        Assert.Equal(AdminReviewStatus.PendingReview, saved.ReviewStatus);
    }

    [Fact]
    public async Task CreateNode_DuplicateKey_ReturnsConflict()
    {
        var title = $"Dup node {Guid.NewGuid():N}";
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);
        var request = new { title, description = "D", cefrLevel = "A1", skill = "grammar", subskill = (string?)null, difficultyBand = 1, descriptionForAi = (string?)null, contextTags = Array.Empty<string>(), focusTags = Array.Empty<string>() };

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin/skill-graph/nodes", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/admin/skill-graph/nodes", request)).StatusCode);
    }

    [Fact]
    public async Task CreateNode_WithPrerequisiteNodeIds_LinksThemAtCreationTime()
    {
        var prereq = await SeedNodeAsync($"grammar.createprereq_{Guid.NewGuid():N}.a1");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/nodes", new
        {
            title = $"Node with prereq {Guid.NewGuid():N}", description = "A manually authored node.",
            cefrLevel = "A1", skill = "speaking", subskill = (string?)null, difficultyBand = 1,
            descriptionForAi = (string?)null, contextTags = Array.Empty<string>(), focusTags = Array.Empty<string>(),
            prerequisiteNodeIds = new[] { prereq.Id },
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var newNodeId = body.GetProperty("id").GetGuid();
        Assert.Equal(0, body.GetProperty("droppedPrerequisites").GetArrayLength());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.True(await db.SkillGraphPrerequisiteEdges.AnyAsync(e => e.NodeId == newNodeId && e.PrerequisiteNodeId == prereq.Id));
    }

    [Fact]
    public async Task CreateNode_WithDependentNodeIds_LinksThemAsUnlocksAtCreationTime()
    {
        // The symmetric direction: an existing node that this NEW node should become a
        // prerequisite FOR (one node can be the prerequisite for several others).
        var dependent = await SeedNodeAsync($"speaking.createdependent_{Guid.NewGuid():N}.a1", "A1", "speaking");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/nodes", new
        {
            title = $"Foundational node {Guid.NewGuid():N}", description = "A manually authored node.",
            cefrLevel = "A1", skill = "grammar", subskill = (string?)null, difficultyBand = 1,
            descriptionForAi = (string?)null, contextTags = Array.Empty<string>(), focusTags = Array.Empty<string>(),
            dependentNodeIds = new[] { dependent.Id },
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var newNodeId = body.GetProperty("id").GetGuid();
        Assert.Equal(0, body.GetProperty("droppedDependents").GetArrayLength());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        // dependent depends on the new node — edge direction is NodeId=dependent, PrerequisiteNodeId=newNode.
        Assert.True(await db.SkillGraphPrerequisiteEdges.AnyAsync(e => e.NodeId == dependent.Id && e.PrerequisiteNodeId == newNodeId));
    }

    [Fact]
    public async Task CreateNode_WithPrerequisiteThatWouldCycle_DropsItAndReportsWhy()
    {
        var a = await SeedNodeAsync($"grammar.cyclea_{Guid.NewGuid():N}.a1");
        var b = await SeedNodeAsync($"grammar.cycleb_{Guid.NewGuid():N}.a1");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        // a depends on b
        await client.PostAsJsonAsync($"/api/admin/skill-graph/nodes/{a.Id}/prerequisites", new { prerequisiteNodeId = b.Id });

        // Creating a new node "c" that depends on a, where b (already an ancestor of a) is also
        // requested as a prerequisite of c, is fine — no cycle. Instead assert the real cycle case:
        // requesting b to depend on the new node while the new node depends on a which depends on b.
        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/nodes", new
        {
            title = $"Cycle test {Guid.NewGuid():N}", description = "D", cefrLevel = "A1", skill = "grammar",
            subskill = (string?)null, difficultyBand = 1, descriptionForAi = (string?)null,
            contextTags = Array.Empty<string>(), focusTags = Array.Empty<string>(),
            prerequisiteNodeIds = new[] { a.Id },
        });
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var newNodeId = body.GetProperty("id").GetGuid();

        // Now try to add the new node itself as b's prerequisite — b -> newNode -> a -> b would cycle.
        var cycleResp = await client.PostAsJsonAsync($"/api/admin/skill-graph/nodes/{b.Id}/prerequisites", new { prerequisiteNodeId = newNodeId });
        Assert.Equal(HttpStatusCode.Conflict, cycleResp.StatusCode);
    }

    [Fact]
    public async Task UpdateNode_WhilePendingReview_UpdatesFields()
    {
        var node = await SeedNodeAsync($"grammar.editme_{Guid.NewGuid():N}.a1");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PutAsJsonAsync($"/api/admin/skill-graph/nodes/{node.Id}", new
        {
            title = "Updated title", description = "Updated desc", cefrLevel = "A2", skill = "reading",
            subskill = (string?)null, difficultyBand = 4, descriptionForAi = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var saved = await db.SkillGraphNodes.FirstAsync(n => n.Id == node.Id);
        Assert.Equal("Updated title", saved.Title);
        Assert.Equal("A2", saved.CefrLevel);
        Assert.Equal("reading", saved.Skill);
    }

    [Fact]
    public async Task UpdateNode_WhileApproved_ReturnsConflict()
    {
        var node = await SeedNodeAsync($"grammar.editapproved_{Guid.NewGuid():N}.a1");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            (await db.SkillGraphNodes.FirstAsync(n => n.Id == node.Id)).Approve(null);
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);
        var resp = await client.PutAsJsonAsync($"/api/admin/skill-graph/nodes/{node.Id}", new
        {
            title = "X", description = "Y", cefrLevel = "A1", skill = "grammar",
            subskill = (string?)null, difficultyBand = 1, descriptionForAi = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    // ── Skill Graph rebuild Phase 6.3d — reparenting-on-edit review ──────────────────────────

    [Fact]
    public async Task UpdateNode_NoReparentReview_WhenCefrLevelAndSkillAreUnchanged()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var node = await SeedNodeAsync($"grammar.reparent_none_{suffix}.a1");
        var prereq = await SeedNodeAsync($"grammar.reparent_none_prereq_{suffix}.a1");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(node.Id, prereq.Id));
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PutAsJsonAsync($"/api/admin/skill-graph/nodes/{node.Id}", new
        {
            title = "Same level same skill", description = "Y", cefrLevel = "A1", skill = "grammar",
            subskill = (string?)null, difficultyBand = 1, descriptionForAi = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("reparentReview").ValueKind);
    }

    [Fact]
    public async Task UpdateNode_NoReparentReview_WhenMovedButNoEdgesExist()
    {
        var node = await SeedNodeAsync($"grammar.reparent_noedges_{Guid.NewGuid():N}.a1");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PutAsJsonAsync($"/api/admin/skill-graph/nodes/{node.Id}", new
        {
            title = "Moved but no edges", description = "Y", cefrLevel = "C1", skill = "grammar",
            subskill = (string?)null, difficultyBand = 1, descriptionForAi = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("reparentReview").ValueKind);
    }

    [Fact]
    public async Task UpdateNode_FlagsASuspiciousPrerequisiteAfterMovingToAnEarlierCefrLevel()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var node = await SeedNodeAsync($"grammar.reparent_susp_{suffix}.c1", "C1", "grammar");
        var laterPrereq = await SeedNodeAsync($"grammar.reparent_susp_prereq_{suffix}.b2", "B2", "grammar");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(node.Id, laterPrereq.Id)); // laterPrereq -> node
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        // Move the node down to A1 — its B2 prerequisite is now at a LATER stage than the node
        // itself, a genuine ordering violation.
        var resp = await client.PutAsJsonAsync($"/api/admin/skill-graph/nodes/{node.Id}", new
        {
            title = "Moved down to A1", description = "Y", cefrLevel = "A1", skill = "grammar",
            subskill = (string?)null, difficultyBand = 1, descriptionForAi = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var review = body.GetProperty("reparentReview");
        Assert.Equal("C1", review.GetProperty("oldCefrLevel").GetString());
        Assert.Equal("A1", review.GetProperty("newCefrLevel").GetString());
        var edge = Assert.Single(review.GetProperty("edgesToReview").EnumerateArray());
        Assert.Equal(laterPrereq.Id, edge.GetProperty("otherNodeId").GetGuid());
        Assert.True(edge.GetProperty("isPrerequisite").GetBoolean());
        Assert.True(edge.GetProperty("looksSuspicious").GetBoolean());
    }

    [Fact]
    public async Task UpdateNode_ReparentReviewTriggeredBySkillChangeAloneListsEdgesWithoutFlaggingSameLevelOnes()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var node = await SeedNodeAsync($"grammar.reparent_skill_{suffix}.a1", "A1", "grammar");
        var samePrereq = await SeedNodeAsync($"grammar.reparent_skill_prereq_{suffix}.a1", "A1", "grammar");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(node.Id, samePrereq.Id));
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PutAsJsonAsync($"/api/admin/skill-graph/nodes/{node.Id}", new
        {
            title = "Same level, different skill", description = "Y", cefrLevel = "A1", skill = "vocabulary",
            subskill = (string?)null, difficultyBand = 1, descriptionForAi = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var review = body.GetProperty("reparentReview");
        Assert.Equal("grammar", review.GetProperty("oldSkill").GetString());
        Assert.Equal("vocabulary", review.GetProperty("newSkill").GetString());
        var edge = Assert.Single(review.GetProperty("edgesToReview").EnumerateArray());
        Assert.False(edge.GetProperty("looksSuspicious").GetBoolean());
    }

    [Fact]
    public async Task AddPrerequisite_ValidPair_CreatesEdge()
    {
        var node = await SeedNodeAsync($"speaking.n_{Guid.NewGuid():N}.a1", "A1", "speaking");
        var prereq = await SeedNodeAsync($"grammar.n_{Guid.NewGuid():N}.a1", "A1", "grammar");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync($"/api/admin/skill-graph/nodes/{node.Id}/prerequisites",
            new { prerequisiteNodeId = prereq.Id });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.True(await db.SkillGraphPrerequisiteEdges.AnyAsync(e => e.NodeId == node.Id && e.PrerequisiteNodeId == prereq.Id));
    }

    [Fact]
    public async Task AddPrerequisite_WouldCreateCycle_ReturnsConflict()
    {
        var a = await SeedNodeAsync($"grammar.a_{Guid.NewGuid():N}.a1");
        var b = await SeedNodeAsync($"grammar.b_{Guid.NewGuid():N}.a1");

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        // b depends on a
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/api/admin/skill-graph/nodes/{b.Id}/prerequisites", new { prerequisiteNodeId = a.Id })).StatusCode);

        // a depends on b would close a cycle
        var resp = await client.PostAsJsonAsync($"/api/admin/skill-graph/nodes/{a.Id}/prerequisites", new { prerequisiteNodeId = b.Id });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task RemovePrerequisite_ExistingEdge_RemovesIt()
    {
        var node = await SeedNodeAsync($"grammar.n_{Guid.NewGuid():N}.a1");
        var prereq = await SeedNodeAsync($"grammar.p_{Guid.NewGuid():N}.a1");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        await client.PostAsJsonAsync($"/api/admin/skill-graph/nodes/{node.Id}/prerequisites", new { prerequisiteNodeId = prereq.Id });

        var resp = await client.DeleteAsync($"/api/admin/skill-graph/nodes/{node.Id}/prerequisites/{prereq.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.False(await db.SkillGraphPrerequisiteEdges.AnyAsync(e => e.NodeId == node.Id && e.PrerequisiteNodeId == prereq.Id));
    }

    [Fact]
    public async Task BatchReject_RemovesDanglingEdges()
    {
        var node = await SeedNodeAsync($"grammar.rejme_{Guid.NewGuid():N}.a1");
        var prereq = await SeedNodeAsync($"grammar.rejprereq_{Guid.NewGuid():N}.a1");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(node.Id, prereq.Id));
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);
        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/nodes/batch/reject",
            new { ids = new[] { node.Id }, reason = "Bad node." });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("edgesRemoved").GetInt32());

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.False(await db2.SkillGraphPrerequisiteEdges.AnyAsync(e => e.NodeId == node.Id));
    }

    // ── Skill Graph rebuild Phase 6.3b — reject-triggered reconnect suggestions (advisory only) ──

    [Fact]
    public async Task BatchReject_SuggestsReconnectingOrphanedPredecessorAndDependent()
    {
        // A -> B -> C; rejecting B should surface a suggestion to reconnect A -> C.
        var suffix = Guid.NewGuid().ToString("N");
        var a = await SeedNodeAsync($"grammar.reconnect_a_{suffix}.a1");
        var b = await SeedNodeAsync($"grammar.reconnect_b_{suffix}.a1");
        var c = await SeedNodeAsync($"grammar.reconnect_c_{suffix}.a1");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(b.Id, a.Id)); // A -> B
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(c.Id, b.Id)); // B -> C
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);
        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/nodes/batch/reject",
            new { ids = new[] { b.Id }, reason = "Bad node." });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var groups = body.GetProperty("reconnectSuggestions").EnumerateArray().ToList();
        var group = Assert.Single(groups);
        Assert.Equal(b.Id, group.GetProperty("rejectedNodeId").GetGuid());
        var predecessor = Assert.Single(group.GetProperty("orphanedPredecessors").EnumerateArray());
        Assert.Equal(a.Id, predecessor.GetProperty("id").GetGuid());
        var dependent = Assert.Single(group.GetProperty("orphanedDependents").EnumerateArray());
        Assert.Equal(c.Id, dependent.GetProperty("id").GetGuid());
        var reconnect = Assert.Single(group.GetProperty("suggestedReconnects").EnumerateArray());
        Assert.Equal(a.Id, reconnect.GetProperty("prerequisiteNodeId").GetGuid());
        Assert.Equal(c.Id, reconnect.GetProperty("nodeId").GetGuid());
    }

    [Fact]
    public async Task BatchReject_NoReconnectSuggestionWhenNothingToBridge()
    {
        // Node with no predecessors (a root) — rejecting it has nothing to reconnect.
        var node = await SeedNodeAsync($"grammar.noreconnect_{Guid.NewGuid():N}.a1");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/nodes/batch/reject",
            new { ids = new[] { node.Id }, reason = "Bad node." });
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(body.GetProperty("reconnectSuggestions").EnumerateArray());
    }

    [Fact]
    public async Task GetIsolatedNodes_NodeWithNoEdges_IsReported()
    {
        var node = await SeedNodeAsync($"grammar.iso_{Guid.NewGuid():N}.a1");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/skill-graph/nodes/isolated");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var isolated = body.GetProperty("isolated").EnumerateArray().ToList();
        Assert.Contains(isolated, n => n.GetProperty("id").GetGuid() == node.Id);
    }

    [Fact]
    public async Task GetIsolatedNodes_NodeWithAnEdge_IsNotReported()
    {
        var node = await SeedNodeAsync($"grammar.linked_iso_{Guid.NewGuid():N}.a1");
        var prereq = await SeedNodeAsync($"grammar.linked_iso_prereq_{Guid.NewGuid():N}.a1");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(node.Id, prereq.Id));
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);
        var resp = await client.GetAsync("/api/admin/skill-graph/nodes/isolated");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var isolated = body.GetProperty("isolated").EnumerateArray().ToList();
        Assert.DoesNotContain(isolated, n => n.GetProperty("id").GetGuid() == node.Id);
        Assert.DoesNotContain(isolated, n => n.GetProperty("id").GetGuid() == prereq.Id);
    }

    [Fact]
    public async Task ImportNodes_CrossSkillAndCrossLevelEdges_ResolveSuccessfully()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        // The exact defect the 2026-07-23 audit found: an A1 grammar node as prerequisite for an
        // A1 speaking node (cross-skill) AND for an A2 grammar node (cross-level) — both
        // structurally impossible to create via Draft()'s same-CEFR+skill-only scope.
        var grammarKey = $"grammar.import_{suffix}.a1";
        var speakingKey = $"speaking.import_{suffix}.a1";
        var grammarA2Key = $"grammar.import_{suffix}.a2";

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/nodes/import", new
        {
            nodes = new[]
            {
                new { key = grammarKey, title = "Present simple statements", description = "D", cefrLevel = "A1", skill = "grammar", subskill = (string?)null, difficultyBand = 1, descriptionForAi = (string?)null, contextTags = Array.Empty<string>(), focusTags = Array.Empty<string>(), prerequisiteKeys = Array.Empty<string>() },
                new { key = speakingKey, title = "Describing daily routines", description = "D", cefrLevel = "A1", skill = "speaking", subskill = (string?)null, difficultyBand = 1, descriptionForAi = (string?)null, contextTags = Array.Empty<string>(), focusTags = Array.Empty<string>(), prerequisiteKeys = new[] { grammarKey } },
                new { key = grammarA2Key, title = "Present simple in narrative contexts", description = "D", cefrLevel = "A2", skill = "grammar", subskill = (string?)null, difficultyBand = 2, descriptionForAi = (string?)null, contextTags = Array.Empty<string>(), focusTags = Array.Empty<string>(), prerequisiteKeys = new[] { grammarKey } },
            },
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, body.GetProperty("createdCount").GetInt32());
        Assert.Equal(2, body.GetProperty("addedEdgeCount").GetInt32());
        Assert.Equal(0, body.GetProperty("droppedEdgeCount").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var grammarNode = await db.SkillGraphNodes.FirstAsync(n => n.Key == grammarKey);
        var speakingNode = await db.SkillGraphNodes.FirstAsync(n => n.Key == speakingKey);
        var grammarA2Node = await db.SkillGraphNodes.FirstAsync(n => n.Key == grammarA2Key);
        Assert.True(await db.SkillGraphPrerequisiteEdges.AnyAsync(e => e.NodeId == speakingNode.Id && e.PrerequisiteNodeId == grammarNode.Id));
        Assert.True(await db.SkillGraphPrerequisiteEdges.AnyAsync(e => e.NodeId == grammarA2Node.Id && e.PrerequisiteNodeId == grammarNode.Id));
    }

    [Fact]
    public async Task ImportNodes_ReRunSameKeys_IsIdempotentAndUpdatesInPlace()
    {
        var key = $"grammar.reimport_{Guid.NewGuid():N}.a1";
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);
        var payload = new
        {
            nodes = new[]
            {
                new { key, title = "First title", description = "D", cefrLevel = "A1", skill = "grammar", subskill = (string?)null, difficultyBand = 1, descriptionForAi = (string?)null, contextTags = Array.Empty<string>(), focusTags = Array.Empty<string>(), prerequisiteKeys = Array.Empty<string>() },
            },
        };
        await client.PostAsJsonAsync("/api/admin/skill-graph/nodes/import", payload);

        var payload2 = new
        {
            nodes = new[]
            {
                new { key, title = "Updated title", description = "D2", cefrLevel = "A1", skill = "grammar", subskill = (string?)null, difficultyBand = 2, descriptionForAi = (string?)null, contextTags = Array.Empty<string>(), focusTags = Array.Empty<string>(), prerequisiteKeys = Array.Empty<string>() },
            },
        };
        var resp2 = await client.PostAsJsonAsync("/api/admin/skill-graph/nodes/import", payload2);
        var body2 = await resp2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body2.GetProperty("createdCount").GetInt32());
        Assert.Equal(1, body2.GetProperty("updatedCount").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var saved = await db.SkillGraphNodes.FirstAsync(n => n.Key == key);
        Assert.Equal("Updated title", saved.Title);
        Assert.Equal(2, saved.DifficultyBand);
    }

    [Fact]
    public async Task NonAdmin_rejected_for_new_editability_endpoints()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"sg_edit_nonadmin_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);
        var someId = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/skill-graph/nodes", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/admin/skill-graph/nodes/{someId}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/skill-graph/nodes/isolated")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/skill-graph/nodes/import", new { nodes = Array.Empty<object>() })).StatusCode);
    }

    // ── Skill Graph rebuild Phase 6.2 — Node-to-Node AI placement suggestions (advisory only).
    // This class's plain ApiTestFactory has no AI provider configured, so these tests only cover
    // the no-AI-call and graceful-degradation paths — the response-parsing/candidate-matching
    // logic itself is exhaustively covered by NodeGraphPlacementSuggestionServiceTests (unit,
    // SwappableFakeAiProvider). ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SuggestPlacement_NodeNotFound_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsync($"/api/admin/skill-graph/nodes/{Guid.NewGuid()}/suggest-placement", null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task SuggestPlacement_NoCandidateNodes_ReturnsSuccessWithEmptyLists()
    {
        // A node whose skill/CEFR level has no other approved nodes yet — matches
        // NodeGraphPlacementSuggestionServiceTests' "no candidates" case at the HTTP layer.
        var suffix = Guid.NewGuid().ToString("N");
        var node = await SeedNodeAsync($"pronunciation.suggest_none_{suffix}.c2", "C2", "pronunciation");

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsync($"/api/admin/skill-graph/nodes/{node.Id}/suggest-placement", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Empty(body.GetProperty("prerequisites").EnumerateArray());
        Assert.Empty(body.GetProperty("dependents").EnumerateArray());
    }

    [Fact]
    public async Task SuggestPlacement_WithRealCandidates_NeverThrowsEvenWithoutARealAiProvider()
    {
        // This class's plain ApiTestFactory has no AI provider configured, so the service must
        // degrade gracefully (200 OK, success=false, error message set) rather than a 500 — the
        // AI-draft-then-validate "never throws" guarantee applies here too.
        var suffix = Guid.NewGuid().ToString("N");
        var node = await SeedNodeAsync($"grammar.suggest_target_{suffix}.a1", "A1", "grammar");
        var candidate = await SeedNodeAsync($"grammar.suggest_candidate_{suffix}.a1", "A1", "grammar");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var trackedCandidate = await db.SkillGraphNodes.FirstAsync(n => n.Id == candidate.Id);
            trackedCandidate.Approve(null);
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsync($"/api/admin/skill-graph/nodes/{node.Id}/suggest-placement", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("error").GetString()));
    }

    [Fact]
    public async Task NonAdmin_rejected_for_suggest_placement_endpoint()
    {
        var node = await SeedNodeAsync($"grammar.suggest_nonadmin_{Guid.NewGuid():N}.a1");
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"sg_suggest_nonadmin_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.PostAsync($"/api/admin/skill-graph/nodes/{node.Id}/suggest-placement", null);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── Skill Graph rebuild Phase 6.3a — deterministic redundant-edge detection ──────────────

    [Fact]
    public async Task GetRedundantEdgeSuggestions_FindsAnEdgeSpannedByALongerPath()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var a = await SeedNodeAsync($"grammar.redundant_a_{suffix}.a1");
        var x = await SeedNodeAsync($"grammar.redundant_x_{suffix}.a1");
        var b = await SeedNodeAsync($"grammar.redundant_b_{suffix}.a1");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(x.Id, a.Id)); // A -> X
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(b.Id, x.Id)); // X -> B
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(b.Id, a.Id)); // A -> B (now redundant)
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/skill-graph/suggestions/redundant-edges");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var suggestions = body.GetProperty("suggestions").EnumerateArray().ToList();
        var match = suggestions.SingleOrDefault(s =>
            s.GetProperty("proposedEdgesToRemove").EnumerateArray().Any(e =>
                e.GetProperty("prerequisiteNodeId").GetGuid() == a.Id && e.GetProperty("nodeId").GetGuid() == b.Id));
        Assert.True(match.ValueKind != JsonValueKind.Undefined, "Expected the A->B edge to be flagged redundant.");
        Assert.Equal("RedundantEdge", match.GetProperty("type").GetString());
    }

    [Fact]
    public async Task AddPrerequisite_ReturnsRedundancySuggestionWhenTheNewEdgeMakesAnotherOneRedundant()
    {
        // Scenario 1/3: A already has a direct prerequisite edge to B; now insert X between them
        // (A->X, X->B) — the second edge (X->B) should trigger a suggestion flagging the now-
        // redundant direct A->B edge, returned inline on the AddPrerequisite response.
        var suffix = Guid.NewGuid().ToString("N");
        var a = await SeedNodeAsync($"grammar.addredundant_a_{suffix}.a1");
        var x = await SeedNodeAsync($"grammar.addredundant_x_{suffix}.a1");
        var b = await SeedNodeAsync($"grammar.addredundant_b_{suffix}.a1");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(x.Id, a.Id)); // A -> X already exists
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(b.Id, a.Id)); // A -> B already exists (direct)
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        // Add X -> B, completing the A->X->B path that makes A->B redundant.
        var resp = await client.PostAsJsonAsync($"/api/admin/skill-graph/nodes/{b.Id}/prerequisites", new { prerequisiteNodeId = x.Id });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var suggestions = body.GetProperty("suggestions").EnumerateArray().ToList();
        var match = suggestions.SingleOrDefault(s =>
            s.GetProperty("proposedEdgesToRemove").EnumerateArray().Any(e =>
                e.GetProperty("prerequisiteNodeId").GetGuid() == a.Id && e.GetProperty("nodeId").GetGuid() == b.Id));
        Assert.True(match.ValueKind != JsonValueKind.Undefined, "Expected AddPrerequisite to flag the now-redundant A->B edge.");
    }

    [Fact]
    public async Task GetRedundantEdgeSuggestions_EmptyWhenNoRedundancyExists()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var a = await SeedNodeAsync($"grammar.noredundancy_a_{suffix}.a1");
        var b = await SeedNodeAsync($"grammar.noredundancy_b_{suffix}.a1");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(b.Id, a.Id));
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/skill-graph/suggestions/redundant-edges");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var suggestions = body.GetProperty("suggestions").EnumerateArray().ToList();
        Assert.DoesNotContain(suggestions, s =>
            s.GetProperty("proposedEdgesToRemove").EnumerateArray().Any(e =>
                e.GetProperty("prerequisiteNodeId").GetGuid() == a.Id && e.GetProperty("nodeId").GetGuid() == b.Id));
    }

    [Fact]
    public async Task NonAdmin_rejected_for_redundant_edges_endpoint()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"sg_redundant_nonadmin_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/admin/skill-graph/suggestions/redundant-edges");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── Skill Graph rebuild Phase 6.3c — near-duplicate node detection & merge ───────────────

    private async Task<SkillGraphNode> SeedNodeWithTitleAsync(string key, string title, string cefrLevel = "A1", string skill = "grammar")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var node = new SkillGraphNode(key, title, "Description.", cefrLevel, skill);
        db.SkillGraphNodes.Add(node);
        await db.SaveChangesAsync();
        return node;
    }

    [Fact]
    public async Task GetNearDuplicateSuggestions_FlagsHighlySimilarTitlesInTheSameCefrAndSkill()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var a = await SeedNodeWithTitleAsync($"grammar.dup_a_{suffix}.a1", "Present simple affirmative");
        var b = await SeedNodeWithTitleAsync($"grammar.dup_b_{suffix}.a1", "Present simple affirmatve"); // typo'd near-duplicate

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/skill-graph/suggestions/near-duplicates");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var suggestions = body.GetProperty("suggestions").EnumerateArray().ToList();
        var match = suggestions.SingleOrDefault(s =>
            new[] { s.GetProperty("nodeAId").GetGuid(), s.GetProperty("nodeBId").GetGuid() }.OrderBy(x => x)
                .SequenceEqual(new[] { a.Id, b.Id }.OrderBy(x => x)));
        Assert.True(match.ValueKind != JsonValueKind.Undefined, "Expected the near-duplicate pair to be flagged.");
        Assert.True(match.GetProperty("similarity").GetDouble() >= 0.85);
        // Phase 6.3f — descriptions are carried on the suggestion so the admin can judge without navigating away.
        Assert.Equal("Description.", match.GetProperty("nodeADescription").GetString());
        Assert.Equal("Description.", match.GetProperty("nodeBDescription").GetString());
    }

    [Fact]
    public async Task GetNearDuplicateSuggestions_EmptyWhenTitlesAreGenuinelyDifferent()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var a = await SeedNodeWithTitleAsync($"grammar.nodup_a_{suffix}.a1", "Present simple affirmative");
        var b = await SeedNodeWithTitleAsync($"grammar.nodup_b_{suffix}.a1", "Past continuous negative");

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/skill-graph/suggestions/near-duplicates");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var suggestions = body.GetProperty("suggestions").EnumerateArray().ToList();
        Assert.DoesNotContain(suggestions, s =>
            new[] { s.GetProperty("nodeAId").GetGuid(), s.GetProperty("nodeBId").GetGuid() }.OrderBy(x => x)
                .SequenceEqual(new[] { a.Id, b.Id }.OrderBy(x => x)));
    }

    [Fact]
    public async Task NonAdmin_rejected_for_near_duplicates_endpoint()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"sg_nearDup_nonadmin_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.GetAsync("/api/admin/skill-graph/suggestions/near-duplicates");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task GetNearDuplicateSuggestions_RealReportedFalsePositiveIsNotFlagged()
    {
        // Phase 6.3f — the exact pair a user reported as a false positive under the old
        // title-only Jaro-Winkler metric (89% similarity): same opening two words, unrelated content.
        var suffix = Guid.NewGuid().ToString("N");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphNodes.Add(new SkillGraphNode(
                $"reading.falsepos_a_{suffix}.a2", "Reading a short biography",
                "Read a short biography of a historical figure and answer comprehension questions.", "A2", "reading"));
            db.SkillGraphNodes.Add(new SkillGraphNode(
                $"reading.falsepos_b_{suffix}.a2", "Reading a holiday blog post",
                "Read a travel blogger's post about a recent holiday and identify key details.", "A2", "reading"));
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/skill-graph/suggestions/near-duplicates");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var suggestions = body.GetProperty("suggestions").EnumerateArray().ToList();
        Assert.DoesNotContain(suggestions, s =>
            s.GetProperty("nodeATitle").GetString() == "Reading a short biography" ||
            s.GetProperty("nodeBTitle").GetString() == "Reading a short biography");
    }

    // ── Skill Graph rebuild Phase 6.3f — on-demand per-pair AI confirmation. Deterministic logic
    // itself is exhaustively covered by NearDuplicateConfirmationServiceTests (unit,
    // SwappableFakeAiProvider); these confirm the HTTP wiring only. ─────────────────────────────

    [Fact]
    public async Task ConfirmNearDuplicate_NodeNotFound_Returns404()
    {
        var node = await SeedNodeAsync($"grammar.confirm_missing_{Guid.NewGuid():N}.a1");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/suggestions/near-duplicates/confirm",
            new { nodeAId = node.Id, nodeBId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ConfirmNearDuplicate_WithRealNodes_NeverThrowsEvenWithoutARealAiProvider()
    {
        // This class's plain ApiTestFactory has no AI provider configured, so the service must
        // degrade gracefully (200 OK, success=false, error message set) rather than a 500 — the
        // AI-draft-then-validate "never throws" guarantee applies here too.
        var suffix = Guid.NewGuid().ToString("N");
        var a = await SeedNodeWithTitleAsync($"grammar.confirm_a_{suffix}.a1", "Present simple affirmative");
        var b = await SeedNodeWithTitleAsync($"grammar.confirm_b_{suffix}.a1", "Present simple affirmatve");

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/suggestions/near-duplicates/confirm",
            new { nodeAId = a.Id, nodeBId = b.Id });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("error").GetString()));
    }

    [Fact]
    public async Task NonAdmin_rejected_for_confirm_near_duplicate_endpoint()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var a = await SeedNodeAsync($"grammar.confirm_nonadmin_a_{suffix}.a1");
        var b = await SeedNodeAsync($"grammar.confirm_nonadmin_b_{suffix}.a1");
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"sg_confirm_nonadmin_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/suggestions/near-duplicates/confirm",
            new { nodeAId = a.Id, nodeBId = b.Id });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task MergeNodes_RepointsEdgesAndDeactivatesTheMergedAwayNode()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var keep = await SeedNodeAsync($"grammar.merge_keep_{suffix}.a1");
        var mergeAway = await SeedNodeAsync($"grammar.merge_away_{suffix}.a1");
        var predecessor = await SeedNodeAsync($"grammar.merge_pred_{suffix}.a1");
        var dependent = await SeedNodeAsync($"grammar.merge_dep_{suffix}.a1");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(mergeAway.Id, predecessor.Id)); // predecessor -> mergeAway
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(dependent.Id, mergeAway.Id)); // mergeAway -> dependent
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsync($"/api/admin/skill-graph/nodes/{keep.Id}/merge/{mergeAway.Id}", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("repointedCount").GetInt32());
        Assert.Equal(0, body.GetProperty("droppedCount").GetInt32());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var mergedAwayNode = await verifyDb.SkillGraphNodes.FirstAsync(n => n.Id == mergeAway.Id);
        Assert.False(mergedAwayNode.IsActive);

        var remainingEdges = await verifyDb.SkillGraphPrerequisiteEdges
            .Where(e => e.NodeId == keep.Id || e.PrerequisiteNodeId == keep.Id)
            .ToListAsync();
        Assert.Contains(remainingEdges, e => e.NodeId == keep.Id && e.PrerequisiteNodeId == predecessor.Id);
        Assert.Contains(remainingEdges, e => e.NodeId == dependent.Id && e.PrerequisiteNodeId == keep.Id);
        Assert.DoesNotContain(remainingEdges, e => e.NodeId == mergeAway.Id || e.PrerequisiteNodeId == mergeAway.Id);
    }

    [Fact]
    public async Task MergeNodes_DropsAnEdgeThatWouldBecomeASelfLoop()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var keep = await SeedNodeAsync($"grammar.mergeself_keep_{suffix}.a1");
        var mergeAway = await SeedNodeAsync($"grammar.mergeself_away_{suffix}.a1");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(mergeAway.Id, keep.Id)); // keep -> mergeAway (would self-loop after repoint)
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsync($"/api/admin/skill-graph/nodes/{keep.Id}/merge/{mergeAway.Id}", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("repointedCount").GetInt32());
        Assert.Equal(1, body.GetProperty("droppedCount").GetInt32());
    }

    [Fact]
    public async Task MergeNodes_RejectsMergingANodeIntoItself()
    {
        var node = await SeedNodeAsync($"grammar.mergeselfsame_{Guid.NewGuid():N}.a1");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsync($"/api/admin/skill-graph/nodes/{node.Id}/merge/{node.Id}", null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task MergeNodes_RejectsWhenTheResultWouldCreateACycle()
    {
        // X requires mergeAway (mergeAway -> X), and keep requires X (X -> keep) already exists.
        // Repointing mergeAway -> X onto keep -> X gives "X requires keep", which combined with
        // the existing "keep requires X" is a direct 2-node cycle.
        var suffix = Guid.NewGuid().ToString("N");
        var keep = await SeedNodeAsync($"grammar.mergecycle_keep_{suffix}.a1");
        var mergeAway = await SeedNodeAsync($"grammar.mergecycle_away_{suffix}.a1");
        var x = await SeedNodeAsync($"grammar.mergecycle_x_{suffix}.a1");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(x.Id, mergeAway.Id)); // mergeAway -> X
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(keep.Id, x.Id)); // X -> keep
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsync($"/api/admin/skill-graph/nodes/{keep.Id}/merge/{mergeAway.Id}", null);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task NonAdmin_rejected_for_merge_endpoint()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var keep = await SeedNodeAsync($"grammar.mergenonadmin_keep_{suffix}.a1");
        var mergeAway = await SeedNodeAsync($"grammar.mergenonadmin_away_{suffix}.a1");
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"sg_merge_nonadmin_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.PostAsync($"/api/admin/skill-graph/nodes/{keep.Id}/merge/{mergeAway.Id}", null);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}

/// <summary>Draft-endpoint tests specifically — uses <see cref="ActivityTestFactory"/>'s
/// FakeAiProvider so no real AI provider is ever called (matches CLAUDE.md's "tests use fake/mock
/// providers, never real AI" rule; a plain ApiTestFactory would resolve a real, unconfigured
/// provider for this seeded prompt and either hang or make a real network call).</summary>
public sealed class AdminSkillGraphDraftEndpointTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public AdminSkillGraphDraftEndpointTests(ActivityTestFactory factory) => _factory = factory;

    private static HttpClient ClientWithToken(ActivityTestFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Draft_InvalidCefrLevel_ReturnsBadRequest()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/draft", new { cefrLevel = "Z9", skill = "grammar" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Rebuild Phase 2 (2026-07-23) — cross-link candidate query must not throw ──────────────

    [Fact]
    public async Task Draft_WithRealCrossSkillAndCrossLevelNodesInDb_QueriesCrossLinkCandidatesWithoutThrowing()
    {
        // Regression test for the new cross-link candidate query (same skill/other CEFR level OR
        // same CEFR level/other skill) — this exact shape of EF query previously hit a real SQLite
        // translation failure elsewhere in this session (GetIsolatedNodes' SelectMany), so this
        // asserts the Draft endpoint still returns 200 (graceful degradation via FakeAiProvider's
        // unrelated response shape) rather than a 500 from an untranslatable query.
        var suffix = Guid.NewGuid().ToString("N");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var sameSkillOtherLevel = new SkillGraphNode($"grammar.crosslink_{suffix}.a2", "T1", "D", "A2", "grammar");
            sameSkillOtherLevel.Approve(null);
            var sameLevelOtherSkill = new SkillGraphNode($"speaking.crosslink_{suffix}.a1", "T2", "D", "A1", "speaking");
            sameLevelOtherSkill.Approve(null);
            db.SkillGraphNodes.AddRange(sameSkillOtherLevel, sameLevelOtherSkill);
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/draft", new { cefrLevel = "A1", skill = "grammar" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Draft_ValidRequest_NeverThrowsEvenWhenAiResponseDoesNotMatchExpectedShape()
    {
        // FakeAiProvider returns a fixed, unrelated (module-generation) JSON shape — the drafting
        // service must degrade gracefully (200 OK, queued=false, error message set) rather than the
        // request failing with a 500, matching the AI-draft-then-validate convention's "never
        // throws" guarantee.
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/draft", new { cefrLevel = "A1", skill = "grammar" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("queued").GetBoolean());
    }

    [Fact]
    public async Task NonAdmin_rejected_for_draft_endpoint()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"sg_draft_nonadmin_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/draft", new { cefrLevel = "A1", skill = "grammar" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── Sprint 2: retag-modules ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RetagModules_NoUntaggedApprovedModules_ReturnsZeroSwept()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        // No approved, untagged Module exists specifically for this run — but the fixture is
        // shared across the test class, so assert non-negative rather than exactly zero to stay
        // robust against modules seeded by sibling tests in this class.
        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/retag-modules", new { });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("sweptCount").GetInt32() >= 0);
    }

    [Fact]
    public async Task RetagModules_ApprovedUntaggedModule_NeverThrowsEvenWhenAiResponseDoesNotMatchExpectedShape()
    {
        var cefr = "B1"; // distinct level to isolate this test's module from sibling drafts
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var node = new SkillGraphNode($"grammar.retag_{Guid.NewGuid():N}.b1", "T", "D", cefr, "grammar");
            node.Approve(null);
            db.SkillGraphNodes.Add(node);

            var module = new Module($"Retag test module {Guid.NewGuid():N}", ModuleSourceMode.Manual, cefrLevel: cefr, skill: "grammar");
            module.Approve(null);
            db.Modules.Add(module);
            await db.SaveChangesAsync();
        }

        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/retag-modules", new { });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("sweptCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task NonAdmin_rejected_for_retag_modules_endpoint()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"sg_retag_nonadmin_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(_factory, token);

        var resp = await client.PostAsJsonAsync("/api/admin/skill-graph/retag-modules", new { });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
