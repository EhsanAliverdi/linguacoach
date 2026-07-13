using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase K1 — AI-assisted import column-mapping "propose" endpoints. The integration test
/// environment's AI config falls back to the seeded fake/fake provider (no real API key ever
/// configured in tests), so these exercise the graceful-degrade path — same convention as
/// AdminResourceImportEndpointTests' Analyze_* tests. Never stage anything, never write to the DB.
/// </summary>
public sealed class ResourceImportColumnMappingEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ResourceImportColumnMappingEndpointTests(ApiTestFactory factory) => _factory = factory;

    private async Task<System.Net.Http.HttpClient> AdminClientAsync()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task FileUpload_ProposeMapping_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        using var form = new MultipartFormDataContent { { new StringContent("Csv"), "importMode" } };
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("headword,CEFR\nabandon,B1\n")), "file", "test.csv");

        var response = await client.PostAsync("/api/admin/resource-import-runs/propose-mapping", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FileUpload_ProposeMapping_ParsesHeaderAndDegradesGracefullyWhenAiUnavailable()
    {
        var client = await AdminClientAsync();
        using var form = new MultipartFormDataContent { { new StringContent("Csv"), "importMode" } };
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("headword,CEFR\nabandon,B1\n")), "file", "test.csv");

        var response = await client.PostAsync("/api/admin/resource-import-runs/propose-mapping", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Graceful degrade: never stages, never throws, just reports no AI suggestion.
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Empty(body.GetProperty("suggestions").EnumerateArray());
    }

    [Fact]
    public async Task FileUpload_ProposeMapping_RejectsUnparsableFile()
    {
        var client = await AdminClientAsync();
        using var form = new MultipartFormDataContent { { new StringContent("Json"), "importMode" } };
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("{ not valid json")), "file", "test.json");

        var response = await client.PostAsync("/api/admin/resource-import-runs/propose-mapping", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ContentImport_ProposeMapping_ForCsvText_DegradesGracefullyWhenAiUnavailable()
    {
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/admin/content-imports/propose-mapping", new
        {
            inputMode = "csv_text",
            content = "headword,CEFR\nabandon,B1\n",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task ContentImport_ProposeMapping_ForPastedText_ReturnsTrivialSuccessWithNoSuggestions()
    {
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/admin/content-imports/propose-mapping", new
        {
            inputMode = "pasted_text",
            content = "hello\nworld",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // pasted_text is synthetic single-column {"text": line} rows — no header ambiguity, so this
        // is a trivial success with nothing to suggest, never an AI call.
        Assert.True(body.GetProperty("success").GetBoolean());
        Assert.Empty(body.GetProperty("suggestions").EnumerateArray());
    }

    [Fact]
    public async Task Import_WithColumnRenames_StagesRowsThatWouldOtherwiseBeRejected()
    {
        var client = await AdminClientAsync();
        var sourceName = $"Column Rename Test Source {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/admin/content-imports", new
        {
            sourceName,
            resourceType = "vocabulary",
            inputMode = "csv_text",
            content = "term,level\nabandon,B1\n", // "term"/"level" aren't recognized on their own
            columnRenames = new Dictionary<string, string> { ["term"] = "word", ["level"] = "cefrLevel" },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("candidateCount").GetInt32());
    }
}
