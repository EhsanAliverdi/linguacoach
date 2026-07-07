using LinguaCoach.Application.Placement;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Placement;

public sealed class AdminPlacementItemListQueryHandler : IAdminPlacementItemListQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminPlacementItemListQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminPlacementItemListResult> HandleAsync(
        ListAdminPlacementItemsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = _db.PlacementItemDefinitions.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Skill) && !string.Equals(query.Skill, "all", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(i => i.Skill == query.Skill);

        // Approximate text search: the question label/choices an admin would recognize appear
        // verbatim inside the authored Form.io schema JSON, so a substring match against it is a
        // reasonable proxy without needing a separate searchable/persisted label column.
        if (!string.IsNullOrWhiteSpace(query.Search))
            filtered = filtered.Where(i => i.FormIoSchemaJson != null && i.FormIoSchemaJson.Contains(query.Search));

        var totalCount = await filtered.CountAsync(ct);

        var items = await filtered
            .OrderBy(i => i.Skill).ThenBy(i => i.CefrLevel).ThenBy(i => i.ItemOrder)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var overallTotalCount = await _db.PlacementItemDefinitions.CountAsync(ct);
        var enabledCount = await _db.PlacementItemDefinitions.CountAsync(i => i.IsEnabled, ct);
        var skillCount = await _db.PlacementItemDefinitions.Select(i => i.Skill).Distinct().CountAsync(ct);

        var dtos = items.Select(PlacementItemMapper.ToDto).ToList();

        return new AdminPlacementItemListResult(dtos, totalCount, overallTotalCount, enabledCount, skillCount);
    }
}
