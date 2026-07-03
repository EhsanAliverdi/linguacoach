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
        // Unified Question-Schema Phase 4: admins author Content directly; the legacy flat
        // fields PlacementItemDefinition still requires are derived from it.
        var (itemType, prompt, correctAnswer, readingPassage, listeningAudioScript) =
            LegacyPlacementContentConverter.ToLegacyFields(command.Content);

        var duplicate = await _db.PlacementItemDefinitions
            .AnyAsync(i => i.Prompt == prompt, ct);
        if (duplicate)
            throw new PlacementItemValidationException("An item with this exact prompt already exists.");

        PlacementItemDefinition item;
        try
        {
            item = new PlacementItemDefinition(
                command.Skill, command.CefrLevel, itemType, prompt, correctAnswer,
                command.ItemOrder, command.IsEnabled, readingPassage, listeningAudioScript);
        }
        catch (ArgumentException ex)
        {
            throw new PlacementItemValidationException(ex.Message);
        }

        item.SetContent(command.Content);

        _db.PlacementItemDefinitions.Add(item);
        await _db.SaveChangesAsync(ct);

        return new AdminPlacementItemDto(
            item.Id, item.Skill, item.CefrLevel, item.ItemType, item.Prompt, item.CorrectAnswer,
            item.ReadingPassage, item.ListeningAudioScript, item.ItemOrder, item.IsEnabled, command.Content);
    }
}
