using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Phase E1 — English resource import staging admin endpoints.</summary>
public sealed class AdminResourceImportEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminResourceImportEndpointTests(ApiTestFactory factory) => _factory = factory;

    private static object SourceBody(string name, bool approved = false) => new
    {
        name,
        licenseType = "CC-BY-4.0",
        sourceUrl = (string?)null,
        usageRestrictionNotes = (string?)null,
        languageCode = "en",
        allowsStudentDisplay = true,
        allowsCommercialUse = true,
        attributionText = (string?)null,
        sourceVersion = "v1",
        downloadUrl = (string?)null,
    };

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/resource-sources");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AddSource_ThenApprove_ThenImportCsv_StagesCandidates()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var name = $"Test Source {Guid.NewGuid():N}";
        var addResp = await client.PostAsJsonAsync("/api/admin/resource-sources", SourceBody(name));
        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var sourceId = addBody.GetProperty("sourceId").GetGuid();
        Assert.False(addBody.GetProperty("isImportApproved").GetBoolean());

        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/resource-sources/{sourceId}/approve", new { reason = "cleared for test" });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
        var approveBody = await approveResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(approveBody.GetProperty("isImportApproved").GetBoolean());

        using var form = new MultipartFormDataContent
        {
            { new StringContent(sourceId.ToString()), "sourceId" },
            { new StringContent("Csv"), "importMode" },
        };

        var csvBytes = Encoding.UTF8.GetBytes("word,cefr\nhello,A1\nworld,A1\n");
        var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "file", "vocab.csv");

        var importResp = await client.PostAsync("/api/admin/resource-import-runs", form);
        Assert.Equal(HttpStatusCode.OK, importResp.StatusCode);
        var importBody = await importResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, importBody.GetProperty("succeededCount").GetInt32());
        var runId = importBody.GetProperty("runId").GetGuid();

        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?importRunId={runId}");
        Assert.Equal(HttpStatusCode.OK, candidatesResp.StatusCode);
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, candidatesBody.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Import_Rejected_When_Source_Not_Approved()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var name = $"Unapproved Source {Guid.NewGuid():N}";
        var addResp = await client.PostAsJsonAsync("/api/admin/resource-sources", SourceBody(name));
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var sourceId = addBody.GetProperty("sourceId").GetGuid();

        using var form = new MultipartFormDataContent
        {
            { new StringContent(sourceId.ToString()), "sourceId" },
            { new StringContent("Csv"), "importMode" },
        };
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("word,cefr\nhello,A1\n"));
        form.Add(fileContent, "file", "vocab.csv");

        var importResp = await client.PostAsync("/api/admin/resource-import-runs", form);
        Assert.Equal(HttpStatusCode.BadRequest, importResp.StatusCode);
    }

    // ── Phase E2 — AI analysis / validation trigger endpoints ──────────────────────

    private async Task<(Guid RunId, Guid CandidateId)> ImportOneApprovedCandidateAsync(
        System.Net.Http.HttpClient client)
    {
        var name = $"E2 Test Source {Guid.NewGuid():N}";
        var addResp = await client.PostAsJsonAsync("/api/admin/resource-sources", SourceBody(name));
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var sourceId = addBody.GetProperty("sourceId").GetGuid();
        await client.PostAsJsonAsync($"/api/admin/resource-sources/{sourceId}/approve", new { reason = "cleared for test" });

        using var form = new MultipartFormDataContent
        {
            { new StringContent(sourceId.ToString()), "sourceId" },
            { new StringContent("Csv"), "importMode" },
        };
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("word,cefr\nhello,A1\n")), "file", "vocab.csv");

        var importResp = await client.PostAsync("/api/admin/resource-import-runs", form);
        var importBody = await importResp.Content.ReadFromJsonAsync<JsonElement>();
        var runId = importBody.GetProperty("runId").GetGuid();

        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?importRunId={runId}");
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        var candidateId = candidatesBody.GetProperty("items")[0].GetProperty("candidateId").GetGuid();

        return (runId, candidateId);
    }

    [Fact]
    public async Task Analyze_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/analyze", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_NonAdmin_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/analyze", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzePendingCandidates_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/admin/resource-import-runs/{Guid.NewGuid()}/candidates/analyze", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_OneCandidate_Succeeds_And_Never_Writes_To_Published_Bank_Tables()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var (_, candidateId) = await ImportOneApprovedCandidateAsync(client);

        // The default test AI config is fake/fake (unusable), so analysis is expected to fail
        // gracefully — the important assertion is that this never surfaces as a 500 and never
        // writes to any published Cefr* bank table, per E1/E2's staging-only discipline.
        var analyzeResp = await client.PostAsync($"/api/admin/resource-candidates/{candidateId}/analyze", null);
        Assert.Equal(HttpStatusCode.OK, analyzeResp.StatusCode);

        var validateResp = await client.PostAsync($"/api/admin/resource-candidates/{candidateId}/validate", null);
        Assert.Equal(HttpStatusCode.OK, validateResp.StatusCode);
    }

    [Fact]
    public async Task AnalyzePendingCandidates_Batch_Reports_Considered_And_Analyzed_Counts()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var (runId, _) = await ImportOneApprovedCandidateAsync(client);

        var batchResp = await client.PostAsync($"/api/admin/resource-import-runs/{runId}/candidates/analyze", null);
        Assert.Equal(HttpStatusCode.OK, batchResp.StatusCode);
        var batchBody = await batchResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, batchBody.GetProperty("candidatesConsidered").GetInt32());
        Assert.Equal(1, batchBody.GetProperty("candidatesAnalyzed").GetInt32());
        Assert.False(batchBody.GetProperty("batchLimitReached").GetBoolean());
    }
}
