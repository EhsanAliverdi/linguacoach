using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Phase 4 (2026-07-15), Parts A+B — request-upload-URL → confirm-upload → inspect-manifest
// lifecycle for an ImportPackage. Reuses IFileStorageService (presigned PUT, added this phase)
// rather than accepting the archive through an API action, so large archives never hit Kestrel's
// request-body limits. ──

internal sealed class ImportPackageUploadService : IImportPackageUploadService
{
    private const string StorageCategory = "import-packages";
    private static readonly TimeSpan UploadUrlExpiry = TimeSpan.FromHours(1);

    private readonly LinguaCoachDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IZipPackageInspector _inspector;
    private readonly ImportPackageLimitsOptions _limits;

    public ImportPackageUploadService(
        LinguaCoachDbContext db,
        IFileStorageService storage,
        IZipPackageInspector inspector,
        IOptions<ImportPackageLimitsOptions> limits)
    {
        _db = db;
        _storage = storage;
        _inspector = inspector;
        _limits = limits.Value;
    }

    public async Task<RequestImportPackageUploadResult> RequestUploadAsync(
        RequestImportPackageUploadCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.OriginalFileName))
            throw new ResourceImportValidationException("A file name is required.");
        if (command.DeclaredSizeBytes <= 0)
            throw new ResourceImportValidationException("Declared file size must be greater than zero.");
        if (command.DeclaredSizeBytes > _limits.MaxCompressedSizeBytes)
        {
            throw new ResourceImportValidationException(
                $"File size {command.DeclaredSizeBytes:N0} bytes exceeds the configured limit of " +
                $"{_limits.MaxCompressedSizeBytes:N0} bytes. Split the package into smaller archives.");
        }

        var sourceExists = await _db.CefrResourceSources.AnyAsync(s => s.Id == command.CefrResourceSourceId, ct);
        if (!sourceExists)
            throw new ResourceImportValidationException("The selected resource source does not exist.");

        var extension = Path.GetExtension(command.OriginalFileName);
        var storageKey = _storage.GenerateKey(command.CefrResourceSourceId.ToString(), StorageCategory, extension);

        var package = new ImportPackage(
            command.CefrResourceSourceId,
            command.OriginalFileName,
            DateTimeOffset.UtcNow,
            command.CreatedByUserId,
            archiveStorageKey: storageKey,
            compressedSizeBytes: command.DeclaredSizeBytes,
            notes: command.Notes);
        package.MoveToStatus(ImportPackageStatus.Uploading);

        _db.ImportPackages.Add(package);
        await _db.SaveChangesAsync(ct);

        var uploadUrl = await _storage.GenerateUploadUrlAsync(
            storageKey, UploadUrlExpiry, "application/zip", ct);

        return new RequestImportPackageUploadResult(
            package.Id, uploadUrl.Url, uploadUrl.ExpiresAt, storageKey);
    }

    public async Task<ImportPackageManifestSummaryDto> ConfirmUploadAsync(
        ConfirmImportPackageUploadCommand command, CancellationToken ct = default)
    {
        var package = await _db.ImportPackages.FirstOrDefaultAsync(p => p.Id == command.ImportPackageId, ct)
            ?? throw new ResourceImportValidationException("Import package not found.");

        if (string.IsNullOrEmpty(package.ArchiveStorageKey))
            throw new ResourceImportValidationException("Import package has no associated storage key.");

        var exists = await _storage.ExistsAsync(package.ArchiveStorageKey, ct);
        if (!exists)
        {
            throw new ResourceImportValidationException(
                "Upload was not found in storage. Confirm the upload finished successfully before retrying.");
        }

        package.MoveToStatus(ImportPackageStatus.InspectingPackage);
        await _db.SaveChangesAsync(ct);

        await using var archiveStream = await _storage.ReadAsync(package.ArchiveStorageKey, ct);
        // ZipArchive (Read mode) requires a seekable stream — copy to a seekable buffer only if
        // the storage-layer stream itself isn't seekable (MinIO reads are already fully-buffered
        // MemoryStreams; Local reads are seekable FileStreams).
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
            manifest = await _inspector.InspectAsync(seekableStream, ct);
        }
        finally
        {
            bufferedCopy?.Dispose();
        }

        var manifestJson = JsonSerializer.Serialize(manifest);

        if (!manifest.IsAccepted)
        {
            package.MarkFailed(manifest.RejectionReason ?? "Archive was rejected during inspection.", DateTimeOffset.UtcNow);
            await _db.SaveChangesAsync(ct);
            return ToSummary(package, manifest);
        }

        package.SetManifest(manifestJson, manifest.EntryCount);
        await _db.SaveChangesAsync(ct);

        return ToSummary(package, manifest);
    }

    public async Task<ImportPackageManifestSummaryDto?> GetManifestSummaryAsync(Guid importPackageId, CancellationToken ct = default)
    {
        var package = await _db.ImportPackages.FirstOrDefaultAsync(p => p.Id == importPackageId, ct);
        if (package is null)
            return null;

        if (string.IsNullOrEmpty(package.ManifestJson))
        {
            return new ImportPackageManifestSummaryDto(
                package.Id, package.Status, IsAccepted: package.Status != ImportPackageStatus.Failed,
                package.ErrorSummary, package.CompressedSizeBytes ?? 0, 0, 0,
                Array.Empty<ImportPackageFolderGroup>(), Array.Empty<string>(), 0, 0, 0);
        }

        var manifest = JsonSerializer.Deserialize<ImportPackageManifest>(package.ManifestJson)!;
        return ToSummary(package, manifest);
    }

    private static ImportPackageManifestSummaryDto ToSummary(ImportPackage package, ImportPackageManifest manifest) =>
        ImportPackageManifestSummaryMapper.ToSummary(package, manifest);
}
