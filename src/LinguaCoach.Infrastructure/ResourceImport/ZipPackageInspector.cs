using System.IO.Compression;
using LinguaCoach.Application.ResourceImport;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Phase 4 (2026-07-15), Part A — secure ZIP ingestion. Streams the central directory only
// (System.IO.Compression.ZipArchive requires a seekable stream in Read mode, which is exactly
// what we want: the archive must already be resting on durable storage — local disk or a
// storage-service temp download — never a live network upload stream). Every entry is bounds-
// checked against declared metadata *before* any bytes are decompressed, then the checksum read
// itself is capped at the entry's own declared length so a lying central-directory size cannot
// be used to smuggle a zip bomb past the pre-check. Per-entry validation itself lives in
// ZipEntrySafetyValidator (Phase 4.8), shared with extraction-time revalidation. ──

internal sealed class ZipPackageInspector : IZipPackageInspector
{
    private static readonly Dictionary<string, string> ExtensionToMimeType = new(StringComparer.OrdinalIgnoreCase)
    {
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".xml"] = "application/xml",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".m4a"] = "audio/mp4",
        [".ogg"] = "audio/ogg",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".mp4"] = "video/mp4",
        [".pdf"] = "application/pdf",
    };

    private readonly ImportPackageLimitsOptions _limits;

    public ZipPackageInspector(IOptions<ImportPackageLimitsOptions> limits)
    {
        _limits = limits.Value;
    }

    public Task<ImportPackageManifest> InspectAsync(Stream archiveStream, CancellationToken ct = default)
    {
        if (!archiveStream.CanSeek)
            throw new ImportPackageInspectionException("Archive stream must be seekable for inspection.");

        var compressedSizeBytes = archiveStream.Length;
        if (compressedSizeBytes > _limits.MaxCompressedSizeBytes)
        {
            return Task.FromResult(Rejected(
                $"Archive size {compressedSizeBytes:N0} bytes exceeds the configured limit of {_limits.MaxCompressedSizeBytes:N0} bytes.",
                compressedSizeBytes));
        }

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new ImportPackageInspectionException($"File is not a valid ZIP archive: {ex.Message}");
        }

        using (archive)
        {
            var realEntries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();

            if (realEntries.Count > _limits.MaxEntryCount)
            {
                return Task.FromResult(Rejected(
                    $"Archive contains {realEntries.Count:N0} entries, exceeding the configured limit of {_limits.MaxEntryCount:N0}.",
                    compressedSizeBytes));
            }

            var entries = new List<ImportPackageManifestEntry>();
            long expandedSizeBytes = 0;

            foreach (var entry in realEntries)
            {
                ct.ThrowIfCancellationRequested();

                expandedSizeBytes += entry.Length;
                if (expandedSizeBytes > _limits.MaxExpandedSizeBytes)
                {
                    return Task.FromResult(Rejected(
                        $"Archive's total expanded size exceeds the configured limit of {_limits.MaxExpandedSizeBytes:N0} bytes.",
                        compressedSizeBytes));
                }

                var validated = ZipEntrySafetyValidator.Validate(entry, _limits);

                var extension = Path.GetExtension(entry.Name);
                entries.Add(new ImportPackageManifestEntry(
                    validated.RelativePath,
                    entry.Name,
                    extension,
                    entry.CompressedLength,
                    entry.Length,
                    ExtensionToMimeType.GetValueOrDefault(extension),
                    validated.Checksum,
                    validated.IsSuspicious,
                    validated.Reason));
            }

            var suspiciousEntries = entries.Where(e => e.IsSuspicious).ToList();
            var unsupportedEntries = entries
                .Where(e => !e.IsSuspicious && e.DetectedMimeType is null)
                .ToList();
            var duplicateChecksumEntries = entries
                .Where(e => !e.IsSuspicious)
                .GroupBy(e => e.Checksum)
                .Where(g => g.Key.Length > 0 && g.Count() > 1)
                .SelectMany(g => g)
                .ToList();

            var folderGroups = entries
                .GroupBy(e => Path.GetDirectoryName(e.RelativePath)?.Replace('\\', '/') ?? string.Empty)
                .Select(g => new ImportPackageFolderGroup(
                    g.Key,
                    g.Count(),
                    g.Select(e => e.FileExtension).Distinct(StringComparer.OrdinalIgnoreCase).ToList()))
                .OrderBy(g => g.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var distinctExtensions = entries
                .Select(e => e.FileExtension)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Task.FromResult(new ImportPackageManifest(
                IsAccepted: true,
                RejectionReason: null,
                compressedSizeBytes,
                expandedSizeBytes,
                entries.Count,
                entries,
                folderGroups,
                distinctExtensions,
                duplicateChecksumEntries,
                unsupportedEntries,
                suspiciousEntries));
        }
    }

    private static ImportPackageManifest Rejected(string reason, long compressedSizeBytes) =>
        new(
            IsAccepted: false,
            RejectionReason: reason,
            compressedSizeBytes,
            ExpandedSizeBytes: 0,
            EntryCount: 0,
            Entries: Array.Empty<ImportPackageManifestEntry>(),
            FolderGroups: Array.Empty<ImportPackageFolderGroup>(),
            DistinctExtensions: Array.Empty<string>(),
            DuplicateChecksumEntries: Array.Empty<ImportPackageManifestEntry>(),
            UnsupportedEntries: Array.Empty<ImportPackageManifestEntry>(),
            SuspiciousEntries: Array.Empty<ImportPackageManifestEntry>());
}
