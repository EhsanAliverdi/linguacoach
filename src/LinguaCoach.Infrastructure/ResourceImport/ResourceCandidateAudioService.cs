using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase J5c — stores and serves the real uploaded audio file backing a ListeningPassage
/// <see cref="Domain.Entities.ResourceCandidate"/>. Reuses <see cref="IFileStorageService"/>
/// directly (the same abstraction SpeakingAudioService/ListeningAudioService already use) —
/// no temp/commit two-phase step is needed here, unlike SpeakingAudioService's STT pipeline,
/// since there's no partial-success scoring flow to roll back on failure.
/// </summary>
public sealed class ResourceCandidateAudioService : IResourceCandidateAudioService
{
    private const string Category = "resource-import-audio";
    private const long MaxAudioBytes = 20 * 1024 * 1024; // 20 MB — matches the speaking-audio upload ceiling.

    // Same allowlist as SpeakingAudioService — this codebase's one existing precedent for
    // admin/student-uploaded audio, reused rather than inventing a second list.
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/webm",
        "audio/wav",
        "audio/mpeg",
        "audio/mp4",
        "audio/x-m4a",
        "audio/ogg",
    };

    private static readonly TimeSpan SignedUrlExpiry = TimeSpan.FromMinutes(15);

    private readonly LinguaCoachDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ILogger<ResourceCandidateAudioService> _logger;

    public ResourceCandidateAudioService(
        LinguaCoachDbContext db, IFileStorageService storage, ILogger<ResourceCandidateAudioService> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public bool IsAllowedMimeType(string mimeType) =>
        AllowedMimeTypes.Contains(mimeType.Split(';')[0].Trim());

    public long GetMaxAudioBytes() => MaxAudioBytes;

    public async Task<ResourceCandidateAudioUploadResult> UploadAsync(
        Guid candidateId, Stream audioStream, string contentType, CancellationToken ct = default)
    {
        var candidate = await _db.ResourceCandidates.FirstOrDefaultAsync(c => c.Id == candidateId, ct)
            ?? throw new ResourceImportValidationException($"Resource candidate '{candidateId}' was not found.");

        if (candidate.CandidateType != ResourceCandidateType.ListeningPassage)
            throw new ResourceImportValidationException(
                $"Audio can only be uploaded to a ListeningPassage candidate (this one is '{candidate.CandidateType}').");

        if (candidate.IsPublished)
            throw new ResourceImportValidationException(
                "Cannot change the audio file on a resource candidate that has already been published.");

        var mimeType = contentType.Split(';')[0].Trim();
        var ext = MimeTypeToExtension(mimeType);
        var key = $"{Category}/{candidateId:N}{ext}";

        await _storage.SaveAsync(key, audioStream, mimeType, ct);
        candidate.AttachAudio(key, mimeType);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Resource candidate audio stored CandidateId={CandidateId} Key={Key}", candidateId, key);
        return new ResourceCandidateAudioUploadResult(candidateId, mimeType);
    }

    public async Task<ResourceCandidateAudioUrlResult?> GetAudioUrlAsync(Guid candidateId, CancellationToken ct = default)
    {
        var storageKey = await _db.ResourceCandidates
            .Where(c => c.Id == candidateId)
            .Select(c => c.AudioStorageKey)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(storageKey)) return null;

        var signed = await _storage.GenerateSignedUrlAsync(storageKey, SignedUrlExpiry, ct);
        var url = signed.Url.StartsWith("local://", StringComparison.OrdinalIgnoreCase)
                  || signed.Url.StartsWith("fake://", StringComparison.OrdinalIgnoreCase)
            ? $"/api/admin/resource-candidates/{candidateId}/audio"
            : signed.Url;

        return new ResourceCandidateAudioUrlResult(url, signed.ExpiresAt);
    }

    public async Task<ResourceCandidateAudioStreamResult?> GetAudioStreamAsync(Guid candidateId, CancellationToken ct = default)
    {
        var candidate = await _db.ResourceCandidates
            .Where(c => c.Id == candidateId)
            .Select(c => new { c.AudioStorageKey, c.AudioContentType })
            .FirstOrDefaultAsync(ct);
        if (candidate is null || string.IsNullOrWhiteSpace(candidate.AudioStorageKey)) return null;

        if (!await _storage.ExistsAsync(candidate.AudioStorageKey, ct)) return null;

        await using var stream = await _storage.ReadAsync(candidate.AudioStorageKey, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return new ResourceCandidateAudioStreamResult(ms.ToArray(), candidate.AudioContentType ?? "application/octet-stream");
    }

    private static string MimeTypeToExtension(string mimeType) => mimeType switch
    {
        "audio/webm" => ".webm",
        "audio/wav" => ".wav",
        "audio/mpeg" => ".mp3",
        "audio/mp4" => ".mp4",
        "audio/x-m4a" => ".m4a",
        "audio/ogg" => ".ogg",
        _ => ".audio",
    };
}
