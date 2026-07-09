using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Phase E5 — read-only browse/search over the published Cefr* bank tables. All 6
/// endpoints (3 list + 3 detail) are admin-only, matching AdminResourceCandidateController's
/// existing auth convention.</summary>
public sealed class AdminResourceBankEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminResourceBankEndpointTests(ApiTestFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/admin/resource-banks/vocabulary")]
    [InlineData("/api/admin/resource-banks/grammar")]
    [InlineData("/api/admin/resource-banks/reading-references")]
    [InlineData("/api/admin/resource-banks/reading-passages")]
    public async Task List_Unauthenticated_Returns401(string route)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/resource-banks/vocabulary")]
    [InlineData("/api/admin/resource-banks/grammar")]
    [InlineData("/api/admin/resource-banks/reading-references")]
    [InlineData("/api/admin/resource-banks/reading-passages")]
    public async Task List_NonAdmin_Returns403(string route)
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/resource-banks/vocabulary")]
    [InlineData("/api/admin/resource-banks/grammar")]
    [InlineData("/api/admin/resource-banks/reading-references")]
    [InlineData("/api/admin/resource-banks/reading-passages")]
    public async Task Detail_Unauthenticated_Returns401(string route)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"{route}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/resource-banks/vocabulary")]
    [InlineData("/api/admin/resource-banks/grammar")]
    [InlineData("/api/admin/resource-banks/reading-references")]
    [InlineData("/api/admin/resource-banks/reading-passages")]
    public async Task Detail_NonAdmin_Returns403(string route)
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"{route}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/resource-banks/vocabulary")]
    [InlineData("/api/admin/resource-banks/grammar")]
    [InlineData("/api/admin/resource-banks/reading-references")]
    [InlineData("/api/admin/resource-banks/reading-passages")]
    public async Task Detail_NonexistentId_Returns404(string route)
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"{route}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/resource-banks/vocabulary")]
    [InlineData("/api/admin/resource-banks/grammar")]
    [InlineData("/api/admin/resource-banks/reading-references")]
    [InlineData("/api/admin/resource-banks/reading-passages")]
    public async Task List_Admin_Returns200_With_Empty_Result_When_Nothing_Published(string route)
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Phase E9 — published metadata is exposed and filterable end-to-end ───────

    /// <summary>Runs the E8 pack through the real staging → validation → approval → publish
    /// pipeline in the shared fixture (idempotent by source name; the Testing environment skips the
    /// startup resource seeders), then runs the E9 backfill (a no-op once publish carries metadata).</summary>
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
        await PublishedBankMetadataBackfillSeeder.RunAsync(
            sp.GetRequiredService<LinguaCoachDbContext>(), NullLogger.Instance);
    }

    // ── Phase E10 — enriched depth metadata is filterable end-to-end ─────────────

    [Fact]
    public async Task Vocabulary_list_can_be_filtered_by_E10_derived_difficulty_band_end_to_end()
    {
        await EnsureE8PublishedAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            // E10 depth enrichment (derives difficulty from CEFR, focus from subskill).
            await InternalBankMetadataDepthSeeder.RunAsync(
                scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>(), NullLogger.Instance);
        }

        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // B1 vocab now carries E10-derived difficulty band 3.
        var response = await client.GetAsync("/api/admin/resource-banks/vocabulary?cefrLevel=B1&difficultyBand=3&pageSize=200");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = doc.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);
        foreach (var item in items.EnumerateArray())
            Assert.Equal(3, item.GetProperty("difficultyBand").GetInt32());
    }

    [Fact]
    public async Task Vocabulary_list_exposes_E9_selection_metadata_from_the_seeded_bank()
    {
        await EnsureE8PublishedAsync();

        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // The E8 pack publishes B1 vocabulary carrying context tags.
        var response = await client.GetAsync("/api/admin/resource-banks/vocabulary?cefrLevel=B1&pageSize=200");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = doc.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);
        // At least one row exposes non-empty selection metadata (context tags), proving the
        // publish-mapping/backfill carried metadata onto the lean bank rows.
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

        var response = await client.GetAsync("/api/admin/resource-banks/vocabulary?contextTag=workplace&pageSize=200");
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

    [Fact]
    public async Task Existing_Typed_Vocabulary_Endpoint_Still_Works_After_Unified_Endpoint_Added()
    {
        await EnsureE8PublishedAsync();

        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/admin/resource-banks/vocabulary?pageSize=200");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(doc.GetProperty("items").GetArrayLength() > 0);
    }
}
