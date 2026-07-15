using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 4.4 (2026-07-16 editable import plans and durable cost accounting) — real API-level
/// integration coverage for the three critical proofs:
///
///   1. Admin-edited plan → exact approved revision → changed candidate output.
///   2. Successful STT → simulated retry → no second provider call, no duplicate cost.
///   3. Persisted accrued cost + next-operation estimate > approved ceiling → provider not called,
///      package pauses safely.
///
/// Uses real EF Core persistence and the real background processing service; only the STT
/// provider is faked (the test host's OpenAiSpeechToTextService.IsSupported is false with no API
/// key configured, so ISpeechToTextService already resolves to FakeSpeechToTextService).
/// </summary>
public sealed class ImportPlanEditingAndCostAccountingTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ImportPlanEditingAndCostAccountingTests(ApiTestFactory factory) => _factory = factory;

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

    /// <summary>Runs one processing pass in its own DI scope and returns that scope's
    /// FakeSpeechToTextService instance, so the test can assert exactly how many times the
    /// provider was called during THIS pass (not cumulatively — a fresh Fake is scoped per call,
    /// matching the app's real scoped registration).</summary>
    private async Task<LinguaCoach.Infrastructure.Speaking.FakeSpeechToTextService> RunPackageProcessingAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var processingService = scope.ServiceProvider.GetRequiredService<IImportPackageProcessingService>();
        await processingService.ProcessPendingAsync(maxPackages: 10);
        return scope.ServiceProvider.GetRequiredService<LinguaCoach.Infrastructure.Speaking.FakeSpeechToTextService>();
    }

    private async Task<(Guid PackageId, Guid PlanId, Guid ConcurrencyStamp)> SubmitAndGeneratePlanAsync(
        HttpClient client, Guid sourceId, byte[] fileBytes, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(sourceId.ToString()), "cefrResourceSourceId" },
        };
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "files", fileName);

        var submitResp = await client.PostAsync("/api/admin/import-packages/submit", form);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var packageId = submitBody.GetProperty("importPackageId").GetGuid();

        var planResp = await client.PostAsJsonAsync($"/api/admin/import-packages/{packageId}/plan", new { });
        Assert.Equal(HttpStatusCode.OK, planResp.StatusCode);
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        var planId = planBody.GetProperty("planId").GetGuid();
        var concurrencyStamp = planBody.GetProperty("concurrencyStamp").GetGuid();

        return (packageId, planId, concurrencyStamp);
    }

    /// <summary>Critical proof #1: an admin edit made through the real PUT draft-update endpoint
    /// is what execution actually follows — not a direct-DB bypass, not what generation originally
    /// proposed.</summary>
    [Fact]
    public async Task Admin_edited_plan_through_the_draft_API_produces_the_edited_candidate_output()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"Draft Edit Source {Guid.NewGuid():N}");
        var marker = $"editedmarker{Guid.NewGuid():N}";
        var csvBytes = Encoding.UTF8.GetBytes($"mystery1\r\n{marker}\r\n");

        var (packageId, planId, concurrencyStamp) = await SubmitAndGeneratePlanAsync(
            client, sourceId, csvBytes, "data.csv", "text/csv");

        // Edit the draft through the real API — map the unrecognized "mystery1" column to "word"
        // and force the VocabularyEntry route, neither of which deterministic clustering would
        // have proposed for a column named "mystery1".
        var editedInstructions = new[]
        {
            new ImportExecutionGroupInstruction(
                "(root)", true, ResourceCandidateType.VocabularyEntry,
                new Dictionary<string, string> { ["mystery1"] = "word" }, Array.Empty<string>()),
        };
        var updateResp = await client.PutAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}",
            new { expectedConcurrencyStamp = concurrencyStamp, groupInstructions = editedInstructions });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updateBody = await updateResp.Content.ReadFromJsonAsync<JsonElement>();
        var updatedStamp = updateBody.GetProperty("concurrencyStamp").GetGuid();
        Assert.NotEqual(concurrencyStamp, updatedStamp);

        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/approve",
            new { approvedCostCeiling = 100m, expectedConcurrencyStamp = updatedStamp });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        await RunPackageProcessingAsync();

        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?sourceId={sourceId}&pageSize=50");
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = candidatesBody.GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(items, i =>
            i.GetProperty("canonicalText").GetString() == marker
            && i.GetProperty("candidateType").GetString() == nameof(ResourceCandidateType.VocabularyEntry));
    }

    /// <summary>Stale concurrency is rejected with 409, per Workstream A5.</summary>
    [Fact]
    public async Task Stale_concurrency_stamp_on_draft_update_returns_conflict()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"Stale Concurrency Source {Guid.NewGuid():N}");
        var csvBytes = Encoding.UTF8.GetBytes("mystery1\r\nfoo\r\n");
        var (packageId, planId, concurrencyStamp) = await SubmitAndGeneratePlanAsync(
            client, sourceId, csvBytes, "data.csv", "text/csv");

        var instructions = new[]
        {
            new ImportExecutionGroupInstruction("(root)", true, ResourceCandidateType.VocabularyEntry,
                new Dictionary<string, string> { ["mystery1"] = "word" }, Array.Empty<string>()),
        };

        // First edit succeeds and moves the stamp forward.
        var firstResp = await client.PutAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}",
            new { expectedConcurrencyStamp = concurrencyStamp, groupInstructions = instructions });
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);

        // Second edit using the original (now-stale) stamp must conflict.
        var staleResp = await client.PutAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}",
            new { expectedConcurrencyStamp = concurrencyStamp, groupInstructions = instructions });
        Assert.Equal(HttpStatusCode.Conflict, staleResp.StatusCode);
    }

    /// <summary>Critical proof #2: a retry after a durably-saved STT success must not call the
    /// provider again or accrue cost again. The "retry" is simulated by resetting the package's
    /// stage checkpoint and the audio asset's processing state back to a pre-completion state —
    /// exactly the state a crash between the ledger/cost save (Phase 4.4's tightened save point)
    /// and the later stage checkpoint save would leave behind — while the STT ledger row and the
    /// package's accrued cost (already durably saved together) are left untouched.</summary>
    [Fact]
    public async Task Retry_after_a_successful_STT_operation_does_not_call_the_provider_again_or_double_charge()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"STT Retry Source {Guid.NewGuid():N}");
        // Flat filename (no folder) — a loose-file submission's synthetic manifest always uses a
        // single root folder group (see ImportPackageSubmissionService), so a filename with a "/"
        // would land its asset in a folder group the manifest never actually declared.
        var (packageId, planId, concurrencyStamp) = await SubmitAndGeneratePlanAsync(
            client, sourceId, new byte[100], "audio.mp3", "audio/mpeg");

        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/approve",
            new { approvedCostCeiling = 100m, expectedConcurrencyStamp = concurrencyStamp });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        var firstPassStt = await RunPackageProcessingAsync();
        Assert.Equal(1, firstPassStt.CallCount);

        decimal accruedAfterFirstPass;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var package = await db.ImportPackages.FirstAsync(p => p.Id == packageId);
            accruedAfterFirstPass = package.AccruedCost;
            Assert.True(accruedAfterFirstPass > 0);

            var sttOps = await db.ImportSttOperations.Where(o => o.ImportPackageId == packageId).ToListAsync();
            Assert.Single(sttOps);
            Assert.Equal(ImportSttOperationStatus.Succeeded, sttOps[0].Status);

            // Simulate the crash window: stage/asset progress not yet checkpointed, but the STT
            // ledger row + accrued cost (saved together, immediately after the provider call)
            // already are.
            var asset = await db.ImportAssets.FirstAsync(a => a.ImportPackageId == packageId);
            db.Entry(asset).Property(nameof(ImportAsset.ProcessingState)).CurrentValue = ImportAssetProcessingState.Inspected;
            package.MoveToStatus(ImportPackageStatus.CreatingCandidates);
            db.Entry(package).Property(nameof(ImportPackage.LastCompletedStageIndex)).CurrentValue = 0;
            await db.SaveChangesAsync();
        }

        var secondPassStt = await RunPackageProcessingAsync();
        Assert.Equal(0, secondPassStt.CallCount); // the provider must NOT be called again

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var package = await db.ImportPackages.FirstAsync(p => p.Id == packageId);
            Assert.Equal(accruedAfterFirstPass, package.AccruedCost); // not doubled

            var sttOps = await db.ImportSttOperations.Where(o => o.ImportPackageId == packageId).ToListAsync();
            Assert.Single(sttOps); // still exactly one ledger row for this logical operation
        }
    }

    /// <summary>Critical proof #3: the cost ceiling is checked using persisted accrued cost before
    /// the provider is ever called — the second of two audio files must never reach the STT
    /// provider once the ceiling is exhausted by the first.</summary>
    [Fact]
    public async Task Ceiling_blocks_the_provider_call_before_it_would_be_exceeded_using_persisted_cost()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"Ceiling Source {Guid.NewGuid():N}");

        using var form = new MultipartFormDataContent
        {
            { new StringContent(sourceId.ToString()), "cefrResourceSourceId" },
        };
        // Flat filenames — see the retry test's comment on why a loose-file submission's assets
        // must stay in the synthetic single root folder group.
        var audio1 = new ByteArrayContent(new byte[100]);
        audio1.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
        form.Add(audio1, "files", "audio1.mp3");
        var audio2 = new ByteArrayContent(new byte[100]);
        audio2.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
        form.Add(audio2, "files", "audio2.mp3");

        var submitResp = await client.PostAsync("/api/admin/import-packages/submit", form);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var packageId = submitBody.GetProperty("importPackageId").GetGuid();

        var planResp = await client.PostAsJsonAsync($"/api/admin/import-packages/{packageId}/plan", new { });
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        var planId = planBody.GetProperty("planId").GetGuid();
        var concurrencyStamp = planBody.GetProperty("concurrencyStamp").GetGuid();

        // One STT call costs 5 minutes * $0.006/minute = $0.03. A ceiling just above that covers
        // exactly one call and must block the second.
        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/approve",
            new { approvedCostCeiling = 0.032m, expectedConcurrencyStamp = concurrencyStamp });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        var sttFake = await RunPackageProcessingAsync();

        Assert.Equal(1, sttFake.CallCount); // only the first audio file reached the provider

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var package = await db.ImportPackages.FirstAsync(p => p.Id == packageId);
        Assert.Equal(ImportPackageStatus.AwaitingMappingApproval, package.Status);
        Assert.True(package.AccruedCost > 0); // the completed first call's cost was preserved

        var plan = await db.ImportProfiles.FirstAsync(p => p.Id == planId);
        Assert.Equal(ImportProfileStatus.PausedForCostApproval, plan.Status);
        Assert.False(string.IsNullOrWhiteSpace(plan.PauseReason));
    }

    /// <summary>TODO-4.4-LOOSE-FILE-FOLDER-BUG regression: a loose-file submission's synthetic
    /// manifest always declares a single "(root)" folder group. A client-supplied file name
    /// containing a directory separator (never produced by a real browser file input, but
    /// reachable via a directly-crafted API call) must not be allowed to resolve to a folder
    /// group the manifest never declared — the asset must be flattened into "(root)" so plan
    /// generation, editing, preview, and execution all agree on which group governs it.</summary>
    [Fact]
    public async Task Loose_file_with_directory_separator_in_name_is_flattened_to_the_root_group()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"Loose File Folder Bug Source {Guid.NewGuid():N}");
        var marker = $"flattenmarker{Guid.NewGuid():N}";
        var csvBytes = Encoding.UTF8.GetBytes($"mystery1\r\n{marker}\r\n");

        using var form = new MultipartFormDataContent
        {
            { new StringContent(sourceId.ToString()), "cefrResourceSourceId" },
        };
        var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        // Deliberately crafted with a directory component — this is what a direct API call
        // (bypassing the browser file input, which never sends one) could send.
        form.Add(fileContent, "files", "lesson/data.csv");

        var submitResp = await client.PostAsync("/api/admin/import-packages/submit", form);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var packageId = submitBody.GetProperty("importPackageId").GetGuid();

        var manifestResp = await client.GetAsync($"/api/admin/import-packages/{packageId}/manifest");
        var manifestBody = await manifestResp.Content.ReadFromJsonAsync<JsonElement>();
        var folderGroups = manifestBody.GetProperty("folderGroups").EnumerateArray().ToList();
        Assert.Single(folderGroups);
        Assert.Equal(string.Empty, folderGroups[0].GetProperty("folderPath").GetString());

        var planResp = await client.PostAsJsonAsync($"/api/admin/import-packages/{packageId}/plan", new { });
        Assert.Equal(HttpStatusCode.OK, planResp.StatusCode);
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        var planId = planBody.GetProperty("planId").GetGuid();
        var concurrencyStamp = planBody.GetProperty("concurrencyStamp").GetGuid();
        var groups = planBody.GetProperty("groupInstructions").EnumerateArray().ToList();
        Assert.Single(groups);
        Assert.Equal("(root)", groups[0].GetProperty("groupKey").GetString());

        var editedInstructions = new[]
        {
            new ImportExecutionGroupInstruction(
                "(root)", true, ResourceCandidateType.VocabularyEntry,
                new Dictionary<string, string> { ["mystery1"] = "word" }, Array.Empty<string>()),
        };
        var updateResp = await client.PutAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}",
            new { expectedConcurrencyStamp = concurrencyStamp, groupInstructions = editedInstructions });
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updatedStamp = (await updateResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("concurrencyStamp").GetGuid();

        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/approve",
            new { approvedCostCeiling = 100m, expectedConcurrencyStamp = updatedStamp });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        await RunPackageProcessingAsync();

        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?sourceId={sourceId}&pageSize=50");
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = candidatesBody.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("canonicalText").GetString() == marker);
    }
}
