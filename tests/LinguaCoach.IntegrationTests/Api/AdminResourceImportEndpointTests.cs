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
        // No cefr column here on purpose — several tests using this helper rely on the resulting
        // candidate being genuinely "pending" (AiAnalysisJson == null) for AnalyzePendingCandidates
        // to pick up. A recognized cefr column would set it deterministically at import time
        // (Phase E6), which is correct behavior but would make this fixture stop being "pending".
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes($"word\n{Guid.NewGuid():N}\n")), "file", "vocab.csv");

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

    // ── Phase E3 — read-only rendered preview endpoint ──────────────────────────────

    [Fact]
    public async Task Preview_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/preview");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Preview_NonAdmin_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/preview");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Preview_NonexistentCandidate_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/preview");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Preview_ExistingCandidate_Returns200_And_Never_Writes_To_Published_Bank_Tables()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var (_, candidateId) = await ImportOneApprovedCandidateAsync(client);

        var beforeResp = await client.GetAsync($"/api/admin/resource-candidates/{candidateId}");
        var beforeBody = await beforeResp.Content.ReadFromJsonAsync<JsonElement>();
        var updatedAtBefore = beforeBody.GetProperty("updatedAtUtc").GetString();

        var previewResp = await client.GetAsync($"/api/admin/resource-candidates/{candidateId}/preview");
        Assert.Equal(HttpStatusCode.OK, previewResp.StatusCode);
        var previewBody = await previewResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("VocabularyEntry", previewBody.GetProperty("candidateType").GetString());
        Assert.True(previewBody.TryGetProperty("renderedPreviewModel", out _));
        Assert.True(previewBody.TryGetProperty("source", out var sourceEl));
        Assert.False(string.IsNullOrWhiteSpace(sourceEl.GetProperty("sourceName").GetString()));

        var afterResp = await client.GetAsync($"/api/admin/resource-candidates/{candidateId}");
        var afterBody = await afterResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(updatedAtBefore, afterBody.GetProperty("updatedAtUtc").GetString());
    }

    // ── Phase E4 — approve/reject/publish workflow ──────────────────────────────────

    [Fact]
    public async Task Approve_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/approve", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Approve_NonAdmin_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/approve", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reject_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/admin/resource-candidates/{Guid.NewGuid()}/reject", new { reason = "bad" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reject_NonAdmin_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync(
            $"/api/admin/resource-candidates/{Guid.NewGuid()}/reject", new { reason = "bad" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Publish_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/publish", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Publish_NonAdmin_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/publish", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reject_WithBlankReason_Returns400()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var (_, candidateId) = await ImportOneApprovedCandidateAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/admin/resource-candidates/{candidateId}/reject", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Approve_ThenPublish_WithoutValidationPassed_ReturnsFailureNotException()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var (_, candidateId) = await ImportOneApprovedCandidateAsync(client);

        var approveResp = await client.PostAsJsonAsync($"/api/admin/resource-candidates/{candidateId}/approve", new { });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
        var approveBody = await approveResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", approveBody.GetProperty("reviewStatus").GetString());

        // A freshly-imported candidate has never been through E2's AI analysis/validation pass in
        // this test (the test AI provider is fake/unusable), so ValidationStatus is still Pending
        // — publish must fail cleanly with a reason, never throw/500.
        var publishResp = await client.PostAsync($"/api/admin/resource-candidates/{candidateId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishResp.StatusCode);
        var publishBody = await publishResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(publishBody.GetProperty("success").GetBoolean());
        Assert.True(publishBody.GetProperty("errors").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Publish_NonexistentCandidate_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/publish", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Phase I1 — merged approve-and-publish endpoint ──────────────────────────────

    [Fact]
    public async Task ApproveAndPublish_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/approve-and-publish", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApproveAndPublish_NonAdmin_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/approve-and-publish", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApproveAndPublish_NonexistentCandidate_Returns404()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/approve-and-publish", new { });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApproveAndPublish_UnapprovedButOtherwiseValidCandidate_PublishesInOneCall()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Judgment call: licenseType intentionally does NOT contain "BY" (unlike SourceBody's
        // default "CC-BY-4.0") so ValidateAttribution never fires its "requires attribution"
        // warning — that warning alone would force ValidationStatus to NeedsReview instead of
        // Passed, and PublishAsync requires Passed. Mirrors the frontend's own AdminUpload
        // default license (see AdminContentImportComponent.createSource).
        var name = $"I1 Test Source {Guid.NewGuid():N}";
        var addResp = await client.PostAsJsonAsync("/api/admin/resource-sources", new
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
        });
        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);
        var addBody = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var sourceId = addBody.GetProperty("sourceId").GetGuid();

        var approveSourceResp = await client.PostAsJsonAsync(
            $"/api/admin/resource-sources/{sourceId}/approve", new { reason = "cleared for test" });
        Assert.Equal(HttpStatusCode.OK, approveSourceResp.StatusCode);

        // Judgment call: the word must be unique across the whole test run — ResourceCandidateValidationService's
        // dedup gate checks content fingerprints globally (not just within this source/run), and
        // other tests in this file/class also import a plain "hello" row. A collision there would
        // add a "Duplicate" warning, forcing NeedsReview instead of Passed.
        var uniqueWord = $"contentpipelineword{Guid.NewGuid():N}";
        using var form = new MultipartFormDataContent
        {
            { new StringContent(sourceId.ToString()), "sourceId" },
            { new StringContent("Csv"), "importMode" },
        };
        // Column must be named "cefrLevel" (ResourceImportService.CefrLevelField), not "cefr" —
        // a plain "cefr" column is not recognized and CefrLevel is required to publish a
        // VocabularyEntry candidate.
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes($"word,cefrLevel\n{uniqueWord},A1\n")), "file", "vocab.csv");

        var importResp = await client.PostAsync("/api/admin/resource-import-runs", form);
        Assert.Equal(HttpStatusCode.OK, importResp.StatusCode);
        var importBody = await importResp.Content.ReadFromJsonAsync<JsonElement>();
        var runId = importBody.GetProperty("runId").GetGuid();

        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?importRunId={runId}");
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        var candidateId = candidatesBody.GetProperty("items")[0].GetProperty("candidateId").GetGuid();

        // Deterministic re-validation only (no AI call) — should pass cleanly for this row.
        var validateResp = await client.PostAsJsonAsync($"/api/admin/resource-candidates/{candidateId}/validate", new { });
        Assert.Equal(HttpStatusCode.OK, validateResp.StatusCode);
        var validateBody = await validateResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Passed", validateBody.GetProperty("status").GetString());

        // The candidate has NOT been approved yet — approve-and-publish must do both in one call.
        var approveAndPublishResp = await client.PostAsJsonAsync(
            $"/api/admin/resource-candidates/{candidateId}/approve-and-publish", new { });
        Assert.Equal(HttpStatusCode.OK, approveAndPublishResp.StatusCode);
        var publishBody = await approveAndPublishResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(publishBody.GetProperty("success").GetBoolean());
        Assert.Equal("CefrVocabularyEntry", publishBody.GetProperty("publishedEntityType").GetString());
        Assert.NotEqual(Guid.Empty, publishBody.GetProperty("publishedEntityId").GetGuid());

        var candidateResp = await client.GetAsync($"/api/admin/resource-candidates/{candidateId}");
        Assert.Equal(HttpStatusCode.OK, candidateResp.StatusCode);
        var candidateBody = await candidateResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", candidateBody.GetProperty("reviewStatus").GetString());
        Assert.True(candidateBody.GetProperty("isPublished").GetBoolean());
    }
}
