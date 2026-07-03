using LinguaCoach.Application.Placement;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Placement;

public sealed class AdminRemovePlacementItemHandler : IAdminRemovePlacementItemHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminRemovePlacementItemHandler(LinguaCoachDbContext db) => _db = db;

    public async Task HandleAsync(RemovePlacementItemCommand command, CancellationToken ct = default)
    {
        var item = await _db.PlacementItemDefinitions.FirstOrDefaultAsync(i => i.Id == command.ItemId, ct)
            ?? throw new PlacementItemValidationException($"Placement item {command.ItemId} not found.");

        _db.PlacementItemDefinitions.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}
