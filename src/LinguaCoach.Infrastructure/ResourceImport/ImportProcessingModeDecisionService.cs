using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Phase 4 (2026-07-15), Part C — see ImportProcessingModeContracts.cs. Rules, in order:
//
//   1. Oversized packages (by expanded size, entry count, or a size-proxy record-count estimate)
//      must always go SampleDriven — a package too large to inspect in full has no business
//      going through an in-request AI call or single-pass deterministic parse.
//   2. Small packages containing only recognized structured-data files (csv/json), no
//      suspicious/unsupported entries, and a single folder-shape (one schema) go Direct —
//      the existing deterministic column-mapping pipeline (Phase K1) already handles this.
//   3. Everything else within the size envelope — mixed media, unknown schema, multiple folder
//      groups needing per-group mapping — goes FullAiAssisted.
//
// This mirrors "do not accept work the infrastructure cannot safely complete": SampleDriven
// exists precisely so a package that's too large for #2/#3 still gets processed, just via a
// smaller representative slice first. ──

internal sealed class ImportProcessingModeDecisionService : IImportProcessingModeDecisionService
{
    private static readonly HashSet<string> StructuredDataExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".csv", ".json" };

    private readonly ImportPackageLimitsOptions _limits;

    public ImportProcessingModeDecisionService(IOptions<ImportPackageLimitsOptions> limits)
    {
        _limits = limits.Value;
    }

    public ImportProcessingModeDecision Decide(ImportPackageManifest manifest)
    {
        if (manifest.ExpandedSizeBytes > _limits.FullAiAnalysisMaxExpandedSizeBytes)
        {
            return new ImportProcessingModeDecision(
                ImportProcessingMode.SampleDriven,
                $"Package's expanded size ({manifest.ExpandedSizeBytes:N0} bytes) exceeds the " +
                $"{_limits.FullAiAnalysisMaxExpandedSizeBytes:N0}-byte threshold for direct/full-AI " +
                "analysis; a representative sample will be analyzed first.");
        }

        if (manifest.EntryCount > _limits.FullAiAnalysisMaxFileCount)
        {
            return new ImportProcessingModeDecision(
                ImportProcessingMode.SampleDriven,
                $"Package contains {manifest.EntryCount:N0} files, exceeding the " +
                $"{_limits.FullAiAnalysisMaxFileCount:N0}-file threshold for direct/full-AI analysis; " +
                "a representative sample will be analyzed first.");
        }

        // No file content has been parsed yet at manifest time (Part B is structure-only), so the
        // record count is estimated from entry count alone — an explicit, labeled proxy, never
        // presented to the admin as a precise count.
        var estimatedRecordCount = manifest.EntryCount;
        if (estimatedRecordCount > _limits.FullAiAnalysisMaxEstimatedRecordCount)
        {
            return new ImportProcessingModeDecision(
                ImportProcessingMode.SampleDriven,
                $"Estimated record count ({estimatedRecordCount:N0}, based on file count) exceeds the " +
                $"{_limits.FullAiAnalysisMaxEstimatedRecordCount:N0}-record threshold for direct/full-AI " +
                "analysis; a representative sample will be analyzed first.");
        }

        var hasOnlyStructuredData = manifest.Entries.Count > 0 &&
            manifest.Entries.All(e => StructuredDataExtensions.Contains(e.FileExtension));
        var hasNoIssues = manifest.SuspiciousEntries.Count == 0 && manifest.UnsupportedEntries.Count == 0;
        var hasSingleSchemaShape = manifest.FolderGroups.Count <= 1;

        if (hasOnlyStructuredData && hasNoIssues && hasSingleSchemaShape)
        {
            return new ImportProcessingModeDecision(
                ImportProcessingMode.Direct,
                "All files are recognized structured data (CSV/JSON) in a single folder shape with " +
                "no suspicious or unsupported entries — the deterministic column-mapping pipeline can " +
                "process this package directly, without an AI structuring pass.");
        }

        return new ImportProcessingModeDecision(
            ImportProcessingMode.FullAiAssisted,
            "Package is within size limits for full-AI analysis but contains mixed media, multiple " +
            "folder shapes, or entries the deterministic pipeline cannot map on its own — every file " +
            "will be sent through AI-assisted structuring.");
    }
}
