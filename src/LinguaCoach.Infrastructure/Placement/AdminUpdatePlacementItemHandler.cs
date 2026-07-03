using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Questions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Placement;

public sealed class AdminUpdatePlacementItemHandler : IAdminUpdatePlacementItemHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminUpdatePlacementItemHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminPlacementItemDto> HandleAsync(
        UpdatePlacementItemCommand command, CancellationToken ct = default)
    {
        var item = await _db.PlacementItemDefinitions.FirstOrDefaultAsync(i => i.Id == command.ItemId, ct)
            ?? throw new PlacementItemValidationException($"Placement item {command.ItemId} not found.");

        var duplicate = await _db.PlacementItemDefinitions
            .AnyAsync(i => i.Id != command.ItemId && i.Prompt == command.Prompt, ct);
        if (duplicate)
            throw new PlacementItemValidationException("An item with this exact prompt already exists.");

        try
        {
            item.Update(
                command.Skill, command.CefrLevel, command.ItemType, command.Prompt, command.CorrectAnswer,
                command.ItemOrder, command.IsEnabled, command.ReadingPassage, command.ListeningAudioScript);
        }
        catch (ArgumentException ex)
        {
            throw new PlacementItemValidationException(ex.Message);
        }

        item.SetContent(LegacyPlacementContentConverter.FromLegacyItem(
            item.ItemType, item.Prompt, item.CorrectAnswer, item.ReadingPassage, item.ListeningAudioScript));

        await _db.SaveChangesAsync(ct);

        return new AdminPlacementItemDto(
            item.Id, item.Skill, item.CefrLevel, item.ItemType, item.Prompt, item.CorrectAnswer,
            item.ReadingPassage, item.ListeningAudioScript, item.ItemOrder, item.IsEnabled);
    }
}
