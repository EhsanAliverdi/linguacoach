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
/// Phase 4.4C (2026-07-16 cost path cleanup and STT operation visibility) — real API-level
/// integration coverage for:
///   1. GET .../plan/{planId}/stt-operations returns package/plan-scoped, safe summaries.
///   2. The removed unaudited approve-revised-ceiling route no longer exists.
/// </summary>
public sealed class ImportSttOperationSummaryTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ImportSttOperationSummaryTests(ApiTestFactory factory) => _factory = factory;

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

    private async Task RunPackageProcessingAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var processingService = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.ResourceImport.IImportPackageProcessingService>();
        await processingService.ProcessPendingAsync(maxPackages: 10);
    }

    /// <summary>Submits one loose audio file, approves, and runs one processing pass so exactly
    /// one Succeeded ImportSttOperation exists.</summary>
    private async Task<(Guid PackageId, Guid PlanId)> CreateApprovedPlanWithOneSttOperationAsync(HttpClient client, string sourceName)
    {
        var sourceId = await CreateApprovedSourceAsync(client, sourceName);

        using var form = new MultipartFormDataContent { { new StringContent(sourceId.ToString()), "cefrResourceSourceId" } };
        var audio = new ByteArrayContent(new byte[100]);
        audio.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
        form.Add(audio, "files", "audio.mp3");

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

        await RunPackageProcessingAsync();

        return (packageId, planId);
    }

    [Fact]
    public async Task The_old_unaudited_approve_revised_ceiling_route_no_longer_exists()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"Removed Route Source {Guid.NewGuid():N}");

        using var form = new MultipartFormDataContent { { new StringContent(sourceId.ToString()), "cefrResourceSourceId" } };
        var audio = new ByteArrayContent(new byte[100]);
        audio.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
        form.Add(audio, "files", "audio.mp3");
        var submitResp = await client.PostAsync("/api/admin/import-packages/submit", form);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var packageId = submitBody.GetProperty("importPackageId").GetGuid();

        var planResp = await client.PostAsJsonAsync($"/api/admin/import-packages/{packageId}/plan", new { });
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        var planId = planBody.GetProperty("planId").GetGuid();

        var resp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/approve-revised-ceiling",
            new { approvedCostCeiling = 100m, expectedConcurrencyStamp = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode); // ASP.NET routing: no matching endpoint
    }

    [Fact]
    public async Task Stt_summary_returns_provider_model_cost_attempts_and_reused_state()
    {
        var client = await AdminClientAsync();
        var (packageId, planId) = await CreateApprovedPlanWithOneSttOperationAsync(client, $"STT Summary Source {Guid.NewGuid():N}");

        var resp = await client.GetAsync($"/api/admin/import-packages/{packageId}/plan/{planId}/stt-operations");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var rows = (await resp.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal("audio.mp3", row.GetProperty("assetFileName").GetString());
        Assert.Equal("openai", row.GetProperty("providerName").GetString()); // ImportPackageProcessingService's assumed STT provider name
        Assert.Equal("Succeeded", row.GetProperty("status").GetString());
        Assert.Equal(1, row.GetProperty("attemptNumber").GetInt32());
        Assert.True(row.GetProperty("resultReusable").GetBoolean());
        Assert.True(row.GetProperty("calculatedCost").GetDecimal() > 0);
        Assert.Equal("USD", row.GetProperty("currency").GetString());
        Assert.False(string.IsNullOrEmpty(row.GetProperty("startedAtUtc").GetString()));
        Assert.False(string.IsNullOrEmpty(row.GetProperty("completedAtUtc").GetString()));

        // No transcript content, no provider credentials — only the safe summary fields.
        Assert.False(row.TryGetProperty("transcriptText", out _));
        Assert.False(row.TryGetProperty("apiKey", out _));
    }

    [Fact]
    public async Task Reused_STT_operation_after_retry_still_reports_a_single_attempt_and_no_double_charge()
    {
        var client = await AdminClientAsync();
        var (packageId, planId) = await CreateApprovedPlanWithOneSttOperationAsync(client, $"STT Reuse Source {Guid.NewGuid():N}");

        decimal firstCost;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var op = await db.ImportSttOperations.SingleAsync(o => o.ImportPackageId == packageId);
            firstCost = op.CalculatedCost!.Value;

            // Simulate the crash-recovery window a real retry would hit (see
            // ImportPlanEditingAndCostAccountingTests' retry test for the full rationale).
            var package = await db.ImportPackages.FirstAsync(p => p.Id == packageId);
            var asset = await db.ImportAssets.FirstAsync(a => a.ImportPackageId == packageId);
            db.Entry(asset).Property(nameof(ImportAsset.ProcessingState)).CurrentValue = ImportAssetProcessingState.Inspected;
            package.MoveToStatus(LinguaCoach.Domain.Enums.ImportPackageStatus.CreatingCandidates);
            db.Entry(package).Property(nameof(ImportPackage.LastCompletedStageIndex)).CurrentValue = 0;
            await db.SaveChangesAsync();
        }

        await RunPackageProcessingAsync(); // the "retry"

        var resp = await client.GetAsync($"/api/admin/import-packages/{packageId}/plan/{planId}/stt-operations");
        var rows = (await resp.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();

        Assert.Single(rows); // still exactly one ledger row — reused, not duplicated
        Assert.Equal(1, rows[0].GetProperty("attemptNumber").GetInt32());
        Assert.True(rows[0].GetProperty("resultReusable").GetBoolean());
        Assert.Equal(firstCost, rows[0].GetProperty("calculatedCost").GetDecimal()); // cost unchanged, not doubled
    }

    [Fact]
    public async Task Failed_STT_operation_exposes_a_safe_error_message()
    {
        var client = await AdminClientAsync();
        var (packageId, planId) = await CreateApprovedPlanWithOneSttOperationAsync(client, $"STT Failed Source {Guid.NewGuid():N}");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var op = await db.ImportSttOperations.SingleAsync(o => o.ImportPackageId == packageId);
            // Directly exercise the failed-state read path (write-side failure handling is already
            // covered by ImportPlanEditingAndCostAccountingTests) — construct a second, independent
            // failed operation to verify the summary query surfaces FailureReason safely.
            var failed = new ImportSttOperation(
                packageId, planId, op.ImportAssetId, $"{op.LogicalOperationKey}-failed-test",
                "Fake", 5m, DateTimeOffset.UtcNow);
            failed.MarkFailed("STT provider returned no transcript.", DateTimeOffset.UtcNow);
            db.ImportSttOperations.Add(failed);
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync($"/api/admin/import-packages/{packageId}/plan/{planId}/stt-operations");
        var rows = (await resp.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var failedRow = rows.Single(r => r.GetProperty("status").GetString() == "Failed");

        Assert.Equal("STT provider returned no transcript.", failedRow.GetProperty("safeErrorMessage").GetString());
        Assert.False(failedRow.GetProperty("resultReusable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, failedRow.GetProperty("calculatedCost").ValueKind); // no misleading cost on failure
    }

    [Fact]
    public async Task Stt_summary_is_scoped_to_the_requested_package_and_plan_not_a_different_one()
    {
        var client = await AdminClientAsync();
        var (packageA, planA) = await CreateApprovedPlanWithOneSttOperationAsync(client, $"STT Scope A Source {Guid.NewGuid():N}");
        var (packageB, planB) = await CreateApprovedPlanWithOneSttOperationAsync(client, $"STT Scope B Source {Guid.NewGuid():N}");

        var respA = await client.GetAsync($"/api/admin/import-packages/{packageA}/plan/{planA}/stt-operations");
        var rowsA = (await respA.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Single(rowsA);

        // Requesting package B's plan against package A's id must not return package B's operation.
        var crossResp = await client.GetAsync($"/api/admin/import-packages/{packageA}/plan/{planB}/stt-operations");
        Assert.Equal(HttpStatusCode.NotFound, crossResp.StatusCode);
    }

    [Fact]
    public async Task Stt_summary_returns_not_found_for_an_unknown_plan()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"STT Unknown Plan Source {Guid.NewGuid():N}");

        using var form = new MultipartFormDataContent { { new StringContent(sourceId.ToString()), "cefrResourceSourceId" } };
        var audio = new ByteArrayContent(new byte[100]);
        audio.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
        form.Add(audio, "files", "audio.mp3");
        var submitResp = await client.PostAsync("/api/admin/import-packages/submit", form);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var packageId = submitBody.GetProperty("importPackageId").GetGuid();

        var resp = await client.GetAsync($"/api/admin/import-packages/{packageId}/plan/{Guid.NewGuid()}/stt-operations");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
