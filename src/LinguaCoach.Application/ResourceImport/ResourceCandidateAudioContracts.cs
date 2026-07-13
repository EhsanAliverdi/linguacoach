namespace LinguaCoach.Application.ResourceImport;

// ── Phase J5c — real audio-file upload for ListeningPassage resource candidates. Separate from
// the text/JSON staging pipeline (Phase E1): a Listening candidate is staged first (title/
// transcript, like any other row), then an admin uploads its actual audio file via this service.
// Mirrors the existing SpeakingAudioService/ActivityController upload pattern (mime-type
// allowlist, size cap, IFileStorageService) rather than inventing a new one. ──

public sealed record ResourceCandidateAudioUploadResult(
    Guid CandidateId,
    string AudioContentType);

public sealed record ResourceCandidateAudioUrlResult(
    string Url,
    DateTimeOffset ExpiresAt);

public sealed record ResourceCandidateAudioStreamResult(
    byte[] Bytes,
    string ContentType);

public interface IResourceCandidateAudioService
{
    bool IsAllowedMimeType(string mimeType);

    long GetMaxAudioBytes();

    /// <summary>Uploads and attaches an audio file to a ListeningPassage candidate. Throws
    /// <see cref="ResourceImportValidationException"/> if the candidate doesn't exist, isn't a
    /// ListeningPassage, or is already published (audio is immutable post-publish).</summary>
    Task<ResourceCandidateAudioUploadResult> UploadAsync(
        Guid candidateId, Stream audioStream, string contentType, CancellationToken ct = default);

    /// <summary>Short-lived signed URL for direct client playback (or, for local storage, a
    /// marker the caller resolves to the streaming endpoint) — null if no audio is attached.</summary>
    Task<ResourceCandidateAudioUrlResult?> GetAudioUrlAsync(Guid candidateId, CancellationToken ct = default);

    /// <summary>Raw bytes for the local-storage streaming fallback — null if no audio is attached.</summary>
    Task<ResourceCandidateAudioStreamResult?> GetAudioStreamAsync(Guid candidateId, CancellationToken ct = default);
}
