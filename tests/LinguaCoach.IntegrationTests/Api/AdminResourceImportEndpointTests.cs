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
}
