using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Phase 4.4 (Workstream B4/B11) — exactly one ImportSttOperation row exists per logical key,
// enforced by a unique database index (see ImportSttOperationConfiguration), mutated in place
// across retries rather than accumulating a new row per attempt. See that entity's and
// IImportSttOperationLedger's doc comments for the exact retry-safety/dedup guarantee and its
// documented remaining boundary (single active package-processing worker assumed — Quartz
// clustering remains deferred). ──

internal sealed class ImportSttOperationLedger : IImportSttOperationLedger
{
    private readonly LinguaCoachDbContext _db;

    public ImportSttOperationLedger(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<ImportSttClaimResult> ClaimAsync(
        Guid importPackageId, Guid importProfileId, Guid importAssetId, string logicalOperationKey,
        string providerName, decimal assumedMinutes, CancellationToken ct = default)
    {
        var existing = await _db.ImportSttOperations
            .FirstOrDefaultAsync(o => o.LogicalOperationKey == logicalOperationKey, ct);

        if (existing is not null)
        {
            if (existing.Status == ImportSttOperationStatus.Succeeded)
                return new ImportSttClaimResult(ImportSttClaimOutcome.AlreadySucceeded, existing);

            if (existing.Status == ImportSttOperationStatus.Failed)
            {
                existing.BeginRetry(DateTimeOffset.UtcNow);
                await _db.SaveChangesAsync(ct);
            }
            // Pending: reuse the row as-is — see the interface's documented concurrency boundary
            // for why a dangling Pending row (from a prior crashed pass) is safe to re-enter here
            // rather than treated as a permanent block.

            return new ImportSttClaimResult(ImportSttClaimOutcome.Claimed, existing);
        }

        var operation = new ImportSttOperation(
            importPackageId, importProfileId, importAssetId, logicalOperationKey,
            providerName, assumedMinutes, DateTimeOffset.UtcNow);
        _db.ImportSttOperations.Add(operation);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique-index race — another concurrent caller inserted the same logical key first.
            // Detach our unsaved instance and defer to whatever the winner's row now says, rather
            // than risk two workers both calling the provider for the same content.
            _db.Entry(operation).State = EntityState.Detached;
            var winner = await _db.ImportSttOperations.FirstAsync(o => o.LogicalOperationKey == logicalOperationKey, ct);
            return new ImportSttClaimResult(
                winner.Status == ImportSttOperationStatus.Succeeded ? ImportSttClaimOutcome.AlreadySucceeded : ImportSttClaimOutcome.Claimed,
                winner);
        }

        return new ImportSttClaimResult(ImportSttClaimOutcome.Claimed, operation);
    }

    /// <summary>Mutates the tracked entity only — does not save. The caller is expected to save
    /// this in the same <c>SaveChangesAsync</c> as the package's <c>AccrueCost</c> update, so the
    /// ledger row and the durable running total can never drift apart from a crash between two
    /// separate saves.</summary>
    public Task MarkSucceededAsync(
        ImportSttOperation operation, string transcriptText, decimal calculatedCost, string currency,
        decimal pricePerMinuteSnapshot, string? modelName, CancellationToken ct = default)
    {
        operation.MarkSucceeded(transcriptText, calculatedCost, currency, pricePerMinuteSnapshot, modelName, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    /// <summary>See <see cref="MarkSucceededAsync"/>'s doc comment — also does not save.</summary>
    public async Task MarkFailedAsync(ImportSttOperation operation, string reason, CancellationToken ct = default)
    {
        operation.MarkFailed(reason, DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(ct);
    }
}
