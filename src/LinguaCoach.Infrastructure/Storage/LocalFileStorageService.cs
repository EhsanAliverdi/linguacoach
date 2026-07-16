using LinguaCoach.Application.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Storage;

/// <summary>
/// Filesystem-backed IFileStorageService for local development.
/// All audio streaming goes through authenticated API endpoints — keys are never exposed to clients.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _basePath = configuration["FileStorage:LocalBasePath"]
            ?? Environment.GetEnvironmentVariable("FILE_STORAGE_LOCAL_BASE_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "app-data", "audio");
        _logger = logger;
    }

    public async Task<string> SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default, long? knownSizeBytes = null)
    {
        var fullPath = GetFullPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file, cancellationToken);
        _logger.LogDebug("LocalStorage saved Key={Key} Bytes={Bytes}", key, file.Length);
        return key;
    }

    public Task<Stream> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(key);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Storage key not found: {key}", fullPath);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullPath = GetFullPath(key);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogDebug("LocalStorage deleted Key={Key}", key);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(GetFullPath(key)));

    public Task MoveAsync(string fromKey, string toKey, CancellationToken cancellationToken = default)
    {
        var from = GetFullPath(fromKey);
        var to = GetFullPath(toKey);
        if (!File.Exists(from))
            throw new FileNotFoundException($"Source key not found for move: {fromKey}", from);
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Move(from, to, overwrite: true);
        _logger.LogDebug("LocalStorage moved From={From} To={To}", fromKey, toKey);
        return Task.CompletedTask;
    }

    public Task<SignedUrlResult> GenerateSignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        // Local storage has no signed URLs — callers fall back to the authenticated streaming endpoint.
        // Return a placeholder that signals "use streaming endpoint" to the caller.
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);
        return Task.FromResult(new SignedUrlResult($"local://{key}", expiresAt));
    }

    public string GenerateKey(string ownerId, string category, string extension)
    {
        var safeOwner = ownerId.Replace("-", "").ToLowerInvariant();
        return $"{category}/{safeOwner}/{Guid.NewGuid():N}{extension}";
    }

    public Task<SignedUrlResult> GenerateUploadUrlAsync(string key, TimeSpan expiry, string contentType, CancellationToken cancellationToken = default)
    {
        // Phase 4.7 (2026-07-17 reliable large uploads) — local storage has no signed-PUT concept
        // and, prior to this phase, silently returned an unusable "local://" marker with no
        // receiving endpoint on the other end (confirmed broken end-to-end — see
        // docs/reviews/2026-07-15-phase-4-1-large-import-validation-and-gap-audit.md Part C).
        // Rather than continue shipping that dead end, fail immediately and actionably: the Import
        // Package upload flow now always goes through the resumable chunked-upload session
        // endpoints (IImportUploadSessionService), which write through SaveAsync/ReadAsync and so
        // work identically on Local or MinIO — nothing should call this method for a package
        // upload anymore. Any other caller still requesting a presigned PUT against Local storage
        // gets a clear, immediate error instead of a broken URL discovered later.
        throw new NotSupportedException(
            "Local file storage does not support presigned direct-to-storage uploads. " +
            "Use the resumable chunked-upload session endpoints (POST .../upload-sessions) instead, " +
            "which work against both Local and MinIO backends.");
    }

    public Task<string?> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(_basePath);
            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            return Task.FromResult<string?>($"Local storage path unavailable: {ex.Message}");
        }
    }

    private string GetFullPath(string key)
    {
        var safeName = key.Replace("..", "").TrimStart('/');
        return Path.GetFullPath(Path.Combine(_basePath, safeName));
    }
}
