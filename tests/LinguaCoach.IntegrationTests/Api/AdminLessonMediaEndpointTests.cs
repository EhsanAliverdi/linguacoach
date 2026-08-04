using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Rich-text rebuild — image/audio/video upload+serving endpoints backing the admin
/// Lesson editor's TipTap embeds.</summary>
public sealed class AdminLessonMediaEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminLessonMediaEndpointTests(ApiTestFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        // .NET's redirect handling strips the Authorization header on any redirect (even
        // same-origin) — the Get→Stream fallback redirect needs the header to survive, so tests
        // that follow it disable auto-redirect and re-attach the header manually instead.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<byte[]> GetFollowingRedirectAsync(HttpClient client, string url)
    {
        var first = await client.GetAsync(url);
        if (first.StatusCode != HttpStatusCode.Redirect && first.StatusCode != HttpStatusCode.Found)
            return await first.Content.ReadAsByteArrayAsync();

        var second = await client.GetAsync(first.Headers.Location!.OriginalString);
        return await second.Content.ReadAsByteArrayAsync();
    }

    private static MultipartFormDataContent MediaForm(string text, string contentType, string fileName)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { fileContent, "file", fileName } };
    }

    [Fact]
    public async Task UploadMedia_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        using var form = MediaForm("fake png bytes", "image/png", "cover.png");

        var response = await client.PostAsync("/api/admin/lessons/media", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadMedia_rejects_unsupported_mime_type()
    {
        var client = await AdminClientAsync();
        using var form = MediaForm("not a real file", "application/zip", "archive.zip");

        var response = await client.PostAsync("/api/admin/lessons/media", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadMedia_image_then_fetch_round_trips_the_same_bytes()
    {
        var client = await AdminClientAsync();
        using var form = MediaForm("fake png bytes", "image/png", "cover.png");

        var uploadResp = await client.PostAsync("/api/admin/lessons/media", form);
        Assert.Equal(HttpStatusCode.OK, uploadResp.StatusCode);
        var uploadBody = await uploadResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("image/png", uploadBody.GetProperty("mimeType").GetString());
        var url = uploadBody.GetProperty("url").GetString()!;
        Assert.StartsWith("/api/lesson-media/", url);

        // Get redirects to the fake storage backend's streaming fallback (no real signed-URL
        // concept), which serves back the exact bytes that were uploaded.
        var bytes = await GetFollowingRedirectAsync(client, url);
        Assert.Equal("fake png bytes", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task UploadMedia_audio_and_video_are_both_accepted()
    {
        var client = await AdminClientAsync();

        using var audioForm = MediaForm("fake webm audio", "audio/webm", "clip.weba");
        var audioResp = await client.PostAsync("/api/admin/lessons/media", audioForm);
        Assert.Equal(HttpStatusCode.OK, audioResp.StatusCode);
        var audioBody = await audioResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("audio/webm", audioBody.GetProperty("mimeType").GetString());

        using var videoForm = MediaForm("fake webm video", "video/webm", "clip.webm");
        var videoResp = await client.PostAsync("/api/admin/lessons/media", videoForm);
        Assert.Equal(HttpStatusCode.OK, videoResp.StatusCode);
        var videoBody = await videoResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("video/webm", videoBody.GetProperty("mimeType").GetString());

        // audio/webm and video/webm must not collide on disambiguation when served back.
        var audioUrl = audioBody.GetProperty("url").GetString()!;
        var videoUrl = videoBody.GetProperty("url").GetString()!;
        var audioBytes = await GetFollowingRedirectAsync(client, audioUrl);
        var videoBytes = await GetFollowingRedirectAsync(client, videoUrl);
        Assert.Equal("fake webm audio", Encoding.UTF8.GetString(audioBytes));
        Assert.Equal("fake webm video", Encoding.UTF8.GetString(videoBytes));
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_key()
    {
        var client = await AdminClientAsync();

        var response = await client.GetAsync("/api/lesson-media/lesson-media/nope/does-not-exist.png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_and_stream_work_with_no_auth_token_at_all()
    {
        // The real-world bug this guards: a raw <img>/<audio> tag can never send the app's JWT
        // (only HttpClient calls get that via the auth interceptor), so this endpoint must be
        // reachable by a completely anonymous request — matching its own redirect target (a MinIO
        // signed URL), which already has zero app-level auth once reached.
        var adminClient = await AdminClientAsync();
        using var form = MediaForm("fake png bytes", "image/png", "cover.png");
        var uploadResp = await adminClient.PostAsync("/api/admin/lessons/media", form);
        var uploadBody = await uploadResp.Content.ReadFromJsonAsync<JsonElement>();
        var url = uploadBody.GetProperty("url").GetString()!;

        var anonClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var bytes = await GetFollowingRedirectAsync(anonClient, url);

        Assert.Equal("fake png bytes", Encoding.UTF8.GetString(bytes));
    }
}
