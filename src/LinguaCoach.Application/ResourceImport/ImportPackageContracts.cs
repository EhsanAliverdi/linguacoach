using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ResourceImport;

// ── Phase 4 (2026-07-15 large-scale AI import packages), Parts A+B — the package upload/
// inspection lifecycle: request a signed upload URL → client PUTs the archive directly to
// storage → confirm upload → stream-inspect it into a manifest → (later phases) processing-
// mode decision, sample selection, AI profiling, background processing. ──

public sealed record RequestImportPackageUploadCommand(
    Guid CefrResourceSourceId,
    string OriginalFileName,
    long DeclaredSizeBytes,
    Guid? CreatedByUserId = null,
    string? Notes = null);

public sealed record RequestImportPackageUploadResult(
    Guid ImportPackageId,
    string UploadUrl,
    DateTimeOffset UploadUrlExpiresAt,
    string StorageKey);

public sealed record ConfirmImportPackageUploadCommand(Guid ImportPackageId);

public sealed record ImportPackageManifestSummaryDto(
    Guid ImportPackageId,
    ImportPackageStatus Status,
    bool IsAccepted,
    string? RejectionReason,
    long CompressedSizeBytes,
    long ExpandedSizeBytes,
    int EntryCount,
    IReadOnlyList<ImportPackageFolderGroup> FolderGroups,
    IReadOnlyList<string> DistinctExtensions,
    int DuplicateChecksumEntryCount,
    int UnsupportedEntryCount,
    int SuspiciousEntryCount);

public interface IImportPackageUploadService
{
    /// <summary>Creates the <c>ImportPackage</c> row (Status=Uploaded is set once the upload is
    /// confirmed, not here) and returns a short-lived signed PUT URL. Rejects up front if
    /// <paramref name="command"/>'s declared size already exceeds
    /// <c>ImportPackageLimitsOptions.MaxCompressedSizeBytes</c> — no point minting a URL for an
    /// upload that can never be accepted.</summary>
    Task<RequestImportPackageUploadResult> RequestUploadAsync(
        RequestImportPackageUploadCommand command, CancellationToken ct = default);

    /// <summary>Called after the client reports the direct PUT finished. Verifies the object
    /// exists in storage, then streams it through <see cref="IZipPackageInspector"/> to build
    /// and persist the manifest, advancing <c>ImportPackage.Status</c> accordingly (rejected
    /// archives move to Failed with the rejection reason as the error summary; accepted archives
    /// move to InspectingPackage → AwaitingSample/Queued once Part C's mode decision runs).</summary>
    Task<ImportPackageManifestSummaryDto> ConfirmUploadAsync(
        ConfirmImportPackageUploadCommand command, CancellationToken ct = default);

    Task<ImportPackageManifestSummaryDto?> GetManifestSummaryAsync(Guid importPackageId, CancellationToken ct = default);
}
