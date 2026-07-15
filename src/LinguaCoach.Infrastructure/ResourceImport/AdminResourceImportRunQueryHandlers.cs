using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

public sealed class AdminResourceImportRunListQueryHandler : IAdminResourceImportRunListQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceImportRunListQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceImportRunListResult> HandleAsync(
        ListAdminResourceImportRunsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = _db.ResourceImportRuns.AsQueryable();

        if (query.SourceId.HasValue)
            filtered = filtered.Where(r => r.CefrResourceSourceId == query.SourceId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<ResourceImportRunStatus>(query.Status, ignoreCase: true, out var status))
            filtered = filtered.Where(r => r.Status == status);

        var totalCount = await filtered.CountAsync(ct);
        var overallTotalCount = await _db.ResourceImportRuns.CountAsync(ct);

        // Ordered client-side, not via OrderBy in the query — SQLite (every test project's DB per
        // this codebase's convention) cannot translate ORDER BY on a DateTimeOffset column. Same
        // fix already applied in ImportPackageProcessingService.ProcessPendingAsync.
        var runs = (await filtered.ToListAsync(ct))
            .OrderByDescending(r => r.StartedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var sourceIds = runs.Select(r => r.CefrResourceSourceId).Distinct().ToList();
        var sourceNames = await _db.CefrResourceSources
            .Where(s => sourceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var items = runs
            .Select(r => ResourceImportMappers.ToDto(r, sourceNames.GetValueOrDefault(r.CefrResourceSourceId, "(unknown)")))
            .ToList();

        return new AdminResourceImportRunListResult(items, totalCount, overallTotalCount);
    }
}

public sealed class AdminResourceImportRunGetQueryHandler : IAdminResourceImportRunGetQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceImportRunGetQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceImportRunDto?> HandleAsync(GetAdminResourceImportRunQuery query, CancellationToken ct = default)
    {
        var run = await _db.ResourceImportRuns.FirstOrDefaultAsync(r => r.Id == query.RunId, ct);
        if (run is null) return null;

        var sourceName = await _db.CefrResourceSources
            .Where(s => s.Id == run.CefrResourceSourceId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct) ?? "(unknown)";

        return ResourceImportMappers.ToDto(run, sourceName);
    }
}

public sealed class AdminResourceRawRecordListQueryHandler : IAdminResourceRawRecordListQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceRawRecordListQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceRawRecordListResult> HandleAsync(
        ListAdminResourceRawRecordsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 500);

        var filtered = _db.ResourceRawRecords.Where(r => r.ResourceImportRunId == query.ResourceImportRunId);

        if (!string.IsNullOrWhiteSpace(query.ExtractionStatus)
            && Enum.TryParse<ResourceRawRecordStatus>(query.ExtractionStatus, ignoreCase: true, out var status))
            filtered = filtered.Where(r => r.ExtractionStatus == status);

        var totalCount = await filtered.CountAsync(ct);

        var items = await filtered
            .OrderBy(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new AdminResourceRawRecordListResult(items.Select(ResourceImportMappers.ToDto).ToList(), totalCount);
    }
}

public sealed class AdminResourceRawRecordGetQueryHandler : IAdminResourceRawRecordGetQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceRawRecordGetQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceRawRecordDto?> HandleAsync(GetAdminResourceRawRecordQuery query, CancellationToken ct = default)
    {
        var record = await _db.ResourceRawRecords.FirstOrDefaultAsync(r => r.Id == query.RawRecordId, ct);
        return record is null ? null : ResourceImportMappers.ToDto(record);
    }
}
