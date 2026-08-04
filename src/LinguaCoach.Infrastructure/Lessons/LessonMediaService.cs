using LinguaCoach.Application.Lessons;
using LinguaCoach.Application.Storage;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Lessons;

/// <summary>
/// Backs the admin lesson rich-text editor's inline image/audio/video embeds. Follows
/// <c>ResourceCandidateAudioService</c>'s simple server-mediated <see cref="IFileStorageService"/>
/// upload pattern (not the signed-URL/chunked-session flow used for large ZIP import archives —
/// lesson media stays well under Kestrel's default request body limit).
/// </summary>
public sealed class LessonMediaService : ILessonMediaService
{
    private const string Category = "lesson-media";
    private const long MaxImageBytes = 10 * 1024 * 1024; // 10 MB
    private const long MaxAudioBytes = 20 * 1024 * 1024; // 20 MB — matches ResourceCandidateAudioService's ceiling.
    private const long MaxVideoBytes = 150 * 1024 * 1024; // 150 MB

    private static readonly TimeSpan SignedUrlExpiry = TimeSpan.FromMinutes(15);

    private static readonly HashSet<string> AllowedImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp", "image/gif",
    };

    // Same allowlist ResourceCandidateAudioService already validates against.
    private static readonly HashSet<string> AllowedAudioMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/webm", "audio/wav", "audio/mpeg", "audio/mp4", "audio/x-m4a", "audio/ogg",
    };

    private static readonly HashSet<string> AllowedVideoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4", "video/webm",
    };

    private readonly IFileStorageService _storage;
    private readonly ILogger<LessonMediaService> _logger;

    public LessonMediaService(IFileStorageService storage, ILogger<LessonMediaService> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public bool IsAllowedMimeType(string mimeType)
    {
        var normalized = Normalize(mimeType);
        return AllowedImageMimeTypes.Contains(normalized)
            || AllowedAudioMimeTypes.Contains(normalized)
            || AllowedVideoMimeTypes.Contains(normalized);
    }

    public long GetMaxBytes(string mimeType)
    {
        var normalized = Normalize(mimeType);
        if (AllowedImageMimeTypes.Contains(normalized)) return MaxImageBytes;
        if (AllowedAudioMimeTypes.Contains(normalized)) return MaxAudioBytes;
        if (AllowedVideoMimeTypes.Contains(normalized)) return MaxVideoBytes;
        return 0;
    }

    public async Task<LessonMediaUploadResult> UploadAsync(
        Stream content, string mimeType, Guid? uploadedByUserId, CancellationToken ct = default)
    {
        var normalized = Normalize(mimeType);
        if (!IsAllowedMimeType(normalized))
            throw new LessonMediaValidationException($"Media type '{normalized}' is not supported.");

        var ownerId = uploadedByUserId?.ToString("N") ?? "unknown";
        var ext = MimeTypeToExtension(normalized);
        var key = _storage.GenerateKey(ownerId, Category, ext);

        await _storage.SaveAsync(key, content, normalized, ct);

        _logger.LogInformation("Lesson media stored Key={Key} MimeType={MimeType}", key, normalized);
        return new LessonMediaUploadResult(key, $"/api/lesson-media/{key}", normalized);
    }

    public async Task<string?> ResolveUrlAsync(string storageKey, CancellationToken ct = default)
    {
        if (!await _storage.ExistsAsync(storageKey, ct)) return null;

        var signed = await _storage.GenerateSignedUrlAsync(storageKey, SignedUrlExpiry, ct);
        return signed.Url.StartsWith("local://", StringComparison.OrdinalIgnoreCase)
               || signed.Url.StartsWith("fake://", StringComparison.OrdinalIgnoreCase)
            ? $"/api/lesson-media/stream/{storageKey}"
            : signed.Url;
    }

    public async Task<LessonMediaStreamResult?> GetStreamAsync(string storageKey, CancellationToken ct = default)
    {
        if (!await _storage.ExistsAsync(storageKey, ct)) return null;

        await using var stream = await _storage.ReadAsync(storageKey, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);

        var mimeType = ExtensionToMimeType(Path.GetExtension(storageKey));
        return new LessonMediaStreamResult(ms.ToArray(), mimeType);
    }

    private static string Normalize(string mimeType) => mimeType.Split(';')[0].Trim();

    // Every extension here must round-trip uniquely back to a MIME type via ExtensionToMimeType
    // below (no ReadAsync-time content-type storage exists to fall back on) — note audio/webm uses
    // the real, distinct ".weba" extension (MDN/Firefox's convention for WebM audio) specifically
    // so it doesn't collide with video/webm's ".webm".
    private static string MimeTypeToExtension(string mimeType) => mimeType switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        "audio/webm" => ".weba",
        "audio/wav" => ".wav",
        "audio/mpeg" => ".mp3",
        "audio/mp4" => ".m4a",
        "audio/x-m4a" => ".m4a",
        "audio/ogg" => ".ogg",
        "video/mp4" => ".mp4",
        "video/webm" => ".webm",
        _ => ".bin",
    };

    private static string ExtensionToMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".weba" => "audio/webm",
        ".wav" => "audio/wav",
        ".mp3" => "audio/mpeg",
        ".m4a" => "audio/mp4",
        ".ogg" => "audio/ogg",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        _ => "application/octet-stream",
    };
}
