namespace LinguaCoach.Application.Lessons;

/// <summary>Uploads/serves image, audio, and video files embedded in a Lesson's rich-text fields
/// (Body/UsageNotes/Examples/CommonMistakes). Mirrors <c>IResourceCandidateAudioService</c>'s
/// simple server-mediated upload pattern rather than the signed-URL/chunked-session flow used for
/// large ZIP import archives — lesson media is small/bounded (see <see cref="LessonMediaLimits"/>).
/// The <see cref="LessonMediaUploadResult.Url"/> returned is a stable app-relative serving path,
/// never the raw storage-provider URL — the URL gets embedded permanently into saved rich-text
/// HTML, and a signed URL would expire.</summary>
public interface ILessonMediaService
{
    bool IsAllowedMimeType(string mimeType);

    /// <summary>Maximum allowed size in bytes for the given MIME type's category (image/audio/video).</summary>
    long GetMaxBytes(string mimeType);

    Task<LessonMediaUploadResult> UploadAsync(
        Stream content, string mimeType, Guid? uploadedByUserId, CancellationToken ct = default);

    /// <summary>Resolves a storage key (as embedded in a Lesson's saved HTML) to a URL for the
    /// browser to load: a fresh short-lived signed URL for real storage backends, or this app's own
    /// authenticated streaming endpoint URL for local/fake dev-and-test backends with no signed-URL
    /// concept — mirrors <c>IResourceCandidateAudioService.GetAudioUrlAsync</c>'s fallback. Returns
    /// null if the key does not exist.</summary>
    Task<string?> ResolveUrlAsync(string storageKey, CancellationToken ct = default);

    /// <summary>Streaming fallback backing the app's own endpoint above — mirrors
    /// <c>IResourceCandidateAudioService.GetAudioStreamAsync</c>.</summary>
    Task<LessonMediaStreamResult?> GetStreamAsync(string storageKey, CancellationToken ct = default);
}

public sealed record LessonMediaUploadResult(string StorageKey, string Url, string MimeType);

public sealed record LessonMediaStreamResult(byte[] Content, string ContentType);

public sealed class LessonMediaValidationException(string message) : Exception(message);
