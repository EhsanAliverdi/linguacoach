using LinguaCoach.Application.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace LinguaCoach.Infrastructure.Storage;

/// <summary>
/// MinIO-backed IFileStorageService for production use.
///
/// Critical gap addressed: HealthCheckAsync validates bucket existence at startup.
/// A missing bucket is reported as a clear error, not a silent generation failure.
///
/// Key format: {category}/{ownerId}/{uuid}.{ext}
/// Secrets (AccessKey, SecretKey) must come from env vars or Docker secrets — never committed.
/// </summary>
public sealed class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _minio;
    private readonly string _bucketName;
    private readonly int _defaultSignedUrlExpiryMinutes;
    private readonly ILogger<MinioFileStorageService> _logger;

    public MinioFileStorageService(IConfiguration configuration, ILogger<MinioFileStorageService> logger)
    {
        _logger = logger;
        var endpoint = configuration["FileStorage:Minio:Endpoint"]
            ?? Environment.GetEnvironmentVariable("MINIO_ENDPOINT")
            ?? "localhost:9000";
        var accessKey = configuration["FileStorage:Minio:AccessKey"]
            ?? Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY")
            ?? string.Empty;
        var secretKey = configuration["FileStorage:Minio:SecretKey"]
            ?? Environment.GetEnvironmentVariable("MINIO_SECRET_KEY")
            ?? string.Empty;
        _bucketName = configuration["FileStorage:Minio:BucketName"]
            ?? Environment.GetEnvironmentVariable("MINIO_BUCKET_NAME")
            ?? "speakpath-audio";
        var useSsl = bool.TryParse(
            configuration["FileStorage:Minio:UseSSL"] ?? Environment.GetEnvironmentVariable("MINIO_USE_SSL"),
            out var ssl) && ssl;
        _defaultSignedUrlExpiryMinutes = int.TryParse(
            configuration["FileStorage:Minio:SignedUrlExpiryMinutes"] ?? Environment.GetEnvironmentVariable("MINIO_SIGNED_URL_EXPIRY_MINUTES"),
            out var expiry) ? expiry : 10;

        _minio = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();
    }

    public async Task<string> SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default, long? knownSizeBytes = null)
    {
        // Phase 4.7 (2026-07-17 reliable large uploads) — when the caller already knows the exact
        // size (every large-upload part/assembly call does), stream straight to MinIO without
        // buffering the whole payload into a MemoryStream first. Callers that don't supply a size
        // hint (pre-existing, typically-small uploads such as TTS/speaking audio) keep the prior
        // buffered behavior — WithObjectSize needs a known length either way, and buffering a
        // small payload is not the problem this phase fixes.
        if (knownSizeBytes.HasValue)
        {
            var sizedArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(key)
                .WithStreamData(content)
                .WithObjectSize(knownSizeBytes.Value)
                .WithContentType(contentType);

            await _minio.PutObjectAsync(sizedArgs, cancellationToken);
            _logger.LogDebug("MinIO saved (streamed) Key={Key} Bytes={Bytes}", key, knownSizeBytes.Value);
            return key;
        }

        var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        ms.Position = 0;

        var args = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key)
            .WithStreamData(ms)
            .WithObjectSize(ms.Length)
            .WithContentType(contentType);

        await _minio.PutObjectAsync(args, cancellationToken);
        _logger.LogDebug("MinIO saved Key={Key} Bytes={Bytes}", key, ms.Length);
        return key;
    }

    public async Task<Stream> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        // Phase 4.7 (2026-07-17 reliable large uploads) — previously buffered the entire object
        // into a MemoryStream (a 2GB archive meant a 2GB in-process allocation). ZipArchive (Read
        // mode) needs a seekable stream, so a plain forward-only pass-through isn't an option
        // either; a temp-file-backed FileStream gives the same seekability with bounded memory —
        // MinIO's callback stream is copied straight to disk, never fully held in RAM.
        var tempPath = Path.Combine(Path.GetTempPath(), $"minio-read-{Guid.NewGuid():N}.tmp");
        await using (var fileWriter = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        {
            var args = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(key)
                .WithCallbackStream(stream => stream.CopyTo(fileWriter));

            await _minio.GetObjectAsync(args, cancellationToken);
        }

        var readStream = new FileStream(
            tempPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, options: FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        return readStream;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key);
        await _minio.RemoveObjectAsync(args, cancellationToken);
        _logger.LogDebug("MinIO deleted Key={Key}", key);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(key);
            await _minio.StatObjectAsync(args, cancellationToken);
            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
    }

    public async Task MoveAsync(string fromKey, string toKey, CancellationToken cancellationToken = default)
    {
        // MinIO has no native rename — copy then delete.
        var copyArgs = new CopyObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(toKey)
            .WithCopyObjectSource(new CopySourceObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(fromKey));

        await _minio.CopyObjectAsync(copyArgs, cancellationToken);
        await DeleteAsync(fromKey, cancellationToken);
        _logger.LogDebug("MinIO moved From={From} To={To}", fromKey, toKey);
    }

    public async Task<SignedUrlResult> GenerateSignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var expirySeconds = (int)Math.Min(expiry.TotalSeconds, 604800); // MinIO max 7 days
        var args = new PresignedGetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key)
            .WithExpiry(expirySeconds);

        var url = await _minio.PresignedGetObjectAsync(args);
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expirySeconds);
        return new SignedUrlResult(url, expiresAt);
    }

    public string GenerateKey(string ownerId, string category, string extension)
    {
        var safeOwner = ownerId.Replace("-", "").ToLowerInvariant();
        return $"{category}/{safeOwner}/{Guid.NewGuid():N}{extension}";
    }

    public async Task<SignedUrlResult> GenerateUploadUrlAsync(string key, TimeSpan expiry, string contentType, CancellationToken cancellationToken = default)
    {
        var expirySeconds = (int)Math.Min(expiry.TotalSeconds, 604800); // MinIO max 7 days
        var args = new PresignedPutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key)
            .WithExpiry(expirySeconds);

        var url = await _minio.PresignedPutObjectAsync(args);
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expirySeconds);
        return new SignedUrlResult(url, expiresAt);
    }

    /// <summary>
    /// Validates MinIO connectivity and bucket existence.
    /// Called at startup — returns null on success, sanitized error message on failure.
    /// A missing bucket is a critical gap: it would silently fail all TTS generation jobs.
    /// </summary>
    public async Task<string?> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var args = new BucketExistsArgs().WithBucket(_bucketName);
            var exists = await _minio.BucketExistsAsync(args, cancellationToken);
            if (!exists)
                return $"MinIO bucket '{_bucketName}' does not exist. Create it or set MINIO_BUCKET_NAME correctly.";
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MinIO health check failed Bucket={Bucket}", _bucketName);
            return $"MinIO connection failed: {ex.Message}";
        }
    }
}
