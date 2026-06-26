using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.IntegrationTests.Api;

namespace LinguaCoach.IntegrationTests.Curriculum;

/// <summary>
/// Integration tests for Phase 11B curriculum validation and coverage matrix endpoints.
/// </summary>
public sealed class CurriculumValidationIntegrationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public CurriculumValidationIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task AuthAsAdmin()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // ── GET /api/admin/curriculum/validation ─────────────────────────────────

    [Fact]
    public async Task GetValidationSummary_ReturnsOk()
    {
        await AuthAsAdmin();

        var response = await _client.GetAsync("/api/admin/curriculum/validation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("isValid", out _), "Response must contain 'isValid'.");
        Assert.True(body.TryGetProperty("totalObjectivesChecked", out var total), "Response must contain 'totalObjectivesChecked'.");
        Assert.True(body.TryGetProperty("errorCount", out _), "Response must contain 'errorCount'.");
        Assert.True(body.TryGetProperty("warningCount", out _), "Response must contain 'warningCount'.");
        Assert.True(body.TryGetProperty("coverageGapCount", out _), "Response must contain 'coverageGapCount'.");
        Assert.True(body.TryGetProperty("errors", out _), "Response must contain 'errors' array.");
        Assert.True(body.TryGetProperty("warnings", out _), "Response must contain 'warnings' array.");
        Assert.True(body.TryGetProperty("coverageGaps", out _), "Response must contain 'coverageGaps' array.");

        // After Phase 11B seeding, total should be at least 33 (22 original + 11 new).
        Assert.True(total.GetInt32() >= 33,
            $"Expected at least 33 seeded objectives, got {total.GetInt32()}.");
    }

    [Fact]
    public async Task GetValidationSummary_Unauthenticated_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/admin/curriculum/validation");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GET /api/admin/curriculum/coverage ───────────────────────────────────

    [Fact]
    public async Task GetCoverageMatrix_ReturnsOkWithExpectedStructure()
    {
        await AuthAsAdmin();

        var response = await _client.GetAsync("/api/admin/curriculum/coverage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("cefrLevels", out var levels), "Response must contain 'cefrLevels'.");
        Assert.True(body.TryGetProperty("skills", out var skills), "Response must contain 'skills'.");
        Assert.True(body.TryGetProperty("cells", out var cells), "Response must contain 'cells'.");

        // Should have exactly 4 CEFR levels (A1-B2).
        Assert.Equal(4, levels.GetArrayLength());

        // Skills array must be non-empty.
        Assert.True(skills.GetArrayLength() > 0);

        // Cells count = levels x skills.
        var expectedCells = levels.GetArrayLength() * skills.GetArrayLength();
        Assert.Equal(expectedCells, cells.GetArrayLength());

        // Spot-check cell structure.
        var firstCell = cells.EnumerateArray().First();
        Assert.True(firstCell.TryGetProperty("cefrLevel", out _));
        Assert.True(firstCell.TryGetProperty("skill", out _));
        Assert.True(firstCell.TryGetProperty("activeCount", out _));
        Assert.True(firstCell.TryGetProperty("hasCoverage", out _));
    }

    [Fact]
    public async Task GetCoverageMatrix_AfterSeeding_SpeakingA1HasCoverage()
    {
        await AuthAsAdmin();

        var response = await _client.GetAsync("/api/admin/curriculum/coverage");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var cells = body.GetProperty("cells").EnumerateArray().ToList();

        var a1Speaking = cells.FirstOrDefault(c =>
            c.GetProperty("cefrLevel").GetString() == "A1" &&
            c.GetProperty("skill").GetString() == "speaking");

        Assert.True(a1Speaking.TryGetProperty("hasCoverage", out var hasCoverage));
        Assert.True(hasCoverage.GetBoolean(), "A1/speaking must have coverage after seeding.");
    }
}
