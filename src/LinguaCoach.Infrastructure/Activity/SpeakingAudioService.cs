using LinguaCoach.Application.Storage;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Stores and serves student-uploaded speaking audio files through IFileStorageService.
///
/// Upload flow:
///   1. StoreTemporaryAsync  — save uploaded bytes under a temp UUID key
///   2. CommitAudioAsync     — MoveAsync temp key to final attemptId key after full success
///   3. DeleteTemporaryAsync — delete temp key on any STT/evaluation failure (no orphans)
///
/// Per-student limit is enforced via DB count, not filesystem scan, to avoid race conditions.
/// </summary>
public sealed class SpeakingAudioService
{
    private const int MaxFilesPerStudent = 50;
    private const string Category = "speaking-recordings";

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/webm",
        "audio/wav",
        "audio/mpeg",
        "audio/mp4",
        "audio/x-m4a",
        "audio/ogg",
    };

    private readonly LinguaCoachDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SpeakingAudioService> _logger;

    public SpeakingAudioService(
        LinguaCoachDbContext db,
        IFileStorageService storage,
        IConfiguration configuration,
        ILogger<SpeakingAudioService> logger)
    {
        _db = db;
        _storage = storage;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsAllowedMimeType(string mimeType)
        => AllowedMimeTypes.Contains(mimeType.Split(';')[0].Trim());

    public long GetMaxAudioBytes()
    {
        var rawMb = Environment.GetEnvironmentVariable("STT_MAX_AUDIO_MB");
        if (!int.TryParse(rawMb, out var envMb))
            envMb = _configuration.GetValue<int?>("Stt:MaxAudioMb") ?? 10;
        return envMb * 1024L * 1024L;
    }

    /// <summary>Enforces the 50-audio-file per-student limit via DB count.</summary>
    public async Task<bool> ExceedsStorageLimitAsync(Guid studentProfileId, CancellationToken ct)
    {
        var count = await _db.ActivityAttempts
            .CountAsync(a => a.StudentProfileId == studentProfileId
                          && a.AudioStorageKey != null, ct);
        return count >= MaxFilesPerStudent;
    }

    /// <summary>Writes uploaded audio to a temp storage key; returns that key.</summary>
    public async Task<string> StoreTemporaryAsync(
        Stream audioStream,
        string mimeType,
        CancellationToken ct)
    {
        var ext = MimeTypeToExtension(mimeType);
        var tempKey = $"{Category}/tmp/{Guid.NewGuid():N}{ext}";
        await _storage.SaveAsync(tempKey, audioStream, mimeType, ct);
        _logger.LogInformation("Speaking audio stored TempKey={TempKey}", tempKey);
        return tempKey;
    }

    /// <summary>Moves the temp key to the final attemptId-based key after successful attempt save.</summary>
    public async Task<string> CommitAudioAsync(string tempKey, Guid attemptId, string mimeType, CancellationToken ct = default)
    {
        var ext = MimeTypeToExtension(mimeType);
        var finalKey = $"{Category}/{attemptId:N}{ext}";

        if (await _storage.ExistsAsync(tempKey, ct))
        {
            await _storage.MoveAsync(tempKey, finalKey, ct);
            _logger.LogInformation("Speaking audio committed TempKey={TempKey} FinalKey={FinalKey}", tempKey, finalKey);
        }

        return finalKey;
    }

    /// <summary>Deletes the temp object on STT or evaluation failure to prevent orphans.</summary>
    public async Task DeleteTemporaryAsync(string tempKey, CancellationToken ct = default)
    {
        try
        {
            await _storage.DeleteAsync(tempKey, ct);
            _logger.LogInformation("Speaking audio temp deleted TempKey={TempKey}", tempKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete speaking audio temp object TempKey={TempKey}", tempKey);
        }
    }

    /// <summary>Returns the audio bytes and content type for a committed attempt audio file.</summary>
    public async Task<SpeakingAudioFile?> GetAudioAsync(string storageKey, CancellationToken ct)
    {
        try
        {
            if (!await _storage.ExistsAsync(storageKey, ct)) return null;
            await using var stream = await _storage.ReadAsync(storageKey, ct);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            var contentType = ExtensionToMimeType(Path.GetExtension(storageKey));
            return new SpeakingAudioFile(ms.ToArray(), contentType);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static string MimeTypeToExtension(string mimeType) => mimeType.Split(';')[0].Trim() switch
    {
        "audio/webm" => ".webm",
        "audio/wav" => ".wav",
        "audio/mpeg" => ".mp3",
        "audio/mp4" => ".mp4",
        "audio/x-m4a" => ".m4a",
        "audio/ogg" => ".ogg",
        _ => ".audio",
    };

    private static string ExtensionToMimeType(string ext) => ext.ToLowerInvariant() switch
    {
        ".webm" => "audio/webm",
        ".wav" => "audio/wav",
        ".mp3" => "audio/mpeg",
        ".mp4" => "audio/mp4",
        ".m4a" => "audio/mp4",
        ".ogg" => "audio/ogg",
        _ => "application/octet-stream",
    };
}

public sealed record SpeakingAudioFile(byte[] Bytes, string ContentType);
