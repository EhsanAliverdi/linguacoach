using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase 4.7 (2026-07-17 reliable large uploads) — one received, bounded-size chunk of an
/// <see cref="ImportUploadSession"/>'s archive. Bytes for the part itself live under
/// <see cref="StorageKey"/> in the configured <c>IFileStorageService</c> backend (Local, MinIO, or
/// the test fake) — this row is just the bookkeeping (which part numbers have arrived, their size
/// and checksum) that lets the client resume an interrupted upload or retry one failed part
/// without resending everything.
///
/// Re-uploading the same <see cref="PartNumber"/> replaces the row and the underlying stored
/// bytes — see <c>ImportUploadSessionService.UploadPartAsync</c>.
/// </summary>
public sealed class ImportUploadSessionPart : BaseEntity
{
    public Guid ImportUploadSessionId { get; private set; }
    public int PartNumber { get; private set; }
    public long SizeBytes { get; private set; }
    public string? Sha256Checksum { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public DateTimeOffset UploadedAtUtc { get; private set; }

    private ImportUploadSessionPart() { }

    public ImportUploadSessionPart(
        Guid importUploadSessionId,
        int partNumber,
        long sizeBytes,
        string storageKey,
        DateTimeOffset uploadedAtUtc,
        string? sha256Checksum = null)
    {
        if (importUploadSessionId == Guid.Empty)
            throw new ArgumentException("ImportUploadSessionId must not be empty.", nameof(importUploadSessionId));
        if (partNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(partNumber), "PartNumber is 1-based.");
        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "SizeBytes must be greater than zero.");
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("StorageKey is required.", nameof(storageKey));

        ImportUploadSessionId = importUploadSessionId;
        PartNumber = partNumber;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        UploadedAtUtc = uploadedAtUtc;
        Sha256Checksum = sha256Checksum;
    }

    public void Replace(long sizeBytes, string storageKey, DateTimeOffset uploadedAtUtc, string? sha256Checksum)
    {
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        UploadedAtUtc = uploadedAtUtc;
        Sha256Checksum = sha256Checksum;
    }
}
