using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

internal sealed class ImportSttOperationSummaryQuery : IImportSttOperationSummaryQuery
{
    private const int MaxSafeErrorMessageLength = 500;

    private readonly LinguaCoachDbContext _db;

    public ImportSttOperationSummaryQuery(LinguaCoachDbContext db) => _db = db;

    public async Task<IReadOnlyList<ImportSttOperationSummaryDto>?> GetForPlanAsync(
        Guid importPackageId, Guid planId, CancellationToken ct = default)
    {
        var planExists = await _db.ImportProfiles
            .AnyAsync(p => p.Id == planId && p.ImportPackageId == importPackageId, ct);
        if (!planExists) return null;

        // Loaded then joined/ordered client-side — SQLite (the test provider) cannot translate
        // ORDER BY over a DateTimeOffset column server-side (see ImportPlanDtoHelpers).
        var operations = await _db.ImportSttOperations
            .Where(o => o.ImportPackageId == importPackageId && o.ImportProfileId == planId)
            .ToListAsync(ct);
        if (operations.Count == 0) return Array.Empty<ImportSttOperationSummaryDto>();

        var assetIds = operations.Select(o => o.ImportAssetId).Distinct().ToList();
        var assetsById = await _db.ImportAssets
            .Where(a => assetIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        return operations
            .OrderBy(o => o.StartedAtUtc)
            .Select(o =>
            {
                assetsById.TryGetValue(o.ImportAssetId, out var asset);
                return new ImportSttOperationSummaryDto(
                    o.Id,
                    asset?.OriginalFileName ?? "(deleted asset)",
                    asset?.RelativePath ?? "(deleted asset)",
                    o.ProviderName,
                    o.ModelName,
                    o.Status.ToString(),
                    o.AttemptNumber,
                    o.Status == ImportSttOperationStatus.Succeeded,
                    o.CalculatedCost,
                    o.Currency,
                    o.StartedAtUtc,
                    o.CompletedAtUtc,
                    TruncateSafeErrorMessage(o.FailureReason));
            })
            .ToList();
    }

    private static string? TruncateSafeErrorMessage(string? reason)
    {
        if (string.IsNullOrEmpty(reason)) return reason;
        return reason.Length <= MaxSafeErrorMessageLength ? reason : reason[..MaxSafeErrorMessageLength] + "…";
    }
}
