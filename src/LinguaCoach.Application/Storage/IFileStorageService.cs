namespace LinguaCoach.Application.Storage;

public interface IFileStorageService
{
    Task<string> SaveAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> ReadAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically moves content from <paramref name="fromKey"/> to <paramref name="toKey"/>.
    /// Used by the speaking audio temp→commit pattern.
    /// </summary>
    Task MoveAsync(
        string fromKey,
        string toKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a short-lived signed URL for direct client access.
    /// For local storage, returns the authenticated streaming endpoint URL instead.
    /// Response must always include the absolute expiry time so callers can pre-fetch.
    /// </summary>
    Task<SignedUrlResult> GenerateSignedUrlAsync(
        string key,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);

    /// <summary>Generates an opaque storage key for a new asset.</summary>
    string GenerateKey(string ownerId, string category, string extension);

    /// <summary>Checks MinIO bucket existence and connectivity. Returns null on success, error message on failure.</summary>
    Task<string?> HealthCheckAsync(CancellationToken cancellationToken = default);
}

public sealed record SignedUrlResult(string Url, DateTimeOffset ExpiresAt);
