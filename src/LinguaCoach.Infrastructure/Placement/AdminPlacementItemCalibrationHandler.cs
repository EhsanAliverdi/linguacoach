using LinguaCoach.Application.Placement;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>
/// Records calibration statistics (DiscriminationIndex/CalibrationSampleSize) computed from real
/// attempt data. No automatic computation exists yet — this is a manual admin entry point until
/// a future statistics job populates it (docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md, Phase 7).
/// </summary>
public sealed class AdminPlacementItemCalibrationHandler : IAdminPlacementItemCalibrationHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminPlacementItemCalibrationHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<AdminPlacementItemDto> HandleAsync(SetPlacementItemCalibrationStatsCommand command, CancellationToken ct = default)
    {
        var item = await _db.PlacementItemDefinitions.FirstOrDefaultAsync(i => i.Id == command.ItemId, ct)
            ?? throw new PlacementItemValidationException($"Placement item {command.ItemId} not found.");

        try
        {
            item.SetCalibrationStats(command.DiscriminationIndex, command.CalibrationSampleSize);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new PlacementItemValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        return PlacementItemMapper.ToDto(item);
    }
}
