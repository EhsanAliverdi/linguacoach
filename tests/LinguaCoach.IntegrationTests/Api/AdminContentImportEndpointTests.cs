using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Phase H2 — Import Content UX v1 admin endpoint (POST /api/admin/content-imports).</summary>
public sealed class AdminContentImportEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminContentImportEndpointTests(ApiTestFactory factory) => _factory = factory;

    private static object Body(
        string sourceName, string resourceType, string inputMode, string content,
        string? defaultCefrLevel = null) => new
    {
        sourceName,
        resourceType,
        inputMode,
        content,
        defaultCefrLevel,
    };

    [Fact]
    public async Task Import_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/admin/content-imports",
            Body("Anon Source", "vocabulary", "pasted_text", "hello"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Import_NonAdmin_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/admin/content-imports",
            Body("Student Source", "vocabulary", "pasted_text", "hello"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_import_pasted_text_and_receive_a_summary()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var sourceName = $"Content Import Source {Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/admin/content-imports",
            Body(sourceName, "vocabulary", "pasted_text", "hello\nworld", defaultCefrLevel: "A1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("candidateCount").GetInt32());
        Assert.Equal(2, body.GetProperty("rawRecordCount").GetInt32());
        var runId = body.GetProperty("importRunId").GetGuid();
        var reviewRoute = body.GetProperty("reviewRoute").GetString();
        Assert.Contains(runId.ToString(), reviewRoute);

        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?importRunId={runId}");
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, candidatesBody.GetProperty("items").GetArrayLength());
        foreach (var item in candidatesBody.GetProperty("items").EnumerateArray())
        {
            Assert.False(item.GetProperty("isPublished").GetBoolean());
            Assert.Equal("A1", item.GetProperty("cefrLevel").GetString());
        }
    }

    [Fact]
    public async Task Imported_candidates_do_not_appear_in_the_unified_published_resource_bank()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var sourceName = $"Unpublished Source {Guid.NewGuid():N}";
        await client.PostAsJsonAsync("/api/admin/content-imports",
            Body(sourceName, "vocabulary", "pasted_text", "unpublishedword"));

        var bankResp = await client.GetAsync("/api/admin/resource-bank?search=unpublishedword");
        var bankBody = await bankResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, bankBody.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Empty_content_returns_400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/admin/content-imports",
            Body("Empty Content Source", "vocabulary", "pasted_text", ""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unsupported_resource_type_returns_400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Phase J5d closed the J5 type-expansion roadmap item — no genuinely "coming soon" type
        // remains, so this now asserts against a made-up type instead of a real future one.
        var response = await client.PostAsJsonAsync("/api/admin/content-imports",
            Body("Not A Real Type Source", "video", "pasted_text", "hello"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Phase J5d — "speaking" resource type ────────────────────────────────────

    [Fact]
    public async Task Speaking_resource_type_stages_a_text_only_reference_prompt()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var sourceName = $"Speaking Type Source {Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/admin/content-imports",
            Body(sourceName, "speaking", "csv_text", "title,scenario\nDeadline negotiation,Role-play: negotiate a deadline extension with your manager."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("candidateCount").GetInt32());
        var runId = body.GetProperty("importRunId").GetGuid();

        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?importRunId={runId}");
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SpeakingPrompt", candidatesBody.GetProperty("items")[0].GetProperty("candidateType").GetString());
    }

    // ── Phase J5b — "mixed" resource type ────────────────────────────────────────

    [Fact]
    public async Task Mixed_resource_type_classifies_each_row_independently()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var sourceName = $"Mixed Type Source {Guid.NewGuid():N}";
        var json = """[{"word":"hello"},{"grammarKey":"present perfect","explanation":"habitual actions"}]""";
        var response = await client.PostAsJsonAsync("/api/admin/content-imports",
            Body(sourceName, "mixed", "json_text", json));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("candidateCount").GetInt32());
        var runId = body.GetProperty("importRunId").GetGuid();

        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?importRunId={runId}");
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        var types = candidatesBody.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("candidateType").GetString()).ToList();
        Assert.Contains("VocabularyEntry", types);
        Assert.Contains("GrammarProfileEntry", types);
    }
}
