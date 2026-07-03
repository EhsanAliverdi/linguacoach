using LinguaCoach.Application.Placement;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Placement;

public sealed class AdminPlacementItemListQueryHandler : IAdminPlacementItemListQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminPlacementItemListQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<IReadOnlyList<AdminPlacementItemDto>> HandleAsync(
        ListAdminPlacementItemsQuery query, CancellationToken ct = default)
    {
        var items = await _db.PlacementItemDefinitions
            .OrderBy(i => i.Skill).ThenBy(i => i.CefrLevel).ThenBy(i => i.ItemOrder)
            .ToListAsync(ct);

        return items.Select(i => new AdminPlacementItemDto(
            i.Id, i.Skill, i.CefrLevel, i.ItemType, i.Prompt, i.CorrectAnswer,
            i.ReadingPassage, i.ListeningAudioScript, i.ItemOrder, i.IsEnabled)).ToList();
    }
}
