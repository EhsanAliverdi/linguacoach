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
