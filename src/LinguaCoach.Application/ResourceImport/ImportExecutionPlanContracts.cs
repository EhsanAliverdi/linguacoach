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

// ── Phase 4.3 (2026-07-16) — approved-plan-driven execution. Pre-4.3, ProfileJson was written
// once at plan generation (as a plain List<ImportExecutionPlanDetectedGroup>) and never read by
// execution — ImportPackageProcessingService independently re-derived file routing, resource-type
// classification, and (for ZIP packages) column mapping from scratch. The types below are what
// ProfileJson now persists and what execution reads back through IApprovedImportProfileResolver —
// a frozen, typed instruction set keyed to the exact ApprovedImportProfileId, never "the latest
// plan" and never re-inferred mid-execution. ──

/// <summary>Shared folder-grouping convention used identically by plan generation (clustering) and
/// Phase 4.3 execution (instruction lookup), so an asset's group at execution time is always the
/// same group the admin approved a route/mapping for. "(root)" covers every pasted/loose-file
/// submission (which has no folder) and every top-level ZIP entry.</summary>
public static class ImportExecutionGroupKey
{
    public const string Root = "(root)";

    public static string ForRelativePath(string relativePath)
    {
        var dir = System.IO.Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? string.Empty;
        return string.IsNullOrEmpty(dir) ? Root : dir;
    }
}

/// <summary>
/// Phase 4.3 — one folder-group's frozen, approved execution instruction; this is exactly what a
/// list of these, serialized, is now persisted as <see cref="Domain.Entities.ImportProfile.ProfileJson"/>.
/// <see cref="ResourceType"/>, when set, is a forced route — execution must not override it with
/// its own field-name heuristics. <see cref="FieldMappings"/> is a source-column -> target-field
/// name map applied to every structured (CSV/JSON/JSONL) file in this group before parsing.
/// <see cref="Included"/> false means every file in this group is skipped entirely — no candidate,
/// no raw record.
/// </summary>
public sealed record ImportExecutionGroupInstruction(
    string GroupKey,
    bool Included,
    ResourceCandidateType? ResourceType,
    IReadOnlyDictionary<string, string> FieldMappings,
    IReadOnlyList<string> SampleRelativePaths)
{
    public IReadOnlyDictionary<string, string> FieldMappings { get; init; } =
        FieldMappings ?? new Dictionary<string, string>();
    public IReadOnlyList<string> SampleRelativePaths { get; init; } =
        SampleRelativePaths ?? Array.Empty<string>();
}

/// <summary>
/// Phase 4.3 — the resolved, validated execution contract for one approved <c>ImportProfile</c>.
/// Built once per package-processing pass by <see cref="IApprovedImportProfileResolver"/>; every
/// downstream routing/mapping decision must come from <see cref="ResolveForRelativePath"/>, not
/// from independently re-inferring file type/extension/folder heuristics.
/// </summary>
public sealed record ApprovedImportExecutionProfile(
    Guid ImportProfileId,
    Guid ImportPackageId,
    int Version,
    IReadOnlyList<ImportExecutionGroupInstruction> GroupInstructions)
{
    /// <summary>Resolves the instruction governing one package-relative asset path. Returns null
    /// when no approved instruction covers that asset's folder — the caller must then fail
    /// deterministically rather than guessing a default route.</summary>
    public ImportExecutionGroupInstruction? ResolveForRelativePath(string relativePath)
    {
        var key = ImportExecutionGroupKey.ForRelativePath(relativePath);
        return GroupInstructions.FirstOrDefault(g => string.Equals(g.GroupKey, key, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Thrown by <see cref="IApprovedImportProfileResolver"/> for every way an approved plan
/// cannot be safely used to drive execution: no approved profile, referenced profile missing, the
/// profile belongs to another package, it isn't in an executable state, or its ProfileJson is
/// malformed/missing a mapping/routes to an unsupported type. Deterministic and specific — the
/// message is safe to surface to an admin via the package's failure state.</summary>
public sealed class ApprovedImportProfileResolutionException : Exception
{
    public ApprovedImportProfileResolutionException(string message) : base(message) { }
}

/// <summary>Centralises ProfileJson deserialization/validation so no controller, job, candidate
/// service, or processing service parses it independently (Phase 4.3, Required Outcome 3).</summary>
public interface IApprovedImportProfileResolver
{
    /// <summary>Loads the package's exact <c>ApprovedImportProfileId</c> plan (never "the latest"
    /// plan), validates package ownership and approval state, deserializes and validates
    /// <c>ProfileJson</c>, and returns the typed, frozen execution instructions. Throws
    /// <see cref="ApprovedImportProfileResolutionException"/> for every validation failure.</summary>
    Task<ApprovedImportExecutionProfile> ResolveAsync(Guid importPackageId, CancellationToken ct = default);
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
