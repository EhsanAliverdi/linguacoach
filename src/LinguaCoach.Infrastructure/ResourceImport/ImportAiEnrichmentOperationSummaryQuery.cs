using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

internal sealed class ImportAiEnrichmentOperationSummaryQuery : IImportAiEnrichmentOperationSummaryQuery
{
    private const int MaxSafeErrorMessageLength = 500;
    private const int MaxSourceLabelLength = 80;

    private readonly LinguaCoachDbContext _db;

    public ImportAiEnrichmentOperationSummaryQuery(LinguaCoachDbContext db) => _db = db;

    public async Task<IReadOnlyList<ImportAiEnrichmentOperationSummaryDto>?> GetForPlanAsync(
        Guid importPackageId, Guid planId, CancellationToken ct = default)
    {
        var planExists = await _db.ImportProfiles
            .AnyAsync(p => p.Id == planId && p.ImportPackageId == importPackageId, ct);
        if (!planExists) return null;

        // Loaded then ordered client-side — SQLite (the test provider) cannot translate ORDER BY
        // over a DateTimeOffset column server-side (see ImportPlanDtoHelpers).
        var operations = await _db.ImportAiEnrichmentOperations
            .Where(o => o.ImportPackageId == importPackageId && o.ImportProfileId == planId)
            .ToListAsync(ct);
        if (operations.Count == 0) return Array.Empty<ImportAiEnrichmentOperationSummaryDto>();

        var candidateIds = operations.Select(o => o.ResourceCandidateId).Distinct().ToList();
        var candidatesById = await _db.ResourceCandidates
            .Where(c => candidateIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        return operations
            .OrderBy(o => o.StartedAtUtc)
            .Select(o =>
            {
                candidatesById.TryGetValue(o.ResourceCandidateId, out var candidate);
                return new ImportAiEnrichmentOperationSummaryDto(
                    o.Id,
                    o.ResourceCandidateId,
                    TruncateSourceLabel(candidate?.CanonicalText) ?? "(deleted candidate)",
                    o.OperationType,
                    o.ProviderName,
                    o.ModelName,
                    o.Status.ToString(),
                    o.AttemptNumber,
                    o.Status == ImportAiOperationStatus.Succeeded,
                    o.InputTokens,
                    o.OutputTokens,
                    o.CalculatedCost,
                    o.Currency,
                    o.StartedAtUtc,
                    o.CompletedAtUtc,
                    TruncateSafeErrorMessage(o.FailureReason));
            })
            .ToList();
    }

    private static string? TruncateSourceLabel(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Length <= MaxSourceLabelLength ? text : text[..MaxSourceLabelLength] + "…";
    }

    private static string? TruncateSafeErrorMessage(string? reason)
    {
        if (string.IsNullOrEmpty(reason)) return reason;
        return reason.Length <= MaxSafeErrorMessageLength ? reason : reason[..MaxSafeErrorMessageLength] + "…";
    }
}
