namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Phase 4 (2026-07-15 large-scale AI import packages) — lifecycle of an <see cref="Entities.ImportPackage"/>.
/// Distinct from <see cref="ResourceImportRunStatus"/> (which still governs the simple single-file
/// CSV/JSON/JSONL/paste pipeline, unchanged): a package can fan out into multiple
/// <see cref="Entities.ResourceImportRun"/>s (one per detected schema/file group), so its own
/// status tracks the package-level pipeline stages, not any one run's.
/// </summary>
public enum ImportPackageStatus
{
    Uploading = 0,
    Uploaded = 1,
    InspectingPackage = 2,
    AwaitingSample = 3,
    AnalysingSample = 4,
    AwaitingMappingApproval = 5,
    Queued = 6,
    Extracting = 7,
    Mapping = 8,
    Transcribing = 9,
    Enriching = 10,
    CreatingCandidates = 11,
    Validating = 12,
    ReadyForReview = 13,
    CompletedWithWarnings = 14,
    Completed = 15,
    Failed = 16,
    Cancelled = 17
}
