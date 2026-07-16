namespace LinguaCoach.Application.ResourceImport;

// ── Phase 4.6 — authenticated audio access for a published Listening Resource Bank item. Mirrors
// IResourceCandidateAudioService's signed-URL/stream-fallback pattern exactly (see
// ResourceCandidateAudioService's doc comment) rather than inventing a second one. The storage key
// itself is never accepted from — or returned to — the client: it is read from the item's own
// ContentJson (ListeningPassageContent.AudioStorageKey), keyed only by the ResourceBankItem's own
// Id, so there is no way for a caller to request an arbitrary storage key or another item's audio. ──

public sealed record ResourceBankAudioUrlResult(string Url, DateTimeOffset ExpiresAt);

public sealed record ResourceBankAudioStreamResult(byte[] Bytes, string ContentType);

public interface IResourceBankMediaService
{
    /// <summary>Short-lived signed URL for direct client playback (or, for local storage, a marker
    /// the caller resolves to the streaming endpoint below) — null when the item doesn't exist,
    /// isn't a Listening item, or has no audio recorded.</summary>
    Task<ResourceBankAudioUrlResult?> GetAudioUrlAsync(Guid resourceBankItemId, CancellationToken ct = default);

    /// <summary>Raw bytes for the local-storage streaming fallback — null under the same
    /// conditions as <see cref="GetAudioUrlAsync"/>, or when the storage backend reports the key no
    /// longer exists (a real live-storage check, unlike the Candidate Review preview DTO's
    /// necessarily-cheap heuristic).</summary>
    Task<ResourceBankAudioStreamResult?> GetAudioStreamAsync(Guid resourceBankItemId, CancellationToken ct = default);
}
