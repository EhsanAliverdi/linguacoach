using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Stores and serves student-uploaded speaking audio files.
///
/// Upload flow:
///   1. StoreTemporaryAsync  — save uploaded bytes under a temp UUID key
///   2. CommitAsync          — rename temp key to final attemptId key after full success
///   3. DeleteTemporaryAsync — delete temp key on any STT/evaluation failure (no orphans)
///
/// Per-student limit is enforced via DB count, not filesystem scan, to avoid race conditions.
/// </summary>
public sealed class SpeakingAudioService
{
    private const int MaxFilesPerStudent = 50;

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
    private readonly IConfiguration _configuration;
    private readonly ILogger<SpeakingAudioService> _logger;

    public SpeakingAudioService(
        LinguaCoachDbContext db,
        IConfiguration configuration,
        ILogger<SpeakingAudioService> logger)
    {
        _db = db;
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

    /// <summary>Writes uploaded audio to a temp file; returns the temp storage key.</summary>
    public async Task<string> StoreTemporaryAsync(
        Stream audioStream,
        string mimeType,
        CancellationToken ct)
    {
        var ext = MimeTypeToExtension(mimeType);
        var tempKey = $"tmp_{Guid.NewGuid():N}{ext}";
        var fullPath = GetAudioPath(tempKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var file = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await audioStream.CopyToAsync(file, ct);

        _logger.LogInformation("Speaking audio stored TempKey={TempKey} Bytes={Bytes}", tempKey, file.Length);
        return tempKey;
    }

    /// <summary>Renames temp key to final attemptId-based key after successful attempt save.</summary>
    public string CommitAudio(string tempKey, Guid attemptId, string mimeType)
    {
        var ext = MimeTypeToExtension(mimeType);
        var finalKey = $"{attemptId:N}{ext}";
        var tempPath = GetAudioPath(tempKey);
        var finalPath = GetAudioPath(finalKey);

        if (File.Exists(tempPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
            File.Move(tempPath, finalPath, overwrite: true);
            _logger.LogInformation("Speaking audio committed TempKey={TempKey} FinalKey={FinalKey}", tempKey, finalKey);
        }

        return finalKey;
    }

    /// <summary>Deletes the temp file on STT or evaluation failure to prevent orphaned files.</summary>
    public void DeleteTemporary(string tempKey)
    {
        try
        {
            var path = GetAudioPath(tempKey);
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("Speaking audio temp deleted TempKey={TempKey}", tempKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete speaking audio temp file TempKey={TempKey}", tempKey);
        }
    }

    /// <summary>Returns the audio bytes and content type for a committed attempt audio file.</summary>
    public async Task<SpeakingAudioFile?> GetAudioAsync(string storageKey, CancellationToken ct)
    {
        var path = GetAudioPath(storageKey);
        if (!File.Exists(path)) return null;

        var bytes = await File.ReadAllBytesAsync(path, ct);
        var contentType = ExtensionToMimeType(Path.GetExtension(storageKey));
        return new SpeakingAudioFile(bytes, contentType);
    }

    private string GetAudioPath(string storageKey)
    {
        var root = Environment.GetEnvironmentVariable("SPEAKING_AUDIO_STORAGE_PATH")
            ?? _configuration["Speaking:AudioStoragePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "app-data", "speaking-audio");
        var safeName = Path.GetFileName(storageKey);
        return Path.GetFullPath(Path.Combine(root, safeName));
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
