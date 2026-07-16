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

/// <summary>
/// Phase 4.6 — authenticated audio access for a published Listening Resource Bank item. Drives the
/// full candidate→approve→publish→Resource Bank flow (reusing
/// AdminResourceCandidateAudioEndpointTests' staging helper pattern) so the audio endpoint is
/// exercised against a genuinely published row, not a directly-seeded one.
/// </summary>
public sealed class AdminResourceBankMediaEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminResourceBankMediaEndpointTests(ApiTestFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, Guid CandidateId)> StageListeningCandidateAsync()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var unique = Guid.NewGuid().ToString("N");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        var source = new CefrResourceSource(
            $"Resource Bank Media Test Source {unique}", "AdminUpload", allowsStudentDisplay: true, allowsCommercialUse: true);
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

        var normalizedJson = $$"""{"title":"Morning News {{unique}}","transcript":"Good morning news {{unique}}."}""";
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

    private static MultipartFormDataContent AudioForm(string text = "fake mp3 bytes") =>
        new()
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes(text))
                { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg") } }, "audioFile", "clip.mp3" }
        };

    private async Task<Guid> PublishListeningResourceAsync(HttpClient client, Guid candidateId, string audioText)
    {
        using var form = AudioForm(audioText);
        var uploadResp = await client.PostAsync($"/api/admin/resource-candidates/{candidateId}/audio", form);
        Assert.Equal(HttpStatusCode.OK, uploadResp.StatusCode);

        var validateResp = await client.PostAsJsonAsync($"/api/admin/resource-candidates/{candidateId}/validate", new { });
        Assert.Equal(HttpStatusCode.OK, validateResp.StatusCode);

        var approveResp = await client.PostAsJsonAsync(
            $"/api/admin/resource-candidates/{candidateId}/approve", new { notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        var publishResp = await client.PostAsync($"/api/admin/resource-candidates/{candidateId}/publish", null);
        var publishBody = await publishResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(publishBody.GetProperty("success").GetBoolean(), publishBody.ToString());

        return publishBody.GetProperty("publishedEntityId").GetGuid();
    }

    [Fact]
    public async Task GetAudioUrl_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/admin/resource-bank/{Guid.NewGuid()}/audio-url");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAudio_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/admin/resource-bank/{Guid.NewGuid()}/audio");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAudioUrl_returns_404_for_a_non_listening_resource()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var source = new CefrResourceSource($"Vocab Media Test {Guid.NewGuid():N}", "Internal", allowsStudentDisplay: true, allowsCommercialUse: true);
        db.CefrResourceSources.Add(source);
        await db.SaveChangesAsync();
        var entry = new ResourceBankItem(
            PublishedResourceType.Vocabulary, source.Id, "A1",
            LinguaCoach.Application.ResourceImport.ResourceBankItemContent.Serialize(
                new LinguaCoach.Application.ResourceImport.VocabularyContent("hello", null, null)));
        db.ResourceBankItems.Add(entry);
        await db.SaveChangesAsync();

        var response = await client.GetAsync($"/api/admin/resource-bank/{entry.Id}/audio-url");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Published_listening_resource_audio_round_trips_through_the_signed_url_and_stream_endpoints()
    {
        var (client, candidateId) = await StageListeningCandidateAsync();
        var resourceId = await PublishListeningResourceAsync(client, candidateId, "published audio bytes");

        var urlResp = await client.GetAsync($"/api/admin/resource-bank/{resourceId}/audio-url");
        Assert.Equal(HttpStatusCode.OK, urlResp.StatusCode);
        var urlBody = await urlResp.Content.ReadFromJsonAsync<JsonElement>();
        var url = urlBody.GetProperty("url").GetString();
        Assert.Contains($"/api/admin/resource-bank/{resourceId}/audio", url);

        var streamResp = await client.GetAsync($"/api/admin/resource-bank/{resourceId}/audio");
        Assert.Equal(HttpStatusCode.OK, streamResp.StatusCode);
        var bytes = await streamResp.Content.ReadAsByteArrayAsync();
        Assert.Equal("published audio bytes", Encoding.UTF8.GetString(bytes));
    }

    /// <summary>Ownership: fetching a different resource id's audio must never resolve the first
    /// resource's bytes — proves the endpoint can't be used to reach an unrelated storage key.</summary>
    [Fact]
    public async Task Two_published_listening_resources_never_cross_resolve_each_others_audio()
    {
        var (clientA, candidateA) = await StageListeningCandidateAsync();
        var resourceA = await PublishListeningResourceAsync(clientA, candidateA, "audio-A-bytes");

        var (clientB, candidateB) = await StageListeningCandidateAsync();
        var resourceB = await PublishListeningResourceAsync(clientB, candidateB, "audio-B-bytes");

        var streamA = await clientA.GetAsync($"/api/admin/resource-bank/{resourceA}/audio");
        var streamB = await clientA.GetAsync($"/api/admin/resource-bank/{resourceB}/audio");

        Assert.Equal("audio-A-bytes", Encoding.UTF8.GetString(await streamA.Content.ReadAsByteArrayAsync()));
        Assert.Equal("audio-B-bytes", Encoding.UTF8.GetString(await streamB.Content.ReadAsByteArrayAsync()));
    }

    /// <summary>Phase 4.6 — the published Resource Bank detail query surfaces the audio/transcript
    /// metadata that used to be silently dropped by MapListening.</summary>
    [Fact]
    public async Task Resource_bank_detail_surfaces_published_audio_and_transcript_metadata()
    {
        var (client, candidateId) = await StageListeningCandidateAsync();
        var resourceId = await PublishListeningResourceAsync(client, candidateId, "published audio bytes");

        var detailResp = await client.GetAsync($"/api/admin/resource-bank/{resourceId}");
        Assert.Equal(HttpStatusCode.OK, detailResp.StatusCode);
        var detail = await detailResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(detail.GetProperty("hasAudio").GetBoolean());
        Assert.Equal("audio/mpeg", detail.GetProperty("audioContentType").GetString());
        Assert.False(detail.TryGetProperty("audioStorageKey", out _), "the list/detail DTO must never expose the raw storage key");
    }
}
