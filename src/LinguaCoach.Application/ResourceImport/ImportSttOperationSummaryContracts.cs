namespace LinguaCoach.Application.ResourceImport;

// ── Phase 4.4C — read-only visibility into the Phase 4.4 durable ImportSttOperation ledger for
// the admin plan page. Deliberately narrow: no raw provider credentials, no full transcript text
// (only a bounded, already-safe failure message), and every row is scoped to the exact package +
// plan requested — never a cross-package query. Not a general billing dashboard: this is the one
// ledgered billable operation type today (STT), surfaced exactly as it is persisted. ──

/// <summary>One STT operation's safe-to-display summary. <see cref="ResultReusable"/> is true once
/// the operation has <c>Succeeded</c> — from that point on, any future claim of this exact logical
/// operation (same package + asset + content checksum) reuses this result and does not call the
/// provider or accrue cost again (see <c>IImportSttOperationLedger</c>). It is not a per-attempt
/// "was this specific run a reuse" flag — the ledger does not persist that history, only the
/// current terminal/pending state — but it is the accurate, honest signal for "will a retry of
/// this operation cost money again."</summary>
public sealed record ImportSttOperationSummaryDto(
    Guid OperationId,
    string AssetFileName,
    string AssetRelativePath,
    string ProviderName,
    string? ModelName,
    string Status,
    int AttemptNumber,
    bool ResultReusable,
    decimal? CalculatedCost,
    string Currency,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? SafeErrorMessage);

public interface IImportSttOperationSummaryQuery
{
    /// <summary>Returns every STT operation ledgered against <paramref name="planId"/> within
    /// <paramref name="importPackageId"/>, oldest first. Returns an empty list (not null/404) when
    /// the package/plan exist but no STT operation has run yet. Returns null when the package or
    /// plan does not exist, or the plan does not belong to the package — the caller maps that to
    /// 404, matching the rest of this controller's not-found handling.</summary>
    Task<IReadOnlyList<ImportSttOperationSummaryDto>?> GetForPlanAsync(
        Guid importPackageId, Guid planId, CancellationToken ct = default);
}
