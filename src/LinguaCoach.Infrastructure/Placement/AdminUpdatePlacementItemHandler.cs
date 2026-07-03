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

        // Unified Question-Schema Phase 4: admins author Content directly; the legacy flat
        // fields PlacementItemDefinition still requires are derived from it.
        var (itemType, prompt, correctAnswer, readingPassage, listeningAudioScript) =
            LegacyPlacementContentConverter.ToLegacyFields(command.Content);

        var duplicate = await _db.PlacementItemDefinitions
            .AnyAsync(i => i.Id != command.ItemId && i.Prompt == prompt, ct);
        if (duplicate)
            throw new PlacementItemValidationException("An item with this exact prompt already exists.");

        try
        {
            item.Update(
                command.Skill, command.CefrLevel, itemType, prompt, correctAnswer,
                command.ItemOrder, command.IsEnabled, readingPassage, listeningAudioScript);
        }
        catch (ArgumentException ex)
        {
            throw new PlacementItemValidationException(ex.Message);
        }

        item.SetContent(command.Content);

        await _db.SaveChangesAsync(ct);

        return new AdminPlacementItemDto(
            item.Id, item.Skill, item.CefrLevel, item.ItemType, item.Prompt, item.CorrectAnswer,
            item.ReadingPassage, item.ListeningAudioScript, item.ItemOrder, item.IsEnabled, command.Content);
    }
}
