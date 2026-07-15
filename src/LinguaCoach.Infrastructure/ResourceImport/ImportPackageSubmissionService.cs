using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Phase 4.2 (2026-07-15 mandatory Import Execution Plan gate for every import) — the one
// canonical non-ZIP submission path. Pasted text becomes one line-based JSONL asset (see
// PastedContentConverter); each uploaded file becomes its own asset. Both are stored through the
// same IFileStorageService abstraction the ZIP pipeline already uses, and a synthetic, always-
// accepted ImportPackageManifest is built directly from the created assets — no archive, no
// IZipPackageInspector involved — so ImportExecutionPlanGenerationService and
// ImportPackageProcessingService work completely unmodified downstream (both already operate
// purely off ImportPackage.ManifestJson / ImportAsset rows). This is the ONLY path that may create
// an ImportPackage from pasted text or a loose file — see AdminImportPackageController's
// doc comment for the plan-gate invariant this closes. ──

internal sealed class ImportPackageSubmissionService : IImportPackageSubmissionService
{
    private const string StorageCategory = "import-package-assets";
    private const string PastedContentFileName = "pasted-content.jsonl";

    private readonly LinguaCoachDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ImportPackageLimitsOptions _limits;

    public ImportPackageSubmissionService(
        LinguaCoachDbContext db,
        IFileStorageService storage,
        IOptions<ImportPackageLimitsOptions> limits)
    {
        _db = db;
        _storage = storage;
        _limits = limits.Value;
    }

    public async Task<ImportPackageManifestSummaryDto> SubmitAsync(SubmitImportPackageCommand command, CancellationToken ct = default)
    {
        var hasPastedText = !string.IsNullOrWhiteSpace(command.PastedText);
        if (!hasPastedText && command.Files.Count == 0)
            throw new ResourceImportValidationException("Provide pasted content, at least one file, or both.");

        var sourceExists = await _db.CefrResourceSources.AnyAsync(s => s.Id == command.CefrResourceSourceId, ct);
        if (!sourceExists)
            throw new ResourceImportValidationException("The selected resource source does not exist.");

        foreach (var file in command.Files)
        {
            if (file.DeclaredLength > _limits.MaxIndividualFileSizeBytes)
            {
                throw new ResourceImportValidationException(
                    $"File '{file.FileName}' ({file.DeclaredLength:N0} bytes) exceeds the configured per-file limit of " +
                    $"{_limits.MaxIndividualFileSizeBytes:N0} bytes.");
            }
        }

        var displayName = BuildDisplayName(hasPastedText, command.Files);
        var package = new ImportPackage(
            command.CefrResourceSourceId,
            displayName,
            DateTimeOffset.UtcNow,
            command.CreatedByUserId,
            archiveStorageKey: null,
            notes: command.Notes);
        _db.ImportPackages.Add(package);
        await _db.SaveChangesAsync(ct);

        var entries = new List<ImportPackageManifestEntry>();
        var utcNow = DateTimeOffset.UtcNow;

        if (hasPastedText)
        {
            var jsonl = PastedContentConverter.ToJsonLines(command.PastedText!);
            if (string.IsNullOrWhiteSpace(jsonl))
                throw new ResourceImportValidationException("Pasted content had no non-empty lines.");

            var bytes = Encoding.UTF8.GetBytes(jsonl);
            entries.Add(await StoreAssetAsync(
                package.Id, PastedContentFileName, bytes, utcNow, ImportAssetRole.PrimaryContent, ct));
        }

        foreach (var file in command.Files)
        {
            using var buffer = new MemoryStream();
            await file.Content.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            if (bytes.Length == 0) continue;

            entries.Add(await StoreAssetAsync(package.Id, file.FileName, bytes, utcNow, ImportAssetRole.Unknown, ct));
        }

        if (entries.Count == 0)
            throw new ResourceImportValidationException("No usable content was found in the submission.");

        var totalBytes = entries.Sum(e => e.UncompressedSizeBytes);
        var distinctExtensions = entries.Select(e => e.FileExtension).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var folderGroup = new ImportPackageFolderGroup(FolderPath: string.Empty, FileCount: entries.Count, Extensions: distinctExtensions);
        var unsupported = entries.Where(e => e.DetectedMimeType == "application/octet-stream").ToList();

        var manifest = new ImportPackageManifest(
            IsAccepted: true,
            RejectionReason: null,
            CompressedSizeBytes: totalBytes,
            ExpandedSizeBytes: totalBytes,
            EntryCount: entries.Count,
            Entries: entries,
            FolderGroups: new List<ImportPackageFolderGroup> { folderGroup },
            DistinctExtensions: distinctExtensions,
            DuplicateChecksumEntries: Array.Empty<ImportPackageManifestEntry>(),
            UnsupportedEntries: unsupported,
            SuspiciousEntries: Array.Empty<ImportPackageManifestEntry>());

        package.SetManifest(JsonSerializer.Serialize(manifest), manifest.EntryCount);
        await _db.SaveChangesAsync(ct);

        return ImportPackageManifestSummaryMapper.ToSummary(package, manifest);
    }

    private async Task<ImportPackageManifestEntry> StoreAssetAsync(
        Guid packageId, string fileName, byte[] bytes, DateTimeOffset utcNow, ImportAssetRole role, CancellationToken ct)
    {
        var extension = Path.GetExtension(fileName);
        var (mimeType, mediaType) = ImportAssetClassification.Classify(extension);
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var storageKey = _storage.GenerateKey(packageId.ToString(), StorageCategory, extension);

        using (var contentStream = new MemoryStream(bytes))
        {
            await _storage.SaveAsync(storageKey, contentStream, mimeType, ct);
        }

        var asset = new ImportAsset(
            packageId, fileName, fileName, storageKey, mimeType, mediaType, extension,
            bytes.Length, checksum, utcNow, compressedSizeBytes: bytes.Length, role: role);
        asset.MarkInspected();
        _db.ImportAssets.Add(asset);

        return new ImportPackageManifestEntry(
            RelativePath: fileName, FileName: fileName, FileExtension: extension,
            CompressedSizeBytes: bytes.Length, UncompressedSizeBytes: bytes.Length,
            DetectedMimeType: mimeType, Checksum: checksum, IsSuspicious: false, SuspiciousReason: null);
    }

    private static string BuildDisplayName(bool hasPastedText, IReadOnlyList<ImportPackageSubmissionFile> files)
    {
        var parts = new List<string>();
        if (hasPastedText) parts.Add("Pasted content");
        if (files.Count == 1) parts.Add(files[0].FileName);
        else if (files.Count > 1) parts.Add($"{files.Count} files");
        return parts.Count > 0 ? string.Join(" + ", parts) : "Import submission";
    }
}
