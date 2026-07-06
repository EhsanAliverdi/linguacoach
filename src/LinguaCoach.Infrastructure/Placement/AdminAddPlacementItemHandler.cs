using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Placement;

public sealed class AdminAddPlacementItemHandler : IAdminAddPlacementItemHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly IFormIoSchemaValidationService _validator;

    public AdminAddPlacementItemHandler(LinguaCoachDbContext db, IFormIoSchemaValidationService validator)
    {
        _db = db;
        _validator = validator;
    }

    public async Task<AdminPlacementItemDto> HandleAsync(
        AddPlacementItemCommand command, CancellationToken ct = default)
    {
        var schemaResult = _validator.ValidateSchema(command.FormIoSchemaJson);
        if (!schemaResult.IsValid)
            throw new PlacementItemValidationException(schemaResult.Error ?? "Invalid Form.io schema.");

        PlacementFormIoScoringValidator.ValidateAndParse(command.FormIoSchemaJson, command.ScoringRulesJson);

        var duplicate = await _db.PlacementItemDefinitions
            .AnyAsync(i => i.Prompt == command.Prompt, ct);
        if (duplicate)
            throw new PlacementItemValidationException("An item with this exact prompt already exists.");

        PlacementItemDefinition item;
        try
        {
            item = new PlacementItemDefinition(
                command.Skill, command.CefrLevel, command.ItemType, command.Prompt,
                command.ItemOrder, command.IsEnabled);
        }
        catch (ArgumentException ex)
        {
            throw new PlacementItemValidationException(ex.Message);
        }

        var rendererKind = Enum.TryParse<FormRendererKind>(command.RendererKind, ignoreCase: true, out var parsedKind) ? parsedKind : FormRendererKind.FormIo;
        item.SetFormIoAuthoring(command.FormIoSchemaJson, command.ScoringRulesJson, rendererKind);

        _db.PlacementItemDefinitions.Add(item);
        await _db.SaveChangesAsync(ct);

        return new AdminPlacementItemDto(
            item.Id, item.Skill, item.CefrLevel, item.ItemType, item.Prompt, item.ItemOrder, item.IsEnabled,
            item.FormIoSchemaJson, item.ScoringRulesJson, item.ScoringRulesVersion, item.RendererKind.ToString());
    }
}
