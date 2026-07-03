using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Questions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Placement;

public sealed class AdminAddPlacementItemHandler : IAdminAddPlacementItemHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminAddPlacementItemHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminPlacementItemDto> HandleAsync(
        AddPlacementItemCommand command, CancellationToken ct = default)
    {
        var duplicate = await _db.PlacementItemDefinitions
            .AnyAsync(i => i.Prompt == command.Prompt, ct);
        if (duplicate)
            throw new PlacementItemValidationException($"An item with this exact prompt already exists.");

        PlacementItemDefinition item;
        try
        {
            item = new PlacementItemDefinition(
                command.Skill, command.CefrLevel, command.ItemType, command.Prompt, command.CorrectAnswer,
                command.ItemOrder, command.IsEnabled, command.ReadingPassage, command.ListeningAudioScript);
        }
        catch (ArgumentException ex)
        {
            throw new PlacementItemValidationException(ex.Message);
        }

        item.SetContent(LegacyPlacementContentConverter.FromLegacyItem(
            item.ItemType, item.Prompt, item.CorrectAnswer, item.ReadingPassage, item.ListeningAudioScript));

        _db.PlacementItemDefinitions.Add(item);
        await _db.SaveChangesAsync(ct);

        return new AdminPlacementItemDto(
            item.Id, item.Skill, item.CefrLevel, item.ItemType, item.Prompt, item.CorrectAnswer,
            item.ReadingPassage, item.ListeningAudioScript, item.ItemOrder, item.IsEnabled);
    }
}
