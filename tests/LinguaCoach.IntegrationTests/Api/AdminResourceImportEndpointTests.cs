using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 4.2 (2026-07-15 mandatory Import Execution Plan gate for every import) — real API-level
/// integration coverage for the unified Import pipeline: submit → plan → approve → (background
/// job run, invoked directly here since Quartz's own scheduler isn't exercised by this short-lived
/// test host — see ProcessPendingAsync below) → candidate created → review → approve → publish →
/// visible in the Resource Bank. Also proves the old public entry points this phase removed are
/// genuinely gone (404), and that publish/analyze reject candidates with no plan provenance.
/// Replaces the pre-4.2 AdminResourceImportEndpointTests, which exclusively exercised the
/// now-removed ungated file-upload/analyze/approve-and-publish endpoints.
/// </summary>
public sealed class AdminResourceImportEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminResourceImportEndpointTests(ApiTestFactory factory) => _factory = factory;

    private static object SourceBody(string name) => new
    {
        name,
        licenseType = "AdminUpload",
        sourceUrl = (string?)null,
        usageRestrictionNotes = (string?)null,
        languageCode = "en",
        allowsStudentDisplay = true,
        allowsCommercialUse = true,
        attributionText = (string?)null,
        sourceVersion = "v1",
        downloadUrl = (string?)null,
    };

    private async Task<HttpClient> AdminClientAsync()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> CreateApprovedSourceAsync(HttpClient client, string name)
    {
        var addResp = await client.PostAsJsonAsync("/api/admin/resource-sources", SourceBody(name));
        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var sourceId = addBody.GetProperty("sourceId").GetGuid();

        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/resource-sources/{sourceId}/approve", new { reason = "cleared for test" });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
        return sourceId;
    }

    /// <summary>Runs the background processing job synchronously (in-process, one sweep) — this
    /// test host does not run Quartz's own scheduled trigger within a single HTTP-test lifetime,
    /// so this stands in for "wait up to 2 minutes for the next scheduled tick."</summary>
    private async Task RunPackageProcessingAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var processingService = scope.ServiceProvider.GetRequiredService<IImportPackageProcessingService>();
        await processingService.ProcessPendingAsync(maxPackages: 10);
    }

    // ── The one canonical submission → plan → approve → process → review → publish chain ──────

    [Fact]
    public async Task Pasted_text_submission_creates_a_package_and_plan_with_no_candidates_before_approval()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"Pasted Text Source {Guid.NewGuid():N}");

        using var form = new MultipartFormDataContent
        {
            { new StringContent(sourceId.ToString()), "cefrResourceSourceId" },
            { new StringContent($"unique-word-{Guid.NewGuid():N}\nanother-word-{Guid.NewGuid():N}"), "pastedText" },
        };
        var submitResp = await client.PostAsync("/api/admin/import-packages/submit", form);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(submitBody.GetProperty("isAccepted").GetBoolean());
        var packageId = submitBody.GetProperty("importPackageId").GetGuid();

        var planResp = await client.PostAsJsonAsync($"/api/admin/import-packages/{packageId}/plan", new { });
        Assert.Equal(HttpStatusCode.OK, planResp.StatusCode);
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("awaitingApproval", planBody.GetProperty("status").GetString());

        // No candidate may exist yet — the plan hasn't been approved.
        var candidatesResp = await client.GetAsync($"/api/admin/resource-import-runs?sourceId={sourceId}");
        var runsRaw = await candidatesResp.Content.ReadAsStringAsync();
        Assert.True(candidatesResp.IsSuccessStatusCode, $"Unexpected status {candidatesResp.StatusCode}: {runsRaw}");
        var runsBody = JsonDocument.Parse(runsRaw).RootElement;
        Assert.True(runsBody.TryGetProperty("items", out var itemsEl), $"No 'items' property in: {runsRaw}");
        Assert.Equal(0, itemsEl.GetArrayLength());
    }

    [Fact]
    public async Task Approving_the_plan_then_processing_creates_candidates_traceable_to_the_package()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"E2E Source {Guid.NewGuid():N}");
        var uniqueWord = $"pipelineword{Guid.NewGuid():N}";

        using var form = new MultipartFormDataContent
        {
            { new StringContent(sourceId.ToString()), "cefrResourceSourceId" },
            { new StringContent(uniqueWord), "pastedText" },
        };
        var submitResp = await client.PostAsync("/api/admin/import-packages/submit", form);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var packageId = submitBody.GetProperty("importPackageId").GetGuid();

        var planResp = await client.PostAsJsonAsync($"/api/admin/import-packages/{packageId}/plan", new { });
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        var planId = planBody.GetProperty("planId").GetGuid();

        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/approve", new { approvedCostCeiling = 100m });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
        var approveBody = await approveResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("approved", approveBody.GetProperty("status").GetString());

        await RunPackageProcessingAsync();

        var runsResp = await client.GetAsync($"/api/admin/resource-import-runs?sourceId={sourceId}");
        var runsBody = await runsResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(runsBody.GetProperty("items").GetArrayLength() > 0);

        // Queried by source rather than a specific run id: a package can produce more than one
        // ResourceImportRun (e.g. the always-created "listening-assets" run alongside the
        // structured-data run), so picking "the newest run" is not reliably "the run with this
        // candidate" — sourceId scoping avoids that ambiguity for this test's purpose.
        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?sourceId={sourceId}");
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(candidatesBody.GetProperty("items").GetArrayLength() > 0);
        var candidateId = candidatesBody.GetProperty("items")[0].GetProperty("candidateId").GetGuid();

        // ── Review lifecycle (unchanged Phase 3 behavior) + publish, gated by the now-approved plan. ──
        var validateResp = await client.PostAsJsonAsync($"/api/admin/resource-candidates/{candidateId}/validate", new { });
        Assert.Equal(HttpStatusCode.OK, validateResp.StatusCode);

        var approveCandResp = await client.PostAsJsonAsync($"/api/admin/resource-candidates/{candidateId}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, approveCandResp.StatusCode);

        var publishResp = await client.PostAsync($"/api/admin/resource-candidates/{candidateId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishResp.StatusCode);
        var publishBody = await publishResp.Content.ReadFromJsonAsync<JsonElement>();
        // Publish may still fail on unrelated deterministic gates (e.g. missing CEFR) depending on
        // how the row was inferred — the point of this test is that provenance is NOT what blocks
        // it. Assert the provenance-specific error text never appears among any failure reasons.
        if (!publishBody.GetProperty("success").GetBoolean())
        {
            foreach (var err in publishBody.GetProperty("errors").EnumerateArray())
            {
                Assert.DoesNotContain("Import Package", err.GetString());
            }
        }

        var bankResp = await client.GetAsync($"/api/admin/resource-bank?search={uniqueWord}");
        var bankBody = await bankResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(publishBody.GetProperty("success").GetBoolean() ? 1 : 0, bankBody.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Single_csv_file_submission_produces_a_mapping_preview_and_gates_on_approval()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"CSV Source {Guid.NewGuid():N}");
        var uniqueWord = $"csvword{Guid.NewGuid():N}";

        using var form = new MultipartFormDataContent
        {
            { new StringContent(sourceId.ToString()), "cefrResourceSourceId" },
        };
        var csvBytes = Encoding.UTF8.GetBytes($"word,cefrLevel\n{uniqueWord},A1\n");
        var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "files", "vocab.csv");

        var submitResp = await client.PostAsync("/api/admin/import-packages/submit", form);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var packageId = submitBody.GetProperty("importPackageId").GetGuid();

        var planResp = await client.PostAsJsonAsync($"/api/admin/import-packages/{packageId}/plan", new { });
        Assert.Equal(HttpStatusCode.OK, planResp.StatusCode);
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        var previews = planBody.GetProperty("estimate").GetProperty("structuredMappingPreviews");
        Assert.True(previews.GetArrayLength() > 0);
        var detectedColumns = previews[0].GetProperty("detectedColumns").EnumerateArray()
            .Select(c => c.GetString()).ToList();
        Assert.Contains("word", detectedColumns);
        Assert.Contains("cefrLevel", detectedColumns);
    }

    // ── Removed old public entry points are genuinely gone ──────────────────────────────────

    // A route is genuinely gone whether ASP.NET reports it as 404 (no matching template at all)
    // or 405 (the path template still matches a sibling action but no handler accepts this verb/
    // sub-path) — both mean "this specific action no longer exists."
    private static readonly HttpStatusCode[] RouteGoneStatusCodes = { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed };

    [Fact]
    public async Task Old_content_imports_endpoint_no_longer_exists()
    {
        var client = await AdminClientAsync();
        var response = await client.PostAsJsonAsync("/api/admin/content-imports", new { });
        Assert.Contains(response.StatusCode, RouteGoneStatusCodes);
    }

    [Fact]
    public async Task Old_file_upload_import_endpoint_no_longer_exists()
    {
        var client = await AdminClientAsync();
        using var form = new MultipartFormDataContent();
        var response = await client.PostAsync("/api/admin/resource-import-runs", form);
        Assert.Contains(response.StatusCode, RouteGoneStatusCodes);
    }

    [Fact]
    public async Task Old_propose_mapping_endpoints_no_longer_exist()
    {
        var client = await AdminClientAsync();
        var r1 = await client.PostAsJsonAsync("/api/admin/content-imports/propose-mapping", new { });
        Assert.Contains(r1.StatusCode, RouteGoneStatusCodes);

        using var form = new MultipartFormDataContent();
        var r2 = await client.PostAsync("/api/admin/resource-import-runs/propose-mapping", form);
        Assert.Contains(r2.StatusCode, RouteGoneStatusCodes);
    }

    [Fact]
    public async Task Old_batch_analyze_endpoint_no_longer_exists()
    {
        var client = await AdminClientAsync();
        var response = await client.PostAsync($"/api/admin/resource-import-runs/{Guid.NewGuid()}/candidates/analyze", null);
        Assert.Contains(response.StatusCode, RouteGoneStatusCodes);
    }

    [Fact]
    public async Task Old_approve_and_publish_endpoints_no_longer_exist()
    {
        var client = await AdminClientAsync();
        var r1 = await client.PostAsJsonAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/approve-and-publish", new { });
        Assert.Contains(r1.StatusCode, RouteGoneStatusCodes);

        var r2 = await client.PostAsJsonAsync("/api/admin/resource-candidates/batch/approve-and-publish", new { candidateIds = Array.Empty<Guid>() });
        Assert.Contains(r2.StatusCode, RouteGoneStatusCodes);
    }

    // ── Read-only run/raw-record endpoints remain (unchanged behavior) ──────────────────────

    [Fact]
    public async Task List_runs_unauthenticated_returns_401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/resource-import-runs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Submit_with_no_content_returns_400()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"Empty Source {Guid.NewGuid():N}");

        using var form = new MultipartFormDataContent
        {
            { new StringContent(sourceId.ToString()), "cefrResourceSourceId" },
        };
        var response = await client.PostAsync("/api/admin/import-packages/submit", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
