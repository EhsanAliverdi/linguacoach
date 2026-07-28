using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Full content reseed (2026-07-28) — guarded, dev-only, confirmation-gated hard-delete of every
/// content entity. See <c>AdminContentWipeController</c> for the deletion-order rationale.
/// </summary>
public sealed class AdminContentWipeEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminContentWipeEndpointTests(ApiTestFactory factory) => _factory = factory;

    private static HttpClient ClientWithToken(ApiTestFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Seeds one row across every table the wipe touches — a real Module, Lesson,
    /// Exercise, SkillGraphNode(+edge), ResourceBankItem(+source) — so the test exercises the
    /// real FK-dependency chain, not just empty-table no-ops.</summary>
    private async Task SeedOneOfEverythingAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var source = new CefrResourceSource($"Wipe test source {suffix}", "Internal", allowsStudentDisplay: true, allowsCommercialUse: true);
        db.CefrResourceSources.Add(source);
        var lesson = new Lesson($"Wipe test lesson {suffix}", "Body.", LessonSourceMode.Manual, "A1", "grammar");
        db.Lessons.Add(lesson);
        var module = new Module($"Wipe test module {suffix}", ModuleSourceMode.Manual, cefrLevel: "A1", skill: "grammar");
        db.Modules.Add(module);
        var node = new SkillGraphNode($"grammar.wipe_test_{suffix}", "Wipe test node", "D.", "A1", "grammar");
        db.SkillGraphNodes.Add(node);
        await db.SaveChangesAsync();

        var exercise = new Exercise($"Wipe test exercise {suffix}", "Instructions.", "gap_fill",
            ExerciseRendererType.Formio, ExerciseSourceMode.Manual, cefrLevel: "A1", skill: "grammar", lessonId: lesson.Id);
        db.Exercises.Add(exercise);
        var item = new ResourceBankItem(PublishedResourceType.Vocabulary, source.Id, "A1",
            ResourceBankItemContent.Serialize(new VocabularyContent("wipe-test-word", null, null)));
        db.ResourceBankItems.Add(item);
        // Restrict against CefrResourceSource, same as ResourceImportRun/ResourceBankItem — the
        // real dev DB's own 4 rows here are what surfaced the missing deletion step this test class
        // now guards against.
        var importPackage = new ImportPackage(source.Id, $"wipe-test-{suffix}.zip", DateTimeOffset.UtcNow);
        db.ImportPackages.Add(importPackage);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Wipe_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/admin/content-wipe", new { confirm = false });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wipe_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"wipe403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(_factory, token).PostAsJsonAsync("/api/admin/content-wipe", new { confirm = false });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Wipe_WithoutConfirm_ReportsCountsAndDeletesNothing()
    {
        await SeedOneOfEverythingAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/content-wipe", new { confirm = false });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("requiresConfirmation").GetBoolean());
        Assert.True(body.GetProperty("counts").GetProperty("modules").GetInt32() > 0);
        Assert.True(body.GetProperty("counts").GetProperty("importPackages").GetInt32() > 0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.True(await db.Modules.AnyAsync());
    }

    [Fact]
    public async Task Wipe_WithConfirm_DeletesEverythingInFkSafeOrder()
    {
        await SeedOneOfEverythingAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/content-wipe", new { confirm = true });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("requiresConfirmation").GetBoolean());
        var finalCounts = body.GetProperty("counts");
        Assert.Equal(0, finalCounts.GetProperty("modules").GetInt32());
        Assert.Equal(0, finalCounts.GetProperty("lessons").GetInt32());
        Assert.Equal(0, finalCounts.GetProperty("exercises").GetInt32());
        Assert.Equal(0, finalCounts.GetProperty("skillGraphNodes").GetInt32());
        Assert.Equal(0, finalCounts.GetProperty("resourceBankItems").GetInt32());
        Assert.Equal(0, finalCounts.GetProperty("importPackages").GetInt32());
        Assert.Equal(0, finalCounts.GetProperty("cefrResourceSources").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.False(await db.Modules.AnyAsync());
        Assert.False(await db.Lessons.AnyAsync());
        Assert.False(await db.Exercises.AnyAsync());
        Assert.False(await db.SkillGraphNodes.AnyAsync());
        Assert.False(await db.ResourceBankItems.AnyAsync());
        Assert.False(await db.ImportPackages.AnyAsync());
        Assert.False(await db.CefrResourceSources.AnyAsync());
    }

    [Fact]
    public async Task Wipe_DeletesImportPackagesBeforeCefrResourceSources()
    {
        // Regression test — the real dev DB run against this endpoint failed with a Postgres FK
        // violation ("import_packages" -> "cefr_resource_sources") because ImportPackage wasn't in
        // the original deletion order at all. This seeds exactly that shape and proves the fix.
        await SeedOneOfEverythingAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/content-wipe", new { confirm = true });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.False(await db.ImportPackages.AnyAsync());
        Assert.False(await db.CefrResourceSources.AnyAsync());
    }

    [Fact]
    public async Task Wipe_DeletesPrerequisiteEdgesBeforeNodes()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var a = new SkillGraphNode($"grammar.wipe_edge_a_{suffix}", "A", "D.", "A1", "grammar");
            var b = new SkillGraphNode($"grammar.wipe_edge_b_{suffix}", "B", "D.", "A1", "grammar");
            db.SkillGraphNodes.AddRange(a, b);
            await db.SaveChangesAsync();
            db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(b.Id, a.Id));
            await db.SaveChangesAsync();
        }
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.PostAsJsonAsync("/api/admin/content-wipe", new { confirm = true });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.False(await verifyDb.SkillGraphPrerequisiteEdges.AnyAsync());
        Assert.False(await verifyDb.SkillGraphNodes.AnyAsync());
    }

    [Fact]
    public async Task Preview_ReturnsCurrentCountsWithoutMutating()
    {
        await SeedOneOfEverythingAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(_factory, adminToken);

        var resp = await client.GetAsync("/api/admin/content-wipe/preview");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("modules").GetInt32() > 0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        Assert.True(await db.Modules.AnyAsync());
    }
}
