using System.Security.Cryptography;
using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase 4.7 (2026-07-17 reliable large uploads) — the single shared "inspect the persisted
/// archive and turn it into a manifest" step, extracted so both the legacy single-shot
/// presigned-PUT flow (<c>ImportPackageUploadService.ConfirmUploadAsync</c>) and the new
/// resumable chunked-upload flow (<c>ImportUploadSessionService.CompleteAsync</c>) run the exact
/// same code — this is the one place <see cref="IZipPackageInspector"/> is invoked, so every ZIP
/// safety check it enforces (entry count, expanded/compressed size, per-entry ratio, path
/// traversal, nested-archive rejection, duplicate detection) always runs regardless of which
/// upload path produced the archive.
///
/// Never buffers the whole archive into a <see cref="MemoryStream"/> itself — it only seeks on
/// whatever stream <c>IFileStorageService.ReadAsync</c> returns. As of this phase,
/// <c>MinioFileStorageService.ReadAsync</c> returns a temp-file-backed <see cref="FileStream"/>
/// (seekable, not fully materialized in memory) rather than a full in-memory copy.
/// </summary>
internal static class ImportPackageInspectionRunner
{
    public static async Task<ImportPackageManifestSummaryDto> InspectAndPersistAsync(
        ImportPackage package,
        LinguaCoachDbContext db,
        IFileStorageService storage,
        IZipPackageInspector inspector,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(package.ArchiveStorageKey))
            throw new ResourceImportValidationException("Import package has no associated storage key.");

        package.MoveToStatus(ImportPackageStatus.InspectingPackage);
        await db.SaveChangesAsync(ct);

        await using var archiveStream = await storage.ReadAsync(package.ArchiveStorageKey, ct);
        // ZipArchive (Read mode) requires a seekable stream. Every current IFileStorageService
        // implementation now returns a seekable stream for ReadAsync (Local: FileStream; MinIO:
        // temp-file-backed FileStream; Fake: MemoryStream over an in-memory byte[]) — the
        // defensive copy-to-MemoryStream fallback below only fires for a hypothetical future
        // non-seekable implementation, and is intentionally not the common path.
        Stream seekableStream = archiveStream;
        MemoryStream? bufferedCopy = null;
        if (!archiveStream.CanSeek)
        {
            bufferedCopy = new MemoryStream();
            await archiveStream.CopyToAsync(bufferedCopy, ct);
            bufferedCopy.Position = 0;
            seekableStream = bufferedCopy;
        }

        ImportPackageManifest manifest;
        try
        {
            // Phase 4.8 — the legacy single-shot upload path (ImportPackageUploadService) never
            // recorded a whole-archive checksum; the Phase 4.7 chunked-upload session path already
            // has one (computed while assembling the parts) set on the package before this runs.
            // Recording it here, regardless of path, is what makes extraction-time checksum
            // revalidation (ImportPackageProcessingService.ExtractAssetsAsync) meaningful for every
            // ZIP-based package, not just chunked-upload ones.
            if (string.IsNullOrEmpty(package.ArchiveChecksum))
            {
                var checksum = await ComputeChecksumAsync(seekableStream, ct);
                package.SetArchiveChecksum(checksum);
                seekableStream.Position = 0;
            }

            manifest = await inspector.InspectAsync(seekableStream, ct);
        }
        finally
        {
            bufferedCopy?.Dispose();
        }

        var manifestJson = JsonSerializer.Serialize(manifest);

        if (!manifest.IsAccepted)
        {
            package.MarkFailed(manifest.RejectionReason ?? "Archive was rejected during inspection.", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
            return ImportPackageManifestSummaryMapper.ToSummary(package, manifest);
        }

        package.SetManifest(manifestJson, manifest.EntryCount);
        await db.SaveChangesAsync(ct);

        return ImportPackageManifestSummaryMapper.ToSummary(package, manifest);
    }

    private static async Task<string> ComputeChecksumAsync(Stream stream, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
    }
}
