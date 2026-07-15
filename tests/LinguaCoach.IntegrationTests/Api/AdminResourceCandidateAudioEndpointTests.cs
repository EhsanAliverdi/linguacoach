using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Phase J5c — real audio-file upload endpoints for ListeningPassage resource candidates.</summary>
public sealed class AdminResourceCandidateAudioEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminResourceCandidateAudioEndpointTests(ApiTestFactory factory) => _factory = factory;

    /// <summary>
    /// Phase 4.2 — this file's purpose is testing the audio-upload/publish-gate endpoints, not
    /// import staging, and every publishable candidate must now trace back to an ImportPackage
    /// with an approved Import Execution Plan. Seeds directly through the DbContext (mirroring the
    /// UnitTests project's convention) rather than round-tripping through the unified submission
    /// endpoint, which no longer accepts an explicit "force this candidate type" override the old
    /// content-imports endpoint had.
    /// </summary>
    private async Task<(HttpClient Client, Guid CandidateId)> StageListeningCandidateAsync()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var unique = Guid.NewGuid().ToString("N");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var source = new CefrResourceSource(
            $"Listening Audio Test Source {unique}", "AdminUpload",
            allowsStudentDisplay: true, allowsCommercialUse: true);
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
            source.Id, ResourceImportMode.Json, "listening-assets", $"hash-{unique}", DateTimeOffset.UtcNow,
            importPackageId: package.Id);
        db.ResourceImportRuns.Add(run);
        await db.SaveChangesAsync();

        var normalizedJson = $$"""{"title":"Morning News {{unique}}","transcript":"Good morning and welcome to the daily news {{unique}}."}""";
        var raw = new ResourceRawRecord(run.Id, $"rawhash-{unique}", "en", "row", rawJson: normalizedJson);
        raw.MarkParsed();
        db.ResourceRawRecords.Add(raw);
        await db.SaveChangesAsync();

        var fingerprint = new ActivityContentFingerprintService().ComputeFingerprint(
            new ActivityContentFingerprintRequest(normalizedJson, ActivityContentShape.Unknown, null, $"Morning News {unique}"));
        var candidate = new ResourceCandidate(
            raw.Id, ResourceCandidateType.ListeningPassage, $"Morning News {unique}", normalizedJson, "en",
            $"morning news {unique}", fingerprint, ResourceCandidateValidationStatus.NeedsReview);
        candidate.ApplyAnalysis(
            "{}", "A1", 0.95, "listening", null, 1, "[]", "[]", null, null, null, null, null, 0.9, candidate.CanonicalText);
        db.ResourceCandidates.Add(candidate);
        await db.SaveChangesAsync();

        return (client, candidate.Id);
    }

    private static MultipartFormDataContent AudioForm(string text = "fake mp3 bytes", string contentType = "audio/mpeg")
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { fileContent, "audioFile", "clip.mp3" } };
    }

    [Fact]
    public async Task Upload_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        using var form = AudioForm();
        var response = await client.PostAsync($"/api/admin/resource-candidates/{Guid.NewGuid()}/audio", form);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_then_fetch_audio_url_and_stream_round_trips_the_same_bytes()
    {
        var (client, candidateId) = await StageListeningCandidateAsync();
        using var form = AudioForm("fake mp3 bytes");

        var uploadResp = await client.PostAsync($"/api/admin/resource-candidates/{candidateId}/audio", form);
        Assert.Equal(HttpStatusCode.OK, uploadResp.StatusCode);
        var uploadBody = await uploadResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("audio/mpeg", uploadBody.GetProperty("audioContentType").GetString());

        var urlResp = await client.GetAsync($"/api/admin/resource-candidates/{candidateId}/audio-url");
        Assert.Equal(HttpStatusCode.OK, urlResp.StatusCode);
        var urlBody = await urlResp.Content.ReadFromJsonAsync<JsonElement>();
        var url = urlBody.GetProperty("url").GetString();
        Assert.Contains($"/api/admin/resource-candidates/{candidateId}/audio", url);

        var streamResp = await client.GetAsync($"/api/admin/resource-candidates/{candidateId}/audio");
        Assert.Equal(HttpStatusCode.OK, streamResp.StatusCode);
        var bytes = await streamResp.Content.ReadAsByteArrayAsync();
        Assert.Equal("fake mp3 bytes", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Audio_url_returns_404_when_nothing_uploaded_yet()
    {
        var (client, candidateId) = await StageListeningCandidateAsync();

        var urlResp = await client.GetAsync($"/api/admin/resource-candidates/{candidateId}/audio-url");

        Assert.Equal(HttpStatusCode.NotFound, urlResp.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_an_unsupported_mime_type()
    {
        var (client, candidateId) = await StageListeningCandidateAsync();
        using var form = AudioForm(contentType: "video/mp4");

        var response = await client.PostAsync($"/api/admin/resource-candidates/{candidateId}/audio", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_a_non_listening_candidate()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var unique = Guid.NewGuid().ToString("N");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var source = new CefrResourceSource(
            $"Vocab For Audio Reject Test {unique}", "AdminUpload", allowsStudentDisplay: true, allowsCommercialUse: true);
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
        var raw = new ResourceRawRecord(run.Id, $"rawhash-{unique}", "en", "row", rawJson: $$"""{"word":"hello{{unique}}"}""");
        raw.MarkParsed();
        db.ResourceRawRecords.Add(raw);
        await db.SaveChangesAsync();
        var fingerprint = new ActivityContentFingerprintService().ComputeFingerprint(
            new ActivityContentFingerprintRequest($$"""{"word":"hello{{unique}}"}""", ActivityContentShape.Unknown, null, "hello"));
        var candidate = new ResourceCandidate(
            raw.Id, ResourceCandidateType.VocabularyEntry, "hello", $$"""{"word":"hello{{unique}}"}""", "en",
            "hello", fingerprint, ResourceCandidateValidationStatus.NeedsReview);
        db.ResourceCandidates.Add(candidate);
        await db.SaveChangesAsync();

        using var form = AudioForm();
        var response = await client.PostAsync($"/api/admin/resource-candidates/{candidate.Id}/audio", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Publish_is_blocked_until_audio_is_uploaded_then_succeeds_after()
    {
        var (client, candidateId) = await StageListeningCandidateAsync();

        // Deterministic re-validation only (no AI call) — same pattern
        // AdminResourceImportEndpointTests.ApproveAndPublish_UnapprovedButOtherwiseValidCandidate_PublishesInOneCall
        // uses to reliably reach ValidationStatus=Passed in tests without depending on an AI provider.
        var validateResp = await client.PostAsJsonAsync($"/api/admin/resource-candidates/{candidateId}/validate", new { });
        Assert.Equal(HttpStatusCode.OK, validateResp.StatusCode);
        var validateBody = await validateResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Passed", validateBody.GetProperty("status").GetString());

        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/resource-candidates/{candidateId}/approve", new { notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        var firstPublish = await client.PostAsync($"/api/admin/resource-candidates/{candidateId}/publish", null);
        var firstBody = await firstPublish.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(firstBody.GetProperty("success").GetBoolean());
        Assert.Contains(firstBody.GetProperty("errors").EnumerateArray(),
            e => e.GetString()!.Contains("audio file is required"));

        using var form = AudioForm();
        var uploadResp = await client.PostAsync($"/api/admin/resource-candidates/{candidateId}/audio", form);
        Assert.Equal(HttpStatusCode.OK, uploadResp.StatusCode);

        var secondPublish = await client.PostAsync($"/api/admin/resource-candidates/{candidateId}/publish", null);
        var secondBody = await secondPublish.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(secondBody.GetProperty("success").GetBoolean());
        Assert.Equal("CefrListeningPassage", secondBody.GetProperty("publishedEntityType").GetString());
    }
}
