using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 4.5 — the typed Candidate Review editor's write path (PUT .../content with
/// TypedContentJson), the approve gate, and end-to-end publish of a valid typed VocabularyEntry
/// candidate into the Resource Bank. Real HTTP through the test host, real SQLite-backed
/// persistence — mirrors AdminResourceCandidateAudioEndpointTests' conventions.
/// </summary>
public sealed class AdminResourceCandidateTypedContentEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminResourceCandidateTypedContentEndpointTests(ApiTestFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, Guid CandidateId)> StageVocabularyCandidateAsync(string normalizedJson, string canonicalText)
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var unique = Guid.NewGuid().ToString("N");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var source = new CefrResourceSource(
            $"Typed Content Test Source {unique}", "AdminUpload", allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport("cleared for test");
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();

        var package = new ImportPackage(source.Id, "test-package", DateTimeOffset.UtcNow);
        db.ImportPackages.Add(package);
        await db.SaveChangesAsync();
        var plan = new ImportProfile(
            package.Id, 1, profileJson: "{}", sampleAssetIds: Array.Empty<Guid>(),
            estimatedCandidateCount: 1, createdAtUtc: DateTimeOffset.UtcNow);
        plan.SubmitForApproval();
        plan.Approve(null, DateTimeOffset.UtcNow, 100m);
        db.ImportProfiles.Add(plan);
        package.ApproveProfile(plan.Id);
        await db.SaveChangesAsync();

        var run = new ResourceImportRun(
            source.Id, ResourceImportMode.Csv, "test.csv", $"hash-{unique}", DateTimeOffset.UtcNow, importPackageId: package.Id);
        db.ResourceImportRuns.Add(run);
        await db.SaveChangesAsync();

        var raw = new ResourceRawRecord(run.Id, $"rawhash-{unique}", "en", "row", rawJson: normalizedJson);
        raw.MarkParsed();
        db.ResourceRawRecords.Add(raw);
        await db.SaveChangesAsync();

        var fingerprint = new ActivityContentFingerprintService().ComputeFingerprint(
            new ActivityContentFingerprintRequest(normalizedJson, ActivityContentShape.Unknown, null, canonicalText));
        var candidate = new ResourceCandidate(
            raw.Id, ResourceCandidateType.VocabularyEntry, canonicalText, normalizedJson, "en",
            canonicalText, fingerprint, ResourceCandidateValidationStatus.NeedsReview);
        db.ResourceCandidates.Add(candidate);
        await db.SaveChangesAsync();

        return (client, candidate.Id);
    }

    [Fact]
    public async Task Typed_content_edit_persists_and_is_reflected_on_the_get_endpoint()
    {
        var (client, candidateId) = await StageVocabularyCandidateAsync($$"""{"word":"hello-{{Guid.NewGuid():N}}"}""", "hello");

        var editResp = await client.PutAsJsonAsync($"/api/admin/resource-candidates/{candidateId}/content", new
        {
            typedContentJson = """{"word":"greeting","definition":"a friendly hello","partOfSpeech":"noun"}""",
        });

        Assert.Equal(HttpStatusCode.OK, editResp.StatusCode);
        var editBody = await editResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("\"word\":\"greeting\"", editBody.GetProperty("typedContentJson").GetString());

        var getResp = await client.GetAsync($"/api/admin/resource-candidates/{candidateId}");
        var getBody = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("\"definition\":\"a friendly hello\"", getBody.GetProperty("typedContentJson").GetString());
    }

    [Fact]
    public async Task Typed_content_edit_with_a_missing_required_field_returns_structured_field_errors()
    {
        var (client, candidateId) = await StageVocabularyCandidateAsync($$"""{"word":"hello-{{Guid.NewGuid():N}}"}""", "hello");

        var editResp = await client.PutAsJsonAsync($"/api/admin/resource-candidates/{candidateId}/content", new
        {
            typedContentJson = """{"definition":"missing the word field entirely"}""",
        });

        Assert.Equal(HttpStatusCode.BadRequest, editResp.StatusCode);
        var body = await editResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("fieldErrors", out var fieldErrors));
        Assert.Contains(fieldErrors.EnumerateArray(), e => e.GetProperty("fieldName").GetString() == "word");
    }

    [Fact]
    public async Task Valid_typed_vocabulary_candidate_approves_and_publishes_to_the_Resource_Bank()
    {
        var unique = Guid.NewGuid().ToString("N");
        var (client, candidateId) = await StageVocabularyCandidateAsync(
            $$"""{"word":"greeting-{{unique}}","definition":"a friendly hello"}""", $"greeting-{unique}");

        var editResp = await client.PutAsJsonAsync($"/api/admin/resource-candidates/{candidateId}/content", new
        {
            typedContentJson = $$"""{"word":"greeting-{{unique}}","definition":"a friendly hello","partOfSpeech":"noun"}""",
            cefrLevel = "A1",
        });
        Assert.Equal(HttpStatusCode.OK, editResp.StatusCode);

        var validateResp = await client.PostAsJsonAsync($"/api/admin/resource-candidates/{candidateId}/validate", new { });
        Assert.Equal(HttpStatusCode.OK, validateResp.StatusCode);

        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/resource-candidates/{candidateId}/approve", new { notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        var publishResp = await client.PostAsync($"/api/admin/resource-candidates/{candidateId}/publish", null);
        var publishBody = await publishResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(publishBody.GetProperty("success").GetBoolean(), publishBody.ToString());
        Assert.Equal("CefrVocabularyEntry", publishBody.GetProperty("publishedEntityType").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var publishedEntityId = publishBody.GetProperty("publishedEntityId").GetGuid();
        var bankItem = await db.ResourceBankItems.FirstOrDefaultAsync(b => b.Id == publishedEntityId);
        Assert.NotNull(bankItem);
        Assert.Equal(PublishedResourceType.Vocabulary, bankItem!.Type);
        Assert.Contains($"greeting-{unique}", bankItem.ContentJson);
    }
}
