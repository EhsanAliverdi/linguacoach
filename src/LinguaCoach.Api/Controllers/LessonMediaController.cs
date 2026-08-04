using LinguaCoach.Application.Lessons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Serves image/audio/video embedded in a Lesson's rich-text fields via a stable app-relative
/// path (the URL saved in the Lesson's HTML never expires, unlike a raw storage-provider signed
/// URL). Deliberately <see cref="AllowAnonymousAttribute"/>, not <c>[Authorize]</c>: this is a
/// native browser resource load (&lt;img src&gt;/&lt;audio src&gt;/&lt;video src&gt;), which can
/// never send the app's JWT Authorization header the way an HttpClient call can — and the
/// <see cref="Get"/> action's own redirect target (a MinIO signed URL) already has zero app-level
/// auth once reached, so gating only the first hop bought nothing but a guaranteed 401. Storage
/// keys are random GUID-based paths, unguessable, and this endpoint is read-only (upload stays
/// behind <c>[Authorize(Roles = "Admin")]</c> on <c>AdminLessonController.UploadMedia</c>).
/// </summary>
[ApiController]
[Route("api/lesson-media")]
[AllowAnonymous]
public sealed class LessonMediaController : ControllerBase
{
    private readonly ILessonMediaService _mediaService;

    public LessonMediaController(ILessonMediaService mediaService) => _mediaService = mediaService;

    // GET api/lesson-media/stream/{*key} — streaming fallback for local/fake storage backends
    // (dev and test only) that have no signed-URL concept for Get below to redirect to. Must be
    // registered before the catch-all Get route below (a "stream/" prefix keeps them unambiguous —
    // a real storage key is always "lesson-media/{owner}/{guid}.{ext}", never "stream/...").
    [HttpGet("stream/{*key}")]
    public async Task<IActionResult> Stream(string key, CancellationToken ct)
    {
        var result = await _mediaService.GetStreamAsync(key, ct);
        return result is null ? NotFound() : File(result.Content, result.ContentType);
    }

    // GET api/lesson-media/{*key}
    [HttpGet("{*key}")]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        var url = await _mediaService.ResolveUrlAsync(key, ct);
        return url is null ? NotFound() : Redirect(url);
    }
}
