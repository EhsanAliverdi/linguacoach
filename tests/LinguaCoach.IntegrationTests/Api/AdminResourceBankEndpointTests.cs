using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Phase I0 — read-only browse/search over the single consolidated Resource Bank table via
/// the unified <c>GET api/admin/resource-bank</c> endpoint. The four typed routes
/// (vocabulary/grammar/reading-references/reading-passages) were removed in this phase — see
/// AdminResourceBankController's doc comment.</summary>
public sealed class AdminResourceBankEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminResourceBankEndpointTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Unified_List_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/resource-bank");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unified_List_NonAdmin_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/resource-bank");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unified_List_Admin_Returns200_With_Empty_Result_When_Nothing_Published()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Deliberately hits a CEFR level the shared fixture's seeded packs never use, so this
        // proves "200 + well-formed empty result", not "nothing has ever been published".
        var response = await client.GetAsync("/api/admin/resource-bank?cefrLevel=C2&type=Vocabulary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, doc.GetProperty("items").GetArrayLength());
    }

    // ── Phase E9 — published metadata is exposed and filterable end-to-end ───────

    /// <summary>Runs the E8 pack through the real staging → validation → approval → publish
    /// pipeline in the shared fixture (idempotent by source name; the Testing environment skips the
    /// startup resource seeders). Publish itself now carries E9 selection metadata directly onto
    /// ResourceBankItem — no separate backfill step is needed.</summary>
    private async Task EnsureE8PublishedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        await InternalResourceSeedPackE8Seeder.SeedAsync(
            sp.GetRequiredService<LinguaCoachDbContext>(),
            sp.GetRequiredService<IResourceImportService>(),
            sp.GetRequiredService<IResourceCandidateValidationService>(),
            sp.GetRequiredService<IResourceCandidatePublishService>(),
            NullLogger.Instance);
    }

    [Fact]
    public async Task Vocabulary_list_exposes_E9_selection_metadata_from_the_seeded_bank()
    {
        await EnsureE8PublishedAsync();

        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // The E8 pack publishes B1 vocabulary carrying context tags.
        var response = await client.GetAsync("/api/admin/resource-bank?type=Vocabulary&cefrLevel=B1&pageSize=200");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = doc.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);
        // At least one row exposes non-empty selection metadata (context tags), proving the
        // publish-mapping carried metadata onto the consolidated bank rows.
        var anyWithContext = items.EnumerateArray().Any(i =>
            i.TryGetProperty("contextTags", out var tags)
            && tags.ValueKind == JsonValueKind.Array && tags.GetArrayLength() > 0);
        Assert.True(anyWithContext);
    }

    [Fact]
    public async Task Vocabulary_list_can_be_filtered_by_context_tag_end_to_end()
    {
        await EnsureE8PublishedAsync();

        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/resource-bank?type=Vocabulary&contextTag=workplace&pageSize=200");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = doc.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0); // E8 has workplace-tagged vocabulary

        // Every returned row must actually carry the requested context tag.
        foreach (var item in items.EnumerateArray())
        {
            var tags = item.GetProperty("contextTags").EnumerateArray().Select(t => t.GetString()).ToList();
            Assert.Contains("workplace", tags);
        }
    }

    // ── Phase H1 — unified Resource Bank read model endpoint ──────────────────────

    [Fact]
    public async Task Unified_List_Returns_Mixed_Typed_Rows()
    {
        await EnsureE8PublishedAsync();

        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/resource-bank?pageSize=200");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = doc.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);

        // The E8 pack publishes vocabulary, grammar, short reading references, and full passages —
        // the unified endpoint must surface more than one distinct type in a single call.
        var distinctTypes = items.EnumerateArray()
            .Select(i => i.GetProperty("type").GetString())
            .Distinct()
            .ToList();
        Assert.True(distinctTypes.Count > 1);
    }

    [Fact]
    public async Task Unified_List_Filtered_By_Type_And_Cefr_Returns_Only_Matching_Rows()
    {
        await EnsureE8PublishedAsync();

        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/resource-bank?type=Vocabulary&cefrLevel=B1&pageSize=200");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = doc.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);
        foreach (var item in items.EnumerateArray())
        {
            Assert.Equal("vocabulary", item.GetProperty("type").GetString());
            Assert.Equal("B1", item.GetProperty("cefrLevel").GetString());
        }
    }

    [Fact]
    public async Task Unified_List_Unknown_Type_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/resource-bank?type=NotARealType");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
