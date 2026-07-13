using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Phase J5c — real audio-file upload endpoints for ListeningPassage resource candidates.</summary>
public sealed class AdminResourceCandidateAudioEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminResourceCandidateAudioEndpointTests(ApiTestFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, Guid CandidateId)> StageListeningCandidateAsync()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Judgment call: title/transcript must be unique per call — ResourceCandidateValidationService's
        // dedup gate checks content fingerprints globally across the whole test run, and this
        // helper is called from multiple tests in this class (mirrors the same pattern documented
        // in AdminResourceImportEndpointTests.ApproveAndPublish_UnapprovedButOtherwiseValidCandidate_PublishesInOneCall).
        var unique = Guid.NewGuid().ToString("N");
        var sourceName = $"Listening Audio Test Source {unique}";
        var importResp = await client.PostAsJsonAsync("/api/admin/content-imports", new
        {
            sourceName,
            resourceType = "listening",
            inputMode = "csv_text",
            content = $"title,transcript,cefrLevel\nMorning News {unique},Good morning and welcome to the daily news {unique}.,A1",
        });
        Assert.Equal(HttpStatusCode.OK, importResp.StatusCode);
        var importBody = await importResp.Content.ReadFromJsonAsync<JsonElement>();
        var runId = importBody.GetProperty("importRunId").GetGuid();

        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?importRunId={runId}");
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        var candidateId = candidatesBody.GetProperty("items")[0].GetProperty("candidateId").GetGuid();

        return (client, candidateId);
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

        var sourceName = $"Vocab For Audio Reject Test {Guid.NewGuid():N}";
        var importResp = await client.PostAsJsonAsync("/api/admin/content-imports", new
        {
            sourceName,
            resourceType = "vocabulary",
            inputMode = "pasted_text",
            content = "hello",
        });
        var importBody = await importResp.Content.ReadFromJsonAsync<JsonElement>();
        var runId = importBody.GetProperty("importRunId").GetGuid();
        var candidatesResp = await client.GetAsync($"/api/admin/resource-candidates?importRunId={runId}");
        var candidatesBody = await candidatesResp.Content.ReadFromJsonAsync<JsonElement>();
        var candidateId = candidatesBody.GetProperty("items")[0].GetProperty("candidateId").GetGuid();

        using var form = AudioForm();
        var response = await client.PostAsync($"/api/admin/resource-candidates/{candidateId}/audio", form);

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
