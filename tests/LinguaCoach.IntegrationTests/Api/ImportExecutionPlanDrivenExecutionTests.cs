using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 4.3 (2026-07-16) — approved-plan-driven execution. The critical acceptance proof: the
/// exact same package content, processed under two different approved plans, must produce
/// different candidate output. There is no admin plan-editing endpoint yet (out of Phase 4.3
/// scope — see the review doc), so each plan's ProfileJson is overwritten directly against
/// persistence between plan generation and approval, exactly the shape a future edit endpoint
/// would write. Execution itself (extraction, candidate creation, cost accounting) runs entirely
/// through the real <see cref="IImportPackageProcessingService"/> and real EF Core persistence —
/// nothing here mocks the processing pipeline itself.
/// </summary>
public sealed class ImportExecutionPlanDrivenExecutionTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ImportExecutionPlanDrivenExecutionTests(ApiTestFactory factory) => _factory = factory;

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
        var processingService = scope.ServiceProvider.GetRequiredService<IImportPackageProcessingService>();
        await processingService.ProcessPendingAsync(maxPackages: 10);
    }

    private async Task<(Guid PackageId, Guid PlanId)> SubmitCsvAndGeneratePlanAsync(
        HttpClient client, Guid sourceId, byte[] csvBytes, string fileName)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(sourceId.ToString()), "cefrResourceSourceId" },
        };
        var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "files", fileName);

        var submitResp = await client.PostAsync("/api/admin/import-packages/submit", form);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var submitBody = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        var packageId = submitBody.GetProperty("importPackageId").GetGuid();

        var planResp = await client.PostAsJsonAsync($"/api/admin/import-packages/{packageId}/plan", new { });
        Assert.Equal(HttpStatusCode.OK, planResp.StatusCode);
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        var planId = planBody.GetProperty("planId").GetGuid();

        return (packageId, planId);
    }

    private async Task OverwriteApprovedPlanInstructionsAsync(
        Guid planId, IReadOnlyList<ImportExecutionGroupInstruction> instructions)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var plan = await db.ImportProfiles.FirstAsync(p => p.Id == planId);
        db.Entry(plan).CurrentValues["ProfileJson"] = JsonSerializer.Serialize(instructions);
        await db.SaveChangesAsync();
    }

    private async Task ApprovePlanAsync(HttpClient client, Guid packageId, Guid planId)
    {
        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/import-packages/{packageId}/plan/{planId}/approve", new { approvedCostCeiling = 100m });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);
    }

    /// <summary>The critical acceptance proof, per the Phase 4.3 spec: byte-identical package
    /// content ("mystery1,mystery2" columns InferCandidateType would never recognize on its own)
    /// processed under two approved plans that map those same columns differently produces
    /// different candidate types with different field values — proving execution follows each
    /// approved plan's own instructions rather than re-deriving the same result independently.</summary>
    [Fact]
    public async Task Same_csv_content_with_two_different_approved_field_mappings_produces_different_candidates()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"Mapping Divergence Source {Guid.NewGuid():N}");
        var marker1 = $"alpha{Guid.NewGuid():N}";
        var marker2 = $"beta{Guid.NewGuid():N}";
        var csvBytes = Encoding.UTF8.GetBytes($"mystery1,mystery2\r\n{marker1},{marker2}\r\n");

        var (packageAId, planAId) = await SubmitCsvAndGeneratePlanAsync(client, sourceId, csvBytes, "data-a.csv");
        var (packageBId, planBId) = await SubmitCsvAndGeneratePlanAsync(client, sourceId, csvBytes, "data-b.csv");

        // Plan A: mystery1 -> word, forced route Vocabulary.
        await OverwriteApprovedPlanInstructionsAsync(planAId, new[]
        {
            new ImportExecutionGroupInstruction(
                "(root)", true, ResourceCandidateType.VocabularyEntry,
                new Dictionary<string, string> { ["mystery1"] = "word" }, Array.Empty<string>()),
        });

        // Plan B: mystery1 -> title, mystery2 -> text, forced route Reading — same source columns,
        // deliberately different target fields and a different resource type.
        await OverwriteApprovedPlanInstructionsAsync(planBId, new[]
        {
            new ImportExecutionGroupInstruction(
                "(root)", true, ResourceCandidateType.ReadingPassage,
                new Dictionary<string, string> { ["mystery1"] = "title", ["mystery2"] = "text" }, Array.Empty<string>()),
        });

        await ApprovePlanAsync(client, packageAId, planAId);
        await ApprovePlanAsync(client, packageBId, planBId);

        await RunPackageProcessingAsync();

        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?sourceId={sourceId}&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, candidatesResp.StatusCode);
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = candidatesBody.GetProperty("items").EnumerateArray().ToList();

        var vocabCandidate = items.SingleOrDefault(i =>
            i.GetProperty("canonicalText").GetString() == marker1
            && i.GetProperty("candidateType").GetString() == nameof(ResourceCandidateType.VocabularyEntry));
        Assert.True(vocabCandidate.ValueKind != JsonValueKind.Undefined,
            $"Expected a VocabularyEntry candidate with canonicalText '{marker1}' from plan A's mapping. Items: {candidatesBody}");

        var readingCandidate = items.SingleOrDefault(i =>
            i.GetProperty("canonicalText").GetString() == marker1
            && i.GetProperty("candidateType").GetString() == nameof(ResourceCandidateType.ReadingPassage));
        Assert.True(readingCandidate.ValueKind != JsonValueKind.Undefined,
            $"Expected a ReadingPassage candidate with canonicalText '{marker1}' from plan B's mapping. Items: {candidatesBody}");

        // Plan B's candidate must also carry the mystery2 -> text mapping's value somewhere in its
        // normalized content — proving the *second* field mapping was applied too, not just the
        // one used for canonical text.
        Assert.Contains(marker2, readingCandidate.GetProperty("normalizedJson").GetString());
    }

    /// <summary>Excluded groups create no candidates at all — the plan's Included=false decision
    /// must be honoured, not just its mapping/routing decisions.</summary>
    [Fact]
    public async Task Excluded_group_in_the_approved_plan_creates_no_candidates()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"Exclusion Source {Guid.NewGuid():N}");
        var marker = $"excluded{Guid.NewGuid():N}";
        var csvBytes = Encoding.UTF8.GetBytes($"word\r\n{marker}\r\n");

        var (packageId, planId) = await SubmitCsvAndGeneratePlanAsync(client, sourceId, csvBytes, "excluded.csv");

        await OverwriteApprovedPlanInstructionsAsync(planId, new[]
        {
            new ImportExecutionGroupInstruction(
                "(root)", false, null,
                new Dictionary<string, string>(), Array.Empty<string>()),
        });

        await ApprovePlanAsync(client, packageId, planId);
        await RunPackageProcessingAsync();

        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?sourceId={sourceId}&pageSize=50");
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = candidatesBody.GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(items, i => i.GetProperty("canonicalText").GetString() == marker);

        var packageResp = await client.GetAsync($"/api/admin/import-packages/{packageId}/plan");
        var packageBody = await packageResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", packageBody.GetProperty("status").GetString());
    }

    /// <summary>A malformed approved plan (unrecognized field-mapping target) must fail package
    /// processing deterministically, before any candidate is created — never silently fall back to
    /// inferred mapping.</summary>
    [Fact]
    public async Task Malformed_approved_plan_field_mapping_fails_processing_and_creates_no_candidates()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"Malformed Plan Source {Guid.NewGuid():N}");
        var marker = $"malformed{Guid.NewGuid():N}";
        var csvBytes = Encoding.UTF8.GetBytes($"word\r\n{marker}\r\n");

        var (packageId, planId) = await SubmitCsvAndGeneratePlanAsync(client, sourceId, csvBytes, "malformed.csv");

        await OverwriteApprovedPlanInstructionsAsync(planId, new[]
        {
            new ImportExecutionGroupInstruction(
                "(root)", true, null,
                new Dictionary<string, string> { ["word"] = "not-a-recognized-field" }, Array.Empty<string>()),
        });

        await ApprovePlanAsync(client, packageId, planId);
        await RunPackageProcessingAsync();

        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?sourceId={sourceId}&pageSize=50");
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, candidatesBody.GetProperty("items").GetArrayLength());

        var packageResp = await client.GetAsync($"/api/admin/import-packages/{packageId}/plan");
        var packageBody = await packageResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("failed", packageBody.GetProperty("status").GetString());
    }

    /// <summary>Package processing must use the exact approved plan even when a newer draft plan
    /// exists for the same package — never "the latest" profile.</summary>
    [Fact]
    public async Task Processing_uses_the_exact_approved_plan_even_when_a_newer_draft_plan_exists()
    {
        var client = await AdminClientAsync();
        var sourceId = await CreateApprovedSourceAsync(client, $"Latest Plan Guard Source {Guid.NewGuid():N}");
        var marker = $"pinned{Guid.NewGuid():N}";
        var csvBytes = Encoding.UTF8.GetBytes($"mystery1\r\n{marker}\r\n");

        var (packageId, planId) = await SubmitCsvAndGeneratePlanAsync(client, sourceId, csvBytes, "pinned.csv");

        await OverwriteApprovedPlanInstructionsAsync(planId, new[]
        {
            new ImportExecutionGroupInstruction(
                "(root)", true, ResourceCandidateType.VocabularyEntry,
                new Dictionary<string, string> { ["mystery1"] = "word" }, Array.Empty<string>()),
        });
        await ApprovePlanAsync(client, packageId, planId);

        // A newer (v2) Draft plan row for the same package, inserted directly rather than through
        // IImportExecutionPlanGenerationService (which would supersede the just-approved v1 plan —
        // a separate, pre-existing plan-lifecycle rule, not what this test targets). The point
        // here is narrower: prove ImportPackageProcessingService resolves execution by the exact
        // ApprovedImportProfileId, never "the latest version for this package" — v2 below carries
        // a mapping that would produce a *different* candidate if execution mistakenly used it.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var wrongInstructions = JsonSerializer.Serialize(new[]
            {
                new ImportExecutionGroupInstruction(
                    "(root)", true, ResourceCandidateType.ReadingPassage,
                    new Dictionary<string, string> { ["mystery1"] = "title" }, Array.Empty<string>()),
            });
            var v2 = new LinguaCoach.Domain.Entities.ImportProfile(
                packageId, 2, wrongInstructions, Array.Empty<Guid>(), estimatedCandidateCount: 1,
                createdAtUtc: DateTimeOffset.UtcNow, changeReason: "a newer draft that must not be used");
            db.ImportProfiles.Add(v2);
            await db.SaveChangesAsync();
        }

        await RunPackageProcessingAsync();

        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?sourceId={sourceId}&pageSize=50");
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        var items = candidatesBody.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i =>
            i.GetProperty("canonicalText").GetString() == marker
            && i.GetProperty("candidateType").GetString() == nameof(ResourceCandidateType.VocabularyEntry));
    }
}
