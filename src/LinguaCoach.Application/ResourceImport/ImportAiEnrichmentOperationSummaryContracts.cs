namespace LinguaCoach.Application.ResourceImport;

// ── Phase 4.4D — read-only visibility into the durable ImportAiEnrichmentOperation ledger for the
// admin plan page, mirroring ImportSttOperationSummaryContracts.cs exactly. No raw provider
// credentials, no full AI response body (only the bounded ResultReferenceJson's existence is
// implied by ResultReusable, never returned itself), and every row is scoped to the exact package
// + plan requested. Not a general billing dashboard. ──

/// <summary>One AI enrichment operation's safe-to-display summary. <see cref="ResultReusable"/> is
/// true once the operation has <c>Succeeded</c> — see <c>ImportSttOperationSummaryDto</c>'s doc
/// comment for the exact same caveat applied here (a derived-from-current-state signal, not a
/// persisted per-attempt reuse history).</summary>
public sealed record ImportAiEnrichmentOperationSummaryDto(
    Guid OperationId,
    Guid ResourceCandidateId,
    string SourceLabel,
    string OperationType,
    string ProviderName,
    string? ModelName,
    string Status,
    int AttemptNumber,
    bool ResultReusable,
    int? InputTokens,
    int? OutputTokens,
    decimal? CalculatedCost,
    string Currency,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? SafeErrorMessage);

public interface IImportAiEnrichmentOperationSummaryQuery
{
    /// <summary>Returns every AI enrichment operation ledgered against <paramref name="planId"/>
    /// within <paramref name="importPackageId"/>, oldest first. Returns an empty list (not
    /// null/404) when the package/plan exist but no AI operation has run yet. Returns null when
    /// the package or plan does not exist, or the plan does not belong to the package.</summary>
    Task<IReadOnlyList<ImportAiEnrichmentOperationSummaryDto>?> GetForPlanAsync(
        Guid importPackageId, Guid planId, CancellationToken ct = default);
}
