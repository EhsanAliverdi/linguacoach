using LinguaCoach.Application.Storage;
using System.Collections.Concurrent;

namespace LinguaCoach.Infrastructure.Storage;

/// <summary>
/// In-memory IFileStorageService for unit and integration tests.
/// No filesystem side effects. Fast. Thread-safe.
/// </summary>
public sealed class FakeFileStorageService : IFileStorageService
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string ContentType)> _store = new();

    public async Task<string> SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default, long? knownSizeBytes = null)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        _store[key] = (ms.ToArray(), contentType);
        LastSaveUsedKnownSize = knownSizeBytes.HasValue;
        return key;
    }

    /// <summary>Phase 4.7 test hook — records whether the most recent <see cref="SaveAsync"/>
    /// call supplied a size hint, so tests can assert large-upload code paths avoid the
    /// "buffer to learn the length" pattern without needing a real MinIO instance.</summary>
    public bool? LastSaveUsedKnownSize { get; private set; }

    public Task<Stream> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(key, out var entry))
            throw new FileNotFoundException($"Fake storage key not found: {key}");
        return Task.FromResult<Stream>(new MemoryStream(entry.Bytes));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.ContainsKey(key));

    public Task MoveAsync(string fromKey, string toKey, CancellationToken cancellationToken = default)
    {
        if (!_store.TryRemove(fromKey, out var entry))
            throw new FileNotFoundException($"Fake storage source key not found for move: {fromKey}");
        _store[toKey] = entry;
        return Task.CompletedTask;
    }

    public Task<SignedUrlResult> GenerateSignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);
        return Task.FromResult(new SignedUrlResult($"fake://{key}", expiresAt));
    }

    public string GenerateKey(string ownerId, string category, string extension)
        => $"{category}/{ownerId.Replace("-", "")}/{Guid.NewGuid():N}{extension}";

    public Task<SignedUrlResult> GenerateUploadUrlAsync(string key, TimeSpan expiry, string contentType, CancellationToken cancellationToken = default)
        => Task.FromResult(new SignedUrlResult($"fake-upload://{key}", DateTimeOffset.UtcNow.Add(expiry)));

    public Task<string?> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    /// <summary>Returns all stored keys — useful for asserting storage state in tests.</summary>
    public IReadOnlyCollection<string> Keys => _store.Keys.ToArray();

    /// <summary>Returns the bytes stored under a key — useful for asserting content in tests.</summary>
    public byte[]? GetBytes(string key)
        => _store.TryGetValue(key, out var entry) ? entry.Bytes : null;
}
