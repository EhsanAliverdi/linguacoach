namespace LinguaCoach.Application.ResourceImport;

// ── Phase 4 (2026-07-15 large-scale AI import packages), Parts A+B — secure ZIP/archive ingestion
// and the resulting inspection manifest. IZipPackageInspector streams a ZIP's central directory
// (never extracting entries into memory) to produce an ImportPackageManifest: an inventory an
// admin (or the mode-decision/sample-selection services) can reason about before any AI call or
// full-package processing happens. Rejection is a first-class outcome, not an exception path for
// "normal" bad input — a hostile or malformed archive should read as a manifest whose
// IsAccepted is false with SuspiciousEntries/RejectionReason populated, not a thrown error. ──

public sealed record ImportPackageManifestEntry(
    string RelativePath,
    string FileName,
    string FileExtension,
    long CompressedSizeBytes,
    long UncompressedSizeBytes,
    string? DetectedMimeType,
    string Checksum,
    bool IsSuspicious,
    string? SuspiciousReason);

public sealed record ImportPackageFolderGroup(
    string FolderPath,
    int FileCount,
    IReadOnlyList<string> Extensions);

public sealed record ImportPackageManifest(
    bool IsAccepted,
    string? RejectionReason,
    long CompressedSizeBytes,
    long ExpandedSizeBytes,
    int EntryCount,
    IReadOnlyList<ImportPackageManifestEntry> Entries,
    IReadOnlyList<ImportPackageFolderGroup> FolderGroups,
    IReadOnlyList<string> DistinctExtensions,
    IReadOnlyList<ImportPackageManifestEntry> DuplicateChecksumEntries,
    IReadOnlyList<ImportPackageManifestEntry> UnsupportedEntries,
    IReadOnlyList<ImportPackageManifestEntry> SuspiciousEntries);

/// <summary>Thrown only for archive-format-level failures the caller cannot reasonably continue
/// past (cannot open as a ZIP at all). Anything the manifest itself can describe — a path
/// traversal entry, an oversized entry, too many entries — is represented in the returned
/// manifest as a rejection, not this exception.</summary>
public sealed class ImportPackageInspectionException : Exception
{
    public ImportPackageInspectionException(string message) : base(message) { }
}

public interface IZipPackageInspector
{
    /// <summary>Streams and validates a ZIP archive's central directory against the configured
    /// <c>ImportPackageLimitsOptions</c> (size, entry count, compression ratio, nesting, path
    /// traversal, encryption). Never extracts entry content into memory beyond the bounded read
    /// needed to compute each entry's checksum.</summary>
    Task<ImportPackageManifest> InspectAsync(Stream archiveStream, CancellationToken ct = default);
}
