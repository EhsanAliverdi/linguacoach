using LinguaCoach.Application.Placement;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Placement;

public sealed class AdminPlacementItemReviewHandler : IAdminPlacementItemReviewHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminPlacementItemReviewHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<AdminPlacementItemDto> HandleAsync(SetPlacementItemReviewStatusCommand command, CancellationToken ct = default)
    {
        var item = await _db.PlacementItemDefinitions.FirstOrDefaultAsync(i => i.Id == command.ItemId, ct)
            ?? throw new PlacementItemValidationException($"Placement item {command.ItemId} not found.");

        try
        {
            switch (command.Action.Trim().ToLowerInvariant())
            {
                case "approve":
                    item.Approve();
                    break;
                case "reject":
                    if (string.IsNullOrWhiteSpace(command.Reason))
                        throw new PlacementItemValidationException("Reason is required to reject a placement item.");
                    item.Reject(command.Reason);
                    break;
                case "reset":
                    item.ResetToPendingReview();
                    break;
                default:
                    throw new PlacementItemValidationException($"Unknown review action '{command.Action}'. Expected approve, reject, or reset.");
            }
        }
        catch (ArgumentException ex)
        {
            throw new PlacementItemValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        return PlacementItemMapper.ToDto(item);
    }
}
