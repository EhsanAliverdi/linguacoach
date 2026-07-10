using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Admin endpoints for lesson-generation settings, MinIO/object-storage integration health,
/// and background generation job visibility.
///
/// Secrets are never returned: storage GET reports a masked "configured" state only.
///
/// Phase I2B — the legacy generation pipeline (LessonBatchGenerationJob) that RetryBatch and
/// GenerateLessons used to trigger was deleted; Today is module-only now. Both actions below are
/// kept (rather than deleted) because the surrounding "Today Delivery Health" admin page
/// (admin-lessons.component.ts) has substantial unrelated live functionality — readiness pool
/// health, review scaffold pilot monitoring, mastery validation — that would break if this whole
/// controller/page were removed. They now return an honest "nothing to generate" response instead
/// of silently doing nothing or erroring. See
/// docs/reviews/2026-07-10-phase-i2b-today-module-only-collapse-review.md.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminGenerationController : ControllerBase
{
    private readonly LinguaCoachDbContext _db;
    private readonly IConfiguration _config;
    private readonly IFileStorageService _storage;

    public AdminGenerationController(
        LinguaCoachDbContext db,
        IConfiguration config,
        IFileStorageService storage)
    {
        _db = db;
        _config = config;
        _storage = storage;
    }

    // ── Generation settings (T9) ────────────────────────────────────────────────

    [HttpGet("generation/settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var s = await _db.LessonGenerationSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (s is null) return Ok(new LinguaCoach.Domain.Entities.LessonGenerationSettings());
        return Ok(ToSettingsDto(s));
    }

    [HttpPatch("generation/settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] GenerationSettingsRequest req, CancellationToken ct)
    {
        var s = await _db.LessonGenerationSettings.FirstOrDefaultAsync(ct);
        if (s is null)
        {
            s = new LinguaCoach.Domain.Entities.LessonGenerationSettings();
            _db.LessonGenerationSettings.Add(s);
        }

        try
        {
            s.Update(
                req.ReadyLessonBufferSize, req.RefillThreshold, req.RefillBatchSize,
                req.MaxGenerationAttempts, req.GenerationTimeoutSeconds, req.TtsTimeoutSeconds,
                req.MaxConcurrentGenerationJobs, req.MaxConcurrentTtsJobs,
                req.EnableBackgroundGeneration, req.EnableTtsGeneration,
                req.PracticeGymReadyExercisesPerType, req.PracticeGymRefillThresholdPerType,
                req.PracticeGymRefillCountPerType);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _db.SaveChangesAsync(ct);
        return Ok(ToSettingsDto(s));
    }

    // ── Storage integration (T16) ───────────────────────────────────────────────

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

    // ── Generation batches (T16) ─────────────────────────────────────────────────

    [HttpGet("generation/batches")]
    public async Task<IActionResult> GetBatches(CancellationToken ct)
    {
        var batches = await _db.GenerationBatches.AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Take(100)
            .Select(b => new
            {
                id = b.Id,
                studentProfileId = b.StudentProfileId,
                triggerReason = b.TriggerReason.ToString(),
                status = b.Status.ToString(),
                requestedSessionCount = b.RequestedSessionCount,
                completedSessionCount = b.CompletedSessionCount,
                providerName = b.ProviderName,
                modelName = b.ModelName,
                startedAtUtc = b.StartedAtUtc,
                completedAtUtc = b.CompletedAtUtc,
                failureReason = b.FailureReason,
                createdAt = b.CreatedAt
            })
            .ToListAsync(ct);

        var queued = batches.Count(b => b.status == nameof(GenerationBatchStatus.Queued));
        var running = batches.Count(b => b.status == nameof(GenerationBatchStatus.Running));
        var failed = batches.Count(b => b.status == nameof(GenerationBatchStatus.Failed));
        var lastSuccess = batches
            .Where(b => b.status == nameof(GenerationBatchStatus.Completed))
            .Select(b => b.completedAtUtc)
            .FirstOrDefault();

        // Ready lesson buffer count per student.
        var bufferCounts = await _db.LearningSessions.AsNoTracking()
            .Where(s => s.StudentProfileId != null
                     && s.Status == SessionStatus.NotStarted
                     && s.GenerationStatus == GenerationStatus.Ready)
            .GroupBy(s => s.StudentProfileId!.Value)
            .Select(g => new { studentProfileId = g.Key, readyCount = g.Count() })
            .ToListAsync(ct);

        return Ok(new
        {
            summary = new { queued, running, failed, lastSuccessfulGenerationUtc = lastSuccess },
            readyBufferPerStudent = bufferCounts,
            batches
        });
    }

    [HttpPost("generation/batches/{id:guid}/cancel")]
    public async Task<IActionResult> CancelBatch(Guid id, CancellationToken ct)
    {
        var batch = await _db.GenerationBatches.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (batch is null) return NotFound();
        if (batch.Status != GenerationBatchStatus.Queued && batch.Status != GenerationBatchStatus.Running)
            return BadRequest(new { error = "Only queued or running batches can be cancelled." });

        batch.MarkCancelledByAdmin();
        await _db.SaveChangesAsync(ct);

        return Ok(new { cancelled = true });
    }

    /// <summary>
    /// Phase I2B — retired. LessonBatchGenerationJob (the job this used to trigger) was deleted;
    /// Today is module-only now and there is nothing left to regenerate. Kept as a stable,
    /// honest endpoint (rather than deleted) for the still-live admin batches table's retry button.
    /// </summary>
    [HttpPost("generation/batches/{id:guid}/retry")]
    public async Task<IActionResult> RetryBatch(Guid id, CancellationToken ct)
    {
        var batch = await _db.GenerationBatches.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (batch is null) return NotFound();

        return Conflict(new
        {
            error = "Lesson batch generation has been retired. Today is module-only now — there is nothing left to regenerate."
        });
    }

    /// <summary>
    /// Phase I2B — retired. LessonBatchGenerationJob (the job this used to trigger) was deleted;
    /// Today is module-only now and there is nothing left to generate. Kept as a stable, honest
    /// endpoint (rather than deleted) for the still-live admin "generate for student" form.
    /// </summary>
    [HttpPost("students/{id:guid}/generate-lessons")]
    public async Task<IActionResult> GenerateLessons(Guid id, [FromQuery] int? count, CancellationToken ct)
    {
        var profileExists = await _db.StudentProfiles.AnyAsync(p => p.Id == id, ct);
        if (!profileExists) return NotFound();

        return Conflict(new
        {
            error = "Lesson batch generation has been retired. Today is module-only now — there is nothing left to generate."
        });
    }

    private bool HasValue(string configKey, string envKey)
        => !string.IsNullOrWhiteSpace(_config[configKey])
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envKey));

    private static object ToSettingsDto(LinguaCoach.Domain.Entities.LessonGenerationSettings s) => new
    {
        s.ReadyLessonBufferSize,
        s.RefillThreshold,
        s.RefillBatchSize,
        s.MaxGenerationAttempts,
        s.GenerationTimeoutSeconds,
        s.TtsTimeoutSeconds,
        s.MaxConcurrentGenerationJobs,
        s.MaxConcurrentTtsJobs,
        s.EnableBackgroundGeneration,
        s.EnableTtsGeneration,
        s.PracticeGymReadyExercisesPerType,
        s.PracticeGymRefillThresholdPerType,
        s.PracticeGymRefillCountPerType,
        s.UpdatedAtUtc
    };
}

public sealed record GenerationSettingsRequest(
    int ReadyLessonBufferSize,
    int RefillThreshold,
    int RefillBatchSize,
    int MaxGenerationAttempts,
    int GenerationTimeoutSeconds,
    int TtsTimeoutSeconds,
    int MaxConcurrentGenerationJobs,
    int MaxConcurrentTtsJobs,
    bool EnableBackgroundGeneration,
    bool EnableTtsGeneration,
    int PracticeGymReadyExercisesPerType,
    int PracticeGymRefillThresholdPerType,
    int PracticeGymRefillCountPerType);

public sealed record StorageSettingsRequest(
    string? Provider,
    string? Endpoint,
    string? BucketName,
    string? AccessKey,
    string? SecretKey,
    bool? UseSsl,
    int? SignedUrlExpiryMinutes);
