using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ResourceImport;

// ── Mandatory Import Execution Plan addendum (2026-07-15) — every ImportPackage, regardless of
// size, must produce a persisted, admin-approved Import Execution Plan before any material AI,
// STT, TTS, or background processing begins. The plan entity is `ImportProfile` (Phase 4 Part B
// named it as the mapping/extraction ruleset; this addendum extends the same row with the
// estimate/approval fields rather than introducing a parallel entity — see
// ImportProfileStatus's doc comment). Sample selection is fully automatic (deterministic
// clustering + a bounded AI review round) — there is no manual admin sample-picking step. ──

public sealed record ImportExecutionPlanDetectedGroup(
    string GroupKey,
    string Description,
    int FileCount,
    IReadOnlyList<string> SampleRelativePaths,
    ResourceCandidateType? ProposedResourceType,
    double Confidence);

public sealed record ImportExecutionPlanVolumeEstimate(
    int TotalFiles,
    IReadOnlyDictionary<string, int> FilesByExtension,
    int ExpectedCandidateCount,
    int ExpectedAudioFilesRequiringStt,
    double EstimatedAudioMinutesRequiringStt,
    int ExpectedTtsCandidates,
    int EstimatedTtsCharacters,
    int ExpectedImageAnalysisCount,
    int UnmatchedFileCount);

public sealed record ImportExecutionPlanTimeEstimate(
    string EstimatedDurationRangeDescription,
    double EstimatedMinMinutes,
    double EstimatedMaxMinutes,
    string Assumptions);

public sealed record ImportExecutionPlanCostBreakdownLine(string Category, decimal Amount);

public sealed record ImportExecutionPlanCostEstimate(
    decimal ExpectedCost,
    decimal MinCost,
    decimal MaxCost,
    string Currency,
    IReadOnlyList<ImportExecutionPlanCostBreakdownLine> Breakdown,
    IReadOnlyList<string> Assumptions,
    string ProviderModelAssumptions);

public sealed record ImportExecutionPlanDecision(string Topic, string Decision, string Reason);

/// <summary>
/// Phase 4.2 (Part F) — a real preview of the proposed column mapping for one structured
/// (CSV/JSON/JSONL) asset, built only for inline (non-ZIP) packages, since only there do
/// <c>ImportAsset</c> rows already exist at plan-generation time. <see cref="ProposedMapping"/> is
/// the exact confirmed-at-approval-time column-rename map that execution applies when creating
/// candidates from this asset (see <c>ImportPackageProcessingService</c>) — the one approved-plan
/// decision this phase makes execution actually read, so the unified submission page doesn't
/// regress the Phase K1 header-recognition fix.
/// </summary>
public sealed record ImportExecutionPlanStructuredMappingPreview(
    string AssetRelativePath,
    IReadOnlyList<string> DetectedColumns,
    IReadOnlyDictionary<string, string> ProposedMapping,
    IReadOnlyList<string> IgnoredColumns,
    int ExpectedRecordCount,
    IReadOnlyList<string> Warnings);

public sealed record ImportExecutionPlanEstimate(
    IReadOnlyList<ImportExecutionPlanDetectedGroup> DetectedGroups,
    IReadOnlyList<string> AmbiguousGroups,
    IReadOnlyList<string> UnsupportedContentNotes,
    ImportExecutionPlanVolumeEstimate Volume,
    ImportExecutionPlanTimeEstimate Time,
    ImportExecutionPlanCostEstimate Cost,
    IReadOnlyList<string> Risks,
    IReadOnlyList<ImportExecutionPlanDecision> ProposedDecisions,
    int SamplingRoundsUsed,
    double StructureConfidence,
    IReadOnlyList<ImportExecutionPlanStructuredMappingPreview>? StructuredMappingPreviews = null)
{
    public IReadOnlyList<ImportExecutionPlanStructuredMappingPreview> StructuredMappingPreviews { get; init; } =
        StructuredMappingPreviews ?? Array.Empty<ImportExecutionPlanStructuredMappingPreview>();
}

public sealed record ImportExecutionPlanDto(
    Guid PlanId,
    Guid ImportPackageId,
    int Version,
    ImportProfileStatus Status,
    ImportProcessingMode? ProcessingMode,
    string? ProcessingModeReason,
    ImportExecutionPlanEstimate Estimate,
    decimal? ApprovedCostCeiling,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    Guid? ApprovedByUserId,
    DateTimeOffset? RejectedAtUtc,
    string? RejectionReason,
    string? PauseReason,
    string? ChangeReason);

public sealed record GenerateImportExecutionPlanCommand(Guid ImportPackageId, string? ChangeReason = null);

public sealed record ApproveImportExecutionPlanCommand(
    Guid ImportPackageId,
    Guid PlanId,
    Guid? ApprovedByUserId,
    decimal ApprovedCostCeiling);

public sealed record RejectImportExecutionPlanCommand(
    Guid ImportPackageId,
    Guid PlanId,
    Guid? RejectedByUserId,
    string Reason);

public sealed record ApproveRevisedCostCeilingCommand(
    Guid ImportPackageId,
    Guid PlanId,
    decimal NewApprovedCostCeiling);

/// <summary>Thrown when a plan fails Part 9's pre-approval quality validation (e.g. STT required
/// but no STT provider configured, candidate estimate exceeds configured limits). Distinct from
/// <see cref="ResourceImportValidationException"/> so callers can surface it as a blocked-approval
/// state rather than a generic validation error.</summary>
public sealed class ImportExecutionPlanNotApprovableException : Exception
{
    public IReadOnlyList<string> BlockingReasons { get; }

    public ImportExecutionPlanNotApprovableException(IReadOnlyList<string> blockingReasons)
        : base("Import Execution Plan is not approvable: " + string.Join(" ", blockingReasons))
    {
        BlockingReasons = blockingReasons;
    }
}

public interface IImportExecutionPlanGenerationService
{
    /// <summary>Runs pre-flight-only work: deterministic clustering of the manifest, automatic
    /// representative sample selection, a bounded AI review round (capped by
    /// <c>ImportCostEstimationOptions.MaxSamplingRounds</c>), and cost/time estimation. Persists
    /// a new Draft → AwaitingApproval <c>ImportProfile</c> row. Never transcribes audio,
    /// generates TTS, or creates candidates.</summary>
    Task<ImportExecutionPlanDto> GenerateAsync(GenerateImportExecutionPlanCommand command, CancellationToken ct = default);
}

public interface IImportExecutionPlanApprovalService
{
    Task<ImportExecutionPlanDto> ApproveAsync(ApproveImportExecutionPlanCommand command, CancellationToken ct = default);
    Task<ImportExecutionPlanDto> RejectAsync(RejectImportExecutionPlanCommand command, CancellationToken ct = default);
    Task<ImportExecutionPlanDto> ApproveRevisedCostCeilingAsync(ApproveRevisedCostCeilingCommand command, CancellationToken ct = default);
    Task<ImportExecutionPlanDto?> GetCurrentPlanAsync(Guid importPackageId, CancellationToken ct = default);
}
