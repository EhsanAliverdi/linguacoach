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
/// Phase 4.4B (2026-07-16 audited cost ceiling amendment) — real API-level integration coverage
/// for the audited "cost ceiling may only be increased through an explicit audited admin action"
/// workflow: POST .../plan/{planId}/amend-ceiling.
/// </summary>
public sealed class ImportCostCeilingAmendmentTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ImportCostCeilingAmendmentTests(ApiTestFactory factory) => _factory = factory;

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

    /// <summary>Submits two loose audio files, approves with a ceiling that only covers the first,
    /// runs one processing pass so the plan pauses for cost, and returns everything a subsequent
    /// amendment call needs.</summary>
    private async Task<(Guid PackageId, Guid PlanId, Guid ConcurrencyStamp, decimal PausedCeiling)> CreatePausedForCostPlanAsync(HttpClient client, string sourceName)
    {
        var sourceId = await CreateApprovedSourceAsync(client, sourceName);

        using var form = new MultipartFormDataContent
        {
            { new StringContent(sourceId.ToString()), "cefrResourceSourceId" },
        };
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
        Assert.Equal(HttpStatusCode.OK, planResp.StatusCode);
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        var planId = planBody.GetProperty("planId").GetGuid();
        var concurrencyStamp = planBody.GetProperty("concurrencyStamp").GetGuid();

        const decimal pausedCeiling = 0.032m; // covers exactly one $0.03 STT call, blocks the second
        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/approve",
            new { approvedCostCeiling = pausedCeiling, expectedConcurrencyStamp = concurrencyStamp });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        await RunPackageProcessingAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var plan = await db.ImportProfiles.FirstAsync(p => p.Id == planId);
        Assert.Equal(ImportProfileStatus.PausedForCostApproval, plan.Status);

        return (packageId, planId, plan.ConcurrencyStamp, pausedCeiling);
    }

    [Fact]
    public async Task New_ceiling_must_be_greater_than_the_current_ceiling()
    {
        var client = await AdminClientAsync();
        var (packageId, planId, stamp, pausedCeiling) = await CreatePausedForCostPlanAsync(client, $"Ceiling Amend Lower Source {Guid.NewGuid():N}");

        var resp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/amend-ceiling",
            new { expectedConcurrencyStamp = stamp, newApprovedCostCeiling = pausedCeiling, reason = "not actually higher" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Amendment_is_rejected_unless_the_package_is_paused_for_cost()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"Ceiling Amend Not Paused Source {Guid.NewGuid():N}");
        var csvBytes = System.Text.Encoding.UTF8.GetBytes("word\r\nfoo\r\n");

        using var form = new MultipartFormDataContent { { new StringContent(sourceId.ToString()), "cefrResourceSourceId" } };
        var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "files", "data.csv");
        var submitResp = await client.PostAsync("/api/admin/import-packages/submit", form);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var packageId = submitBody.GetProperty("importPackageId").GetGuid();

        var planResp = await client.PostAsJsonAsync($"/api/admin/import-packages/{packageId}/plan", new { });
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        var planId = planBody.GetProperty("planId").GetGuid();
        var stamp = planBody.GetProperty("concurrencyStamp").GetGuid();

        // Still AwaitingApproval — never paused for cost — so an amendment must be rejected.
        var resp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/amend-ceiling",
            new { expectedConcurrencyStamp = stamp, newApprovedCostCeiling = 100m, reason = "trying anyway" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Stale_amendment_returns_conflict()
    {
        var client = await AdminClientAsync();
        var (packageId, planId, staleStamp, pausedCeiling) = await CreatePausedForCostPlanAsync(client, $"Ceiling Amend Stale Source {Guid.NewGuid():N}");

        // First amendment succeeds and moves the stamp forward.
        var firstResp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/amend-ceiling",
            new { expectedConcurrencyStamp = staleStamp, newApprovedCostCeiling = pausedCeiling + 1m, reason = "first amendment" });
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);

        // Second amendment reusing the now-stale stamp must conflict, not silently overwrite.
        var staleResp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/amend-ceiling",
            new { expectedConcurrencyStamp = staleStamp, newApprovedCostCeiling = pausedCeiling + 2m, reason = "stale second attempt" });
        Assert.Equal(HttpStatusCode.Conflict, staleResp.StatusCode);
    }

    /// <summary>Proves acceptance criterion 8 ("two concurrent amendments cannot both succeed"):
    /// both requests are built against the same originally-read concurrency stamp — exactly what
    /// two admins racing from the same loaded page would send — so only the first can possibly
    /// win and the second must conflict rather than silently overwrite it. Issued sequentially
    /// rather than via genuinely simultaneous requests: the integration test host's SQLite
    /// in-memory connection does not support two concurrent transactions on the same connection
    /// (a test-harness limitation, not a production one — Postgres gives each request its own
    /// pooled connection), so real parallel dispatch here produces a spurious
    /// "SqliteConnection does not support nested transactions" error rather than exercising the
    /// actual race. The concurrency guarantee itself — EF's <c>IsConcurrencyToken()</c> on
    /// <c>ImportProfile.ConcurrencyStamp</c>, which makes the second writer's UPDATE affect zero
    /// rows once the first has committed — does not depend on request timing to hold.</summary>
    [Fact]
    public async Task Two_amendments_built_from_the_same_stamp_cannot_both_succeed()
    {
        var client = await AdminClientAsync();
        var (packageId, planId, stamp, pausedCeiling) = await CreatePausedForCostPlanAsync(client, $"Ceiling Amend Concurrent Source {Guid.NewGuid():N}");

        var first = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/amend-ceiling",
            new { expectedConcurrencyStamp = stamp, newApprovedCostCeiling = pausedCeiling + 1m, reason = "racer A" });
        var second = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/amend-ceiling",
            new { expectedConcurrencyStamp = stamp, newApprovedCostCeiling = pausedCeiling + 2m, reason = "racer B" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var amendments = await db.ImportCostCeilingAmendments.Where(a => a.ImportProfileId == planId).ToListAsync();
        Assert.Single(amendments); // exactly one amendment audit row was ever created
    }

    [Fact]
    public async Task Amendment_preserves_previous_and_new_ceilings_actor_reason_and_currency_in_audit_history()
    {
        var client = await AdminClientAsync();
        var (packageId, planId, stamp, pausedCeiling) = await CreatePausedForCostPlanAsync(client, $"Ceiling Amend Audit Source {Guid.NewGuid():N}");
        var newCeiling = pausedCeiling + 5m;

        var resp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/amend-ceiling",
            new { expectedConcurrencyStamp = stamp, newApprovedCostCeiling = newCeiling, reason = "customer requested more content" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var amendment = await db.ImportCostCeilingAmendments.SingleAsync(a => a.ImportProfileId == planId);

        Assert.Equal(pausedCeiling, amendment.PreviousCeiling);
        Assert.Equal(newCeiling, amendment.NewCeiling);
        Assert.Equal("USD", amendment.Currency);
        Assert.Equal("customer requested more content", amendment.Reason);
        Assert.NotNull(amendment.AdministratorUserId);
        Assert.True(amendment.CreatedAtUtc > DateTimeOffset.UtcNow.AddMinutes(-5));

        var plan = await db.ImportProfiles.FirstAsync(p => p.Id == planId);
        Assert.Equal(newCeiling, plan.ApprovedCostCeiling); // the plan's live ceiling is the new value
    }

    [Fact]
    public async Task Package_resumes_only_after_a_successful_amendment()
    {
        var client = await AdminClientAsync();
        var (packageId, planId, stamp, pausedCeiling) = await CreatePausedForCostPlanAsync(client, $"Ceiling Amend Resume Source {Guid.NewGuid():N}");

        var resp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/amend-ceiling",
            new { expectedConcurrencyStamp = stamp, newApprovedCostCeiling = pausedCeiling + 5m, reason = "resume please" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var package = await db.ImportPackages.FirstAsync(p => p.Id == packageId);
        var plan = await db.ImportProfiles.FirstAsync(p => p.Id == planId);
        Assert.Equal(ImportPackageStatus.Queued, package.Status);
        Assert.Equal(ImportProfileStatus.Executing, plan.Status);
        Assert.Null(plan.PauseReason);
    }

    [Fact]
    public async Task Failed_amendment_does_not_resume_processing()
    {
        var client = await AdminClientAsync();
        var (packageId, planId, stamp, pausedCeiling) = await CreatePausedForCostPlanAsync(client, $"Ceiling Amend Failed Source {Guid.NewGuid():N}");

        // A ceiling that is not actually higher fails validation — the package must stay paused.
        var resp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/amend-ceiling",
            new { expectedConcurrencyStamp = stamp, newApprovedCostCeiling = pausedCeiling, reason = "not higher" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var package = await db.ImportPackages.FirstAsync(p => p.Id == packageId);
        var plan = await db.ImportProfiles.FirstAsync(p => p.Id == planId);
        Assert.Equal(ImportPackageStatus.AwaitingMappingApproval, package.Status);
        Assert.Equal(ImportProfileStatus.PausedForCostApproval, plan.Status);
        Assert.Empty(await db.ImportCostCeilingAmendments.Where(a => a.ImportProfileId == planId).ToListAsync());
    }

    [Fact]
    public async Task Plan_detail_returns_the_full_cost_summary_and_amendment_history()
    {
        var client = await AdminClientAsync();
        var (packageId, planId, stamp, pausedCeiling) = await CreatePausedForCostPlanAsync(client, $"Ceiling Amend Detail Source {Guid.NewGuid():N}");

        var amendResp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/amend-ceiling",
            new { expectedConcurrencyStamp = stamp, newApprovedCostCeiling = pausedCeiling + 5m, reason = "detail check" });
        Assert.Equal(HttpStatusCode.OK, amendResp.StatusCode);

        var planResp = await client.GetAsync($"/api/admin/import-packages/{packageId}/plan");
        Assert.Equal(HttpStatusCode.OK, planResp.StatusCode);
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(planBody.GetProperty("accruedCost").GetDecimal() > 0);
        Assert.Equal("USD", planBody.GetProperty("accruedCostCurrency").GetString());
        Assert.True(planBody.TryGetProperty("remainingCeiling", out _));
        var amendments = planBody.GetProperty("ceilingAmendments").EnumerateArray().ToList();
        Assert.Single(amendments);
        Assert.Equal("detail check", amendments[0].GetProperty("reason").GetString());
        Assert.Equal(pausedCeiling, amendments[0].GetProperty("previousCeiling").GetDecimal());
        Assert.Equal(pausedCeiling + 5m, amendments[0].GetProperty("newCeiling").GetDecimal());
    }
}
