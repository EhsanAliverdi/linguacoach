using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 4.4D (2026-07-16 real audio measurement deferred; AI operation ledger delivered) — real
/// API-level integration coverage for the read-only AI enrichment operation summary
/// (GET .../plan/{planId}/ai-operations) and for STT + AI costs both contributing to the package's
/// single durable accrued-cost total. A real/fake AI provider call is deliberately not exercised
/// here — no fake AI provider is substituted in the API test host (unlike STT's
/// FakeSpeechToTextService), so the actual provider-call/reuse/ceiling critical proofs are covered
/// at the unit level (ResourceCandidateAnalysisServiceTests, ImportAiEnrichmentOperationLedgerTests)
/// using a fake IAiProvider — never a real paid call, per the phase brief.
/// </summary>
public sealed class ImportAiEnrichmentOperationSummaryTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ImportAiEnrichmentOperationSummaryTests(ApiTestFactory factory) => _factory = factory;

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

    private async Task<(Guid PackageId, Guid PlanId)> CreateApprovedPlanAsync(HttpClient client, string sourceName)
    {
        var sourceId = await CreateApprovedSourceAsync(client, sourceName);
        var csvBytes = System.Text.Encoding.UTF8.GetBytes("word\r\nfoo\r\n");

        using var form = new MultipartFormDataContent { { new StringContent(sourceId.ToString()), "cefrResourceSourceId" } };
        var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "files", "data.csv");
        var submitResp = await client.PostAsync("/api/admin/import-packages/submit", form);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var packageId = submitBody.GetProperty("importPackageId").GetGuid();

        var planResp = await client.PostAsJsonAsync($"/api/admin/import-packages/{packageId}/plan", new { });
        Assert.Equal(HttpStatusCode.OK, planResp.StatusCode);
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        var planId = planBody.GetProperty("planId").GetGuid();
        var concurrencyStamp = planBody.GetProperty("concurrencyStamp").GetGuid();

        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/approve",
            new { approvedCostCeiling = 100m, expectedConcurrencyStamp = concurrencyStamp });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        return (packageId, planId);
    }

    /// <summary>Seeds one real ResourceCandidate (FK target) plus a Succeeded and a Failed
    /// ImportAiEnrichmentOperation row directly, mirroring ImportSttOperationSummaryTests' pattern
    /// for the equivalent read-path proof (no fake AI provider is available to drive this through
    /// the real processing pipeline in this test host).</summary>
    private async Task<Guid> SeedOperationsAsync(Guid packageId, Guid planId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var package = await db.ImportPackages.FirstAsync(p => p.Id == packageId);
        var run = new ResourceImportRun(package.CefrResourceSourceId, ResourceImportMode.Csv, "data.csv", "hash", DateTimeOffset.UtcNow, importPackageId: packageId);
        db.ResourceImportRuns.Add(run);
        await db.SaveChangesAsync();

        var raw = new ResourceRawRecord(run.Id, "rawhash-seed", "en", "row", rawJson: """{"word":"foo"}""");
        raw.MarkParsed();
        db.ResourceRawRecords.Add(raw);
        await db.SaveChangesAsync();

        var fingerprint = new LinguaCoach.Infrastructure.Activity.ActivityContentFingerprintService().ComputeFingerprint(
            new LinguaCoach.Application.Activity.ActivityContentFingerprintRequest(
                """{"word":"foo"}""", LinguaCoach.Application.Activity.ActivityContentShape.Unknown, null, "foo"));
        var candidate = new ResourceCandidate(
            raw.Id, ResourceCandidateType.VocabularyEntry, "foo", """{"word":"foo"}""", "en",
            "foo", fingerprint, ResourceCandidateValidationStatus.NeedsReview);
        db.ResourceCandidates.Add(candidate);
        await db.SaveChangesAsync();

        var succeeded = new ImportAiEnrichmentOperation(
            packageId, planId, candidate.Id, $"{packageId:N}:{candidate.Id:N}:rawhash-seed:openai:gpt-4o-mini:resource_candidate_analyze:FullAiAssisted",
            "candidate_enrich", "openai", "resource_candidate_analyze", "FullAiAssisted", DateTimeOffset.UtcNow);
        succeeded.MarkSucceeded("""{"cefrLevel":"A1"}""", 0.02m, "USD", 100, 50, 0.01m, 0.03m, "gpt-4o-mini", DateTimeOffset.UtcNow);
        db.ImportAiEnrichmentOperations.Add(succeeded);

        var failed = new ImportAiEnrichmentOperation(
            packageId, planId, candidate.Id, $"{packageId:N}:{candidate.Id:N}:rawhash-seed-2:openai:gpt-4o-mini:resource_candidate_analyze:FullAiAssisted",
            "candidate_enrich", "openai", "resource_candidate_analyze", "FullAiAssisted", DateTimeOffset.UtcNow);
        failed.MarkFailed("AI response could not be parsed after retry.", DateTimeOffset.UtcNow);
        db.ImportAiEnrichmentOperations.Add(failed);

        // Critical proof #11 — STT and AI costs both contribute to the same durable total.
        package.AccrueCost(0.03m, "USD"); // simulated STT accrual
        package.AccrueCost(0.02m, "USD"); // AI accrual (matches the seeded succeeded operation)
        await db.SaveChangesAsync();

        return candidate.Id;
    }

    [Fact]
    public async Task Ai_operation_summary_returns_safe_fields_for_succeeded_and_failed_operations()
    {
        var client = await AdminClientAsync();
        var (packageId, planId) = await CreateApprovedPlanAsync(client, $"AI Ops Summary Source {Guid.NewGuid():N}");
        await SeedOperationsAsync(packageId, planId);

        var resp = await client.GetAsync($"/api/admin/import-packages/{packageId}/plan/{planId}/ai-operations");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var rows = (await resp.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();

        Assert.Equal(2, rows.Count);

        var succeededRow = rows.Single(r => r.GetProperty("status").GetString() == "Succeeded");
        Assert.Equal("openai", succeededRow.GetProperty("providerName").GetString());
        Assert.Equal("gpt-4o-mini", succeededRow.GetProperty("modelName").GetString());
        Assert.True(succeededRow.GetProperty("resultReusable").GetBoolean());
        Assert.Equal(100, succeededRow.GetProperty("inputTokens").GetInt32());
        Assert.Equal(50, succeededRow.GetProperty("outputTokens").GetInt32());
        Assert.Equal(0.02m, succeededRow.GetProperty("calculatedCost").GetDecimal());
        Assert.False(succeededRow.TryGetProperty("resultReferenceJson", out _)); // never exposed
        Assert.False(succeededRow.TryGetProperty("apiKey", out _));

        var failedRow = rows.Single(r => r.GetProperty("status").GetString() == "Failed");
        Assert.False(failedRow.GetProperty("resultReusable").GetBoolean());
        Assert.Equal("AI response could not be parsed after retry.", failedRow.GetProperty("safeErrorMessage").GetString());
        Assert.Equal(JsonValueKind.Null, failedRow.GetProperty("calculatedCost").ValueKind);
    }

    [Fact]
    public async Task Stt_and_AI_costs_both_contribute_to_the_packages_single_accrued_cost_total()
    {
        var client = await AdminClientAsync();
        var (packageId, planId) = await CreateApprovedPlanAsync(client, $"Combined Cost Source {Guid.NewGuid():N}");
        await SeedOperationsAsync(packageId, planId);

        var planResp = await client.GetAsync($"/api/admin/import-packages/{packageId}/plan");
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0.05m, planBody.GetProperty("accruedCost").GetDecimal()); // 0.03 (STT) + 0.02 (AI)
        Assert.Equal("USD", planBody.GetProperty("accruedCostCurrency").GetString());
    }

    [Fact]
    public async Task Ai_operation_summary_is_empty_for_a_plan_with_no_operations_yet()
    {
        var client = await AdminClientAsync();
        var (packageId, planId) = await CreateApprovedPlanAsync(client, $"AI Ops Empty Source {Guid.NewGuid():N}");

        var resp = await client.GetAsync($"/api/admin/import-packages/{packageId}/plan/{planId}/ai-operations");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var rows = (await resp.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Ai_operation_summary_returns_not_found_for_an_unknown_plan()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"AI Ops Unknown Plan Source {Guid.NewGuid():N}");

        using var form = new MultipartFormDataContent { { new StringContent(sourceId.ToString()), "cefrResourceSourceId" } };
        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("word\r\nfoo\r\n"));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "files", "data.csv");
        var submitResp = await client.PostAsync("/api/admin/import-packages/submit", form);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var packageId = submitBody.GetProperty("importPackageId").GetGuid();

        var resp = await client.GetAsync($"/api/admin/import-packages/{packageId}/plan/{Guid.NewGuid()}/ai-operations");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
