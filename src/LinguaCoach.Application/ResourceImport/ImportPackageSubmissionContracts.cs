namespace LinguaCoach.Application.ResourceImport;

// ── Phase 4.2 (2026-07-15 mandatory Import Execution Plan gate for every import) — the one
// canonical Import submission entry point. Pasted text, one file, or several files all become a
// single ImportPackage with ImportAsset rows and a synthetic (non-archive) manifest, so the exact
// same plan-generation/approval/processing pipeline built in Phase 4 for ZIP packages now governs
// every import, not just large ones. A ZIP file continues to use the existing presigned-upload
// flow (IImportPackageUploadService) unchanged — this service is for everything else. ──

public sealed record ImportPackageSubmissionFile(string FileName, Stream Content, long DeclaredLength);

public sealed record SubmitImportPackageCommand(
    Guid CefrResourceSourceId,
    string? PastedText,
    IReadOnlyList<ImportPackageSubmissionFile> Files,
    string? Notes,
    Guid? CreatedByUserId);

public interface IImportPackageSubmissionService
{
    /// <summary>Creates an ImportPackage from pasted text and/or one or more non-archive files,
    /// stores each as an ImportAsset, and synthesizes an accepted manifest — the same shape a ZIP
    /// upload produces via IZipPackageInspector — so plan generation (Part 4) works unmodified.
    /// Throws ResourceImportValidationException if neither PastedText nor Files carries any
    /// content, or if the source does not exist.</summary>
    Task<ImportPackageManifestSummaryDto> SubmitAsync(SubmitImportPackageCommand command, CancellationToken ct = default);
}
