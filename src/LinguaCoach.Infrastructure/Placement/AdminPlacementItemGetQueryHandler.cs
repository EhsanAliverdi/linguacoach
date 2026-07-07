using LinguaCoach.Application.Placement;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Placement;

public sealed class AdminPlacementItemGetQueryHandler : IAdminPlacementItemGetQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminPlacementItemGetQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminPlacementItemDto?> HandleAsync(
        GetAdminPlacementItemQuery query, CancellationToken ct = default)
    {
        var item = await _db.PlacementItemDefinitions.FirstOrDefaultAsync(i => i.Id == query.ItemId, ct);
        if (item is null) return null;

        return new AdminPlacementItemDto(
            item.Id, item.Skill, item.CefrLevel, item.ItemOrder, item.IsEnabled,
            item.FormIoSchemaJson, item.ScoringRulesJson, item.ScoringRulesVersion, item.RendererKind.ToString(),
            PlacementItemSchemaLabel.ExtractLabel(item.FormIoSchemaJson));
    }
}
