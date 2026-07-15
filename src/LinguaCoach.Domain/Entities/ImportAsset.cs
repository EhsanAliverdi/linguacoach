using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase 4 (Part D) — one file within an <see cref="ImportPackage"/>'s inventory. Immutable once
/// stored (content never changes after upload; only role/processing-state/validation fields
/// mutate). May be linked to zero or more <see cref="ResourceCandidate"/> rows via
/// <see cref="ImportCandidateAssetLink"/> — supports both "one candidate, several assets" (e.g. a
/// Listening candidate's audio + transcript) and "one asset, several candidates" (e.g. a licence
/// file shared by every candidate in the package).
/// </summary>
public sealed class ImportAsset : BaseEntity
{
    public Guid ImportPackageId { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;
    /// <summary>Package-relative path, e.g. "listening-unit-01/audio.mp3" — preserved even though
    /// storage itself is flat/opaque, so folder-pattern mapping rules (Part E) have something to
    /// match against.</summary>
    public string RelativePath { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;

    public string MimeType { get; private set; } = string.Empty;
    public ImportAssetMediaType DetectedMediaType { get; private set; }
    public string FileExtension { get; private set; } = string.Empty;

    public long? CompressedSizeBytes { get; private set; }
    public long UncompressedSizeBytes { get; private set; }
    public string Checksum { get; private set; } = string.Empty;

    public ImportAssetRole Role { get; private set; }
    /// <summary>Origin of <see cref="Role"/> — set by AI suggestion or admin correction. See
    /// <see cref="MetadataOrigin"/>.</summary>
    public MetadataOrigin RoleOrigin { get; private set; }

    public ImportAssetProcessingState ProcessingState { get; private set; }
    public string? ValidationErrorsJson { get; private set; }
    public string? ValidationWarningsJson { get; private set; }

    public DateTimeOffset UploadedAtUtc { get; private set; }
    /// <summary>Free-form JSON — STT provider/model/confidence, AI role-suggestion rationale, etc.
    /// Deliberately generic (mirrors <see cref="ResourceCandidate.AiAnalysisJson"/>'s existing
    /// convention) since the shape varies by media type; each well-known field this phase actually
    /// reads back out is documented, not opaque.</summary>
    public string? ProcessingMetadataJson { get; private set; }

    private ImportAsset() { }

    public ImportAsset(
        Guid importPackageId,
        string originalFileName,
        string relativePath,
        string storageKey,
        string mimeType,
        ImportAssetMediaType detectedMediaType,
        string fileExtension,
        long uncompressedSizeBytes,
        string checksum,
        DateTimeOffset uploadedAtUtc,
        long? compressedSizeBytes = null,
        ImportAssetRole role = ImportAssetRole.Unknown)
    {
        if (importPackageId == Guid.Empty)
            throw new ArgumentException("ImportPackageId must not be empty.", nameof(importPackageId));
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("OriginalFileName is required.", nameof(originalFileName));
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("StorageKey is required.", nameof(storageKey));
        if (string.IsNullOrWhiteSpace(checksum))
            throw new ArgumentException("Checksum is required.", nameof(checksum));
        if (uncompressedSizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(uncompressedSizeBytes));

        ImportPackageId = importPackageId;
        OriginalFileName = originalFileName.Trim();
        RelativePath = (relativePath ?? originalFileName).Trim();
        StorageKey = storageKey.Trim();
        MimeType = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType.Trim();
        DetectedMediaType = detectedMediaType;
        FileExtension = (fileExtension ?? string.Empty).Trim().ToLowerInvariant();
        UncompressedSizeBytes = uncompressedSizeBytes;
        CompressedSizeBytes = compressedSizeBytes;
        Checksum = checksum.Trim();
        UploadedAtUtc = uploadedAtUtc;
        Role = role;
        RoleOrigin = role == ImportAssetRole.Unknown ? MetadataOrigin.Unknown : MetadataOrigin.DeterministicallyExtracted;
        ProcessingState = ImportAssetProcessingState.Pending;
    }

    public void MarkInspected() => ProcessingState = ImportAssetProcessingState.Inspected;

    public void MarkRejected(string validationErrorsJson)
    {
        if (string.IsNullOrWhiteSpace(validationErrorsJson))
            throw new ArgumentException("ValidationErrorsJson is required.", nameof(validationErrorsJson));

        ProcessingState = ImportAssetProcessingState.Rejected;
        ValidationErrorsJson = validationErrorsJson;
    }

    public void MarkProcessed(string? processingMetadataJson = null)
    {
        ProcessingState = ImportAssetProcessingState.Processed;
        if (processingMetadataJson is not null) ProcessingMetadataJson = processingMetadataJson;
    }

    public void MarkFailed(string reason)
    {
        ProcessingState = ImportAssetProcessingState.Failed;
        ValidationErrorsJson = System.Text.Json.JsonSerializer.Serialize(new[] { reason });
    }

    /// <summary>AI-suggested role — never overwrites an administrator correction (Part F
    /// precedence: AdministratorCorrected always wins over AIInferred).</summary>
    public void SuggestRole(ImportAssetRole role, double? confidence = null)
    {
        if (RoleOrigin == MetadataOrigin.AdministratorCorrected) return;

        Role = role;
        RoleOrigin = MetadataOrigin.AIInferred;
        if (confidence is not null)
        {
            var meta = string.IsNullOrWhiteSpace(ProcessingMetadataJson)
                ? new Dictionary<string, object?>()
                : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(ProcessingMetadataJson!) ?? new();
            meta["roleSuggestionConfidence"] = confidence;
            ProcessingMetadataJson = System.Text.Json.JsonSerializer.Serialize(meta);
        }
    }

    public void CorrectRole(ImportAssetRole role)
    {
        Role = role;
        RoleOrigin = MetadataOrigin.AdministratorCorrected;
    }

    public void SetWarnings(string? validationWarningsJson) => ValidationWarningsJson = validationWarningsJson;
}
