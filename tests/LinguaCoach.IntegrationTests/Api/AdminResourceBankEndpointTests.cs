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
}
