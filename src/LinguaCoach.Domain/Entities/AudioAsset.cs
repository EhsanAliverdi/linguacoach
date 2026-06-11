using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Tracks a MinIO/IFileStorageService-backed audio asset (listening TTS, speaking recording, live AI turn).
///
/// Idempotency for TTS: a unique fingerprint of
/// LearningActivityId + TranscriptHash + SpeakerProfileHash + ProviderName + ModelName
/// lets generation jobs reuse an existing asset instead of regenerating audio.
/// </summary>
public sealed class AudioAsset : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public Guid? LearningSessionId { get; private set; }
    public Guid? LearningActivityId { get; private set; }
    public Guid? ActivityAttemptId { get; private set; }

    public AssetType AssetType { get; private set; }

    /// <summary>Opaque storage key (MinIO object key / local relative path). Never exposed to clients.</summary>
    public string ObjectKey { get; private set; }
    public string ContentType { get; private set; }

    public double? DurationSeconds { get; private set; }

    /// <summary>SHA-256 of the transcript text — part of the TTS idempotency fingerprint.</summary>
    public string? TranscriptHash { get; private set; }

    /// <summary>SHA-256 of the canonical speaker-profile JSON — part of the TTS idempotency fingerprint.</summary>
    public string? SpeakerProfileHash { get; private set; }

    /// <summary>Voices/accent/noise/speaker count metadata.</summary>
    public string? SpeakerProfileJson { get; private set; }

    public string? ProviderName { get; private set; }
    public string? ModelName { get; private set; }

    public GenerationStatus GenerationStatus { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private AudioAsset()
    {
        ObjectKey = string.Empty;
        ContentType = string.Empty;
    }

    public AudioAsset(
        Guid studentProfileId,
        AssetType assetType,
        string objectKey,
        string contentType,
        Guid? learningSessionId = null,
        Guid? learningActivityId = null,
        Guid? activityAttemptId = null,
        double? durationSeconds = null,
        string? transcriptHash = null,
        string? speakerProfileHash = null,
        string? speakerProfileJson = null,
        string? providerName = null,
        string? modelName = null,
        GenerationStatus generationStatus = GenerationStatus.Ready)
    {
        if (studentProfileId == Guid.Empty)
            throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("ObjectKey is required.", nameof(objectKey));
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("ContentType is required.", nameof(contentType));

        StudentProfileId = studentProfileId;
        AssetType = assetType;
        ObjectKey = objectKey.Trim();
        ContentType = contentType.Trim();
        LearningSessionId = learningSessionId;
        LearningActivityId = learningActivityId;
        ActivityAttemptId = activityAttemptId;
        DurationSeconds = durationSeconds;
        TranscriptHash = transcriptHash;
        SpeakerProfileHash = speakerProfileHash;
        SpeakerProfileJson = speakerProfileJson;
        ProviderName = providerName;
        ModelName = modelName;
        GenerationStatus = generationStatus;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkReady() => GenerationStatus = GenerationStatus.Ready;
    public void MarkFailed() => GenerationStatus = GenerationStatus.Failed;
    public void SetDuration(double seconds) => DurationSeconds = seconds;
}
