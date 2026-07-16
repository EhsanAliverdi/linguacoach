using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Phase 4.4D — exactly one ImportAiEnrichmentOperation row exists per logical key, enforced by
// a unique database index, mutated in place across retries rather than accumulating a new row per
// attempt. Mirrors ImportSttOperationLedger's exact retry-safety/dedup guarantee and its documented
// remaining boundary (single active package-processing worker assumed). ──

internal sealed class ImportAiEnrichmentOperationLedger : IImportAiEnrichmentOperationLedger
{
    private readonly LinguaCoachDbContext _db;

    public ImportAiEnrichmentOperationLedger(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<ImportAiClaimResult> ClaimAsync(
        Guid importPackageId, Guid importProfileId, Guid resourceCandidateId, string logicalOperationKey,
        string operationType, string providerName, string promptVersion, string processingMode,
        CancellationToken ct = default)
    {
        var existing = await _db.ImportAiEnrichmentOperations
            .FirstOrDefaultAsync(o => o.LogicalOperationKey == logicalOperationKey, ct);

        if (existing is not null)
        {
            if (existing.Status == ImportAiOperationStatus.Succeeded)
                return new ImportAiClaimResult(ImportAiClaimOutcome.AlreadySucceeded, existing);

            if (existing.Status == ImportAiOperationStatus.Failed)
            {
                existing.BeginRetry(DateTimeOffset.UtcNow);
                await _db.SaveChangesAsync(ct);
            }
            // Pending: reuse the row as-is — see the interface's documented concurrency boundary.

            return new ImportAiClaimResult(ImportAiClaimOutcome.Claimed, existing);
        }

        var operation = new ImportAiEnrichmentOperation(
            importPackageId, importProfileId, resourceCandidateId, logicalOperationKey,
            operationType, providerName, promptVersion, processingMode, DateTimeOffset.UtcNow);
        _db.ImportAiEnrichmentOperations.Add(operation);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique-index race — another concurrent caller inserted the same logical key first.
            _db.Entry(operation).State = EntityState.Detached;
            var winner = await _db.ImportAiEnrichmentOperations.FirstAsync(o => o.LogicalOperationKey == logicalOperationKey, ct);
            return new ImportAiClaimResult(
                winner.Status == ImportAiOperationStatus.Succeeded ? ImportAiClaimOutcome.AlreadySucceeded : ImportAiClaimOutcome.Claimed,
                winner);
        }

        return new ImportAiClaimResult(ImportAiClaimOutcome.Claimed, operation);
    }

    /// <summary>Mutates the tracked entity only — does not save. The caller must save this in the
    /// same <c>SaveChangesAsync</c> as the package's <c>AccrueCost</c> update.</summary>
    public Task MarkSucceededAsync(
        ImportAiEnrichmentOperation operation, string resultReferenceJson, decimal calculatedCost, string currency,
        int inputTokens, int outputTokens, decimal inputPricePer1KTokensSnapshot, decimal outputPricePer1KTokensSnapshot,
        string? modelName, CancellationToken ct = default)
    {
        operation.MarkSucceeded(
            resultReferenceJson, calculatedCost, currency, inputTokens, outputTokens,
            inputPricePer1KTokensSnapshot, outputPricePer1KTokensSnapshot, modelName, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    /// <summary>See <see cref="MarkSucceededAsync"/>'s doc comment — also does not save.</summary>
    public async Task MarkFailedAsync(ImportAiEnrichmentOperation operation, string reason, CancellationToken ct = default)
    {
        operation.MarkFailed(reason, DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(ct);
    }
}
