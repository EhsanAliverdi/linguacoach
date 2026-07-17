using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Admin endpoints for MinIO/object-storage integration health. Secrets are never returned:
/// GET reports a masked "configured" state only.
///
/// Phase rehaul (2026-07-17) — relocated verbatim from the deleted <c>AdminGenerationController</c>
/// (which bundled unrelated lesson-generation and storage concerns). See
/// docs/reviews/2026-07-17-today-delivery-health-bank-first-rehaul-review.md.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminIntegrationsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IFileStorageService _storage;

    public AdminIntegrationsController(IConfiguration config, IFileStorageService storage)
    {
        _config = config;
        _storage = storage;
    }

    [HttpGet("integrations/storage")]
    public IActionResult GetStorage()
    {
        var provider = _config["FileStorage:Provider"]
            ?? Environment.GetEnvironmentVariable("FILE_STORAGE_PROVIDER") ?? "Local";
        var endpoint = _config["FileStorage:Minio:Endpoint"] ?? Environment.GetEnvironmentVariable("MINIO_ENDPOINT");
        var bucket = _config["FileStorage:Minio:BucketName"] ?? Environment.GetEnvironmentVariable("MINIO_BUCKET_NAME");
        var useSsl = _config["FileStorage:Minio:UseSSL"] ?? Environment.GetEnvironmentVariable("MINIO_USE_SSL");
        var expiry = _config["FileStorage:Minio:SignedUrlExpiryMinutes"] ?? Environment.GetEnvironmentVariable("MINIO_SIGNED_URL_EXPIRY_MINUTES");

        return Ok(new
        {
            provider,
            endpoint,
            bucketName = bucket,
            // Secrets are never returned — only a masked state.
            accessKey = HasValue("FileStorage:Minio:AccessKey", "MINIO_ACCESS_KEY") ? "configured" : null,
            secretKey = HasValue("FileStorage:Minio:SecretKey", "MINIO_SECRET_KEY") ? "configured" : null,
            useSsl = bool.TryParse(useSsl, out var ssl) && ssl,
            signedUrlExpiryMinutes = int.TryParse(expiry, out var e) ? e : 10
        });
    }

    [HttpPatch("integrations/storage")]
    public IActionResult UpdateStorage([FromBody] StorageSettingsRequest req)
    {
        // Storage credentials are sourced from environment / Docker secrets and applied on restart.
        // We acknowledge the request without ever echoing secrets back to the client.
        return Ok(new
        {
            applied = true,
            note = "Storage configuration is sourced from environment variables / Docker secrets and applied on restart. Secrets are never returned."
        });
    }

    [HttpPost("integrations/storage/test")]
    public async Task<IActionResult> TestStorage(CancellationToken ct)
    {
        var error = await _storage.HealthCheckAsync(ct);
        return Ok(new
        {
            ok = error is null,
            lastCheckedUtc = DateTime.UtcNow.ToString("o"),
            error // sanitized message from the storage implementation
        });
    }

    private bool HasValue(string configKey, string envKey)
        => !string.IsNullOrWhiteSpace(_config[configKey])
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envKey));
}

public sealed record StorageSettingsRequest(
    string? Provider,
    string? Endpoint,
    string? BucketName,
    string? AccessKey,
    string? SecretKey,
    bool? UseSsl,
    int? SignedUrlExpiryMinutes);
