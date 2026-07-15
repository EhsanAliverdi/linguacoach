using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase 4.2 — extracted from ImportPackageUploadService's private ToSummary so both the ZIP
/// upload flow and the inline (paste/multi-file) submission flow build the exact same
/// ImportPackageManifestSummaryDto shape from an ImportPackage + its ImportPackageManifest.
/// </summary>
internal static class ImportPackageManifestSummaryMapper {
    public static ImportPackageManifestSummaryDto ToSummary(ImportPackage package, ImportPackageManifest manifest) =>
        new(
            package.Id,
            package.Status,
            manifest.IsAccepted,
            manifest.RejectionReason,
            manifest.CompressedSizeBytes,
            manifest.ExpandedSizeBytes,
            manifest.EntryCount,
            manifest.FolderGroups,
            manifest.DistinctExtensions,
            manifest.DuplicateChecksumEntries.Count,
            manifest.UnsupportedEntries.Count,
            manifest.SuspiciousEntries.Count);
}
