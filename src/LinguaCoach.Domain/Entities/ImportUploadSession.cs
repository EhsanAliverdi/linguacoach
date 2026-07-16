using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase 4.7 (2026-07-17 reliable large uploads) — tracks an in-progress, resumable, chunked
/// upload of one Import Package ZIP archive. Bytes are never accepted through a single
/// large request body; the client (or the admin's browser) uploads bounded-size parts to the
/// API one at a time (see <see cref="ImportUploadSessionPart"/>), any part can be retried or
/// re-uploaded, and the session can be resumed after a page refresh using the same
/// <see cref="BaseEntity.Id"/>. Only on <see cref="Complete"/> are the parts assembled and
/// handed to <c>IFileStorageService</c> as a single object — see
/// <c>ImportUploadSessionService</c> for the assembly step, which streams from part storage keys
/// rather than buffering the whole archive in memory.
///
/// This replaces the single-shot presigned-PUT flow (<c>IImportPackageUploadService.
/// RequestUploadAsync</c>) as the path the Import UI actually uses for ZIP archives — that older
/// service/endpoints remain for API compatibility but are no longer called by the Angular client.
/// </summary>
public sealed class ImportUploadSession : BaseEntity
{
    public Guid CefrResourceSourceId { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    public string OriginalFileName { get; private set; } = string.Empty;
    public long DeclaredTotalSizeBytes { get; private set; }
    public long PartSizeBytes { get; private set; }
    public int TotalPartsExpected { get; private set; }

    /// <summary>Optional whole-file checksum supplied by the client at session creation, verified
    /// against the assembled archive's actual checksum on <see cref="Complete"/>.</summary>
    public string? DeclaredChecksumSha256 { get; private set; }

    public string FinalStorageKey { get; private set; } = string.Empty;
    public string? Notes { get; private set; }

    public ImportUploadSessionStatus Status { get; private set; }

    /// <summary>Set once <see cref="Complete"/> succeeds — a repeated completion call for a
    /// session already in this state must return the same package, never create a second one.</summary>
    public Guid? ImportPackageId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? AbortedAtUtc { get; private set; }

    /// <summary>Phase 4.8 — real EF concurrency token (see <c>ImportUploadSessionConfiguration</c>)
    /// so two concurrent <c>CompleteAsync</c> calls for the same session cannot both pass the
    /// "not yet completed" check and both create an <see cref="ImportPackage"/> — the second
    /// writer's SaveChanges fails atomically and the caller falls back to the idempotent
    /// already-completed path.</summary>
    public Guid ConcurrencyStamp { get; private set; }

    private ImportUploadSession() { }

    public ImportUploadSession(
        Guid cefrResourceSourceId,
        string originalFileName,
        long declaredTotalSizeBytes,
        long partSizeBytes,
        string finalStorageKey,
        DateTimeOffset createdAtUtc,
        TimeSpan expiry,
        Guid? createdByUserId = null,
        string? declaredChecksumSha256 = null,
        string? notes = null)
    {
        if (cefrResourceSourceId == Guid.Empty)
            throw new ArgumentException("CefrResourceSourceId must not be empty.", nameof(cefrResourceSourceId));
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("OriginalFileName is required.", nameof(originalFileName));
        if (declaredTotalSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(declaredTotalSizeBytes), "Declared size must be greater than zero.");
        if (partSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(partSizeBytes), "Part size must be greater than zero.");
        if (string.IsNullOrWhiteSpace(finalStorageKey))
            throw new ArgumentException("FinalStorageKey is required.", nameof(finalStorageKey));

        CefrResourceSourceId = cefrResourceSourceId;
        OriginalFileName = originalFileName.Trim();
        DeclaredTotalSizeBytes = declaredTotalSizeBytes;
        PartSizeBytes = partSizeBytes;
        TotalPartsExpected = (int)Math.Ceiling(declaredTotalSizeBytes / (double)partSizeBytes);
        FinalStorageKey = finalStorageKey;
        CreatedByUserId = createdByUserId;
        DeclaredChecksumSha256 = declaredChecksumSha256;
        Notes = notes?.Trim();
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = createdAtUtc.Add(expiry);
        Status = ImportUploadSessionStatus.Created;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public bool IsOwnedBy(Guid? userId)
        => !CreatedByUserId.HasValue || !userId.HasValue || CreatedByUserId.Value == userId.Value;

    public bool IsExpired(DateTimeOffset nowUtc)
        => nowUtc > ExpiresAtUtc && Status is ImportUploadSessionStatus.Created or ImportUploadSessionStatus.InProgress;

    public void MarkInProgress()
    {
        if (Status == ImportUploadSessionStatus.Created)
        {
            Status = ImportUploadSessionStatus.InProgress;
            ConcurrencyStamp = Guid.NewGuid();
        }
    }

    public void Complete(Guid importPackageId, DateTimeOffset completedAtUtc)
    {
        Status = ImportUploadSessionStatus.Completed;
        ImportPackageId = importPackageId;
        CompletedAtUtc = completedAtUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public void Abort(DateTimeOffset abortedAtUtc)
    {
        Status = ImportUploadSessionStatus.Aborted;
        AbortedAtUtc = abortedAtUtc;
        ConcurrencyStamp = Guid.NewGuid();
    }
}
