using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ResourceImport;

// ── Phase 4.7 (2026-07-17 reliable large uploads) — replaces the single-shot presigned-PUT ZIP
// upload (IImportPackageUploadService.RequestUploadAsync, still present for API compatibility but
// no longer called by the Import UI) with a resumable, API-proxied, bounded-chunk upload:
//
//   create session → PUT one part at a time (any part retryable, any subset resumable using the
//   same sessionId) → complete (assembles parts, verifies size/checksum, creates the
//   ImportPackage, runs the existing ZIP inspection — never before every part is verified
//   present) → abort (any time before complete; a completed session cannot be aborted and an
//   aborted session can never complete).
//
// Chosen over client→storage-direct per-part presigned PUTs because the installed Minio .NET SDK
// (6.0.4, confirmed via reflection — no InitiateMultipartUpload/UploadPart/CompleteMultipartUpload
// surface exists) exposes no public S3 multipart primitives. Proxying bounded chunks through the
// API instead (explicitly an acceptable option per this phase's brief) has the added benefit of
// working identically for the Local and MinIO storage backends — no local-vs-MinIO branching is
// needed for the chunk mechanism itself, since each part and the assembled archive are written via
// the existing IFileStorageService.SaveAsync/ReadAsync abstraction. ──

public sealed record CreateImportUploadSessionCommand(
    Guid CefrResourceSourceId,
    string OriginalFileName,
    long DeclaredTotalSizeBytes,
    Guid? CreatedByUserId,
    string? DeclaredChecksumSha256 = null,
    string? Notes = null);

public sealed record CreateImportUploadSessionResult(
    Guid SessionId,
    long PartSizeBytes,
    int TotalPartsExpected,
    DateTimeOffset ExpiresAtUtc);

public sealed record UploadImportSessionPartCommand(
    Guid SessionId,
    int PartNumber,
    Stream Content,
    long DeclaredSizeBytes,
    Guid? RequestingUserId,
    string? DeclaredChecksumSha256 = null);

public sealed record UploadImportSessionPartResult(
    int PartNumber,
    long SizeBytes,
    string? Sha256Checksum,
    DateTimeOffset UploadedAtUtc);

public sealed record ImportUploadSessionPartSummary(
    int PartNumber,
    long SizeBytes,
    string? Sha256Checksum,
    DateTimeOffset UploadedAtUtc);

public sealed record ImportUploadSessionStatusDto(
    Guid SessionId,
    ImportUploadSessionStatus Status,
    string OriginalFileName,
    long DeclaredTotalSizeBytes,
    long PartSizeBytes,
    int TotalPartsExpected,
    IReadOnlyList<ImportUploadSessionPartSummary> UploadedParts,
    Guid? ImportPackageId,
    DateTimeOffset ExpiresAtUtc);

public sealed record CompleteImportUploadSessionCommand(Guid SessionId, Guid? RequestingUserId);

public sealed record AbortImportUploadSessionCommand(Guid SessionId, Guid? RequestingUserId);

public interface IImportUploadSessionService
{
    Task<CreateImportUploadSessionResult> CreateAsync(CreateImportUploadSessionCommand command, CancellationToken ct = default);

    Task<UploadImportSessionPartResult> UploadPartAsync(UploadImportSessionPartCommand command, CancellationToken ct = default);

    Task<ImportUploadSessionStatusDto> GetStatusAsync(Guid sessionId, Guid? requestingUserId, CancellationToken ct = default);

    /// <summary>Idempotent — calling this again after a session has already completed returns the
    /// same result (same <c>ImportPackageId</c>) without re-assembling or re-inspecting anything.</summary>
    Task<ImportPackageManifestSummaryDto> CompleteAsync(CompleteImportUploadSessionCommand command, CancellationToken ct = default);

    Task AbortAsync(AbortImportUploadSessionCommand command, CancellationToken ct = default);
}

/// <summary>Thrown when a session-scoped action is attempted by a user other than the one who
/// created the session. Maps to HTTP 403 in the controller.</summary>
public sealed class ImportUploadSessionForbiddenException : Exception
{
    public ImportUploadSessionForbiddenException()
        : base("This upload session belongs to a different user.") { }
}
