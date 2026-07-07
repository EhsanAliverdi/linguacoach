using LinguaCoach.Application.FormIo;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Placement;

public sealed class AdminUpdatePlacementItemHandler : IAdminUpdatePlacementItemHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly IFormIoSchemaValidationService _validator;
    private readonly IFormIoQuizSchemaSplitter _splitter;

    public AdminUpdatePlacementItemHandler(LinguaCoachDbContext db, IFormIoSchemaValidationService validator, IFormIoQuizSchemaSplitter splitter)
    {
        _db = db;
        _validator = validator;
        _splitter = splitter;
    }

    public async Task<AdminPlacementItemDto> HandleAsync(
        UpdatePlacementItemCommand command, CancellationToken ct = default)
    {
        var item = await _db.PlacementItemDefinitions.FirstOrDefaultAsync(i => i.Id == command.ItemId, ct)
            ?? throw new PlacementItemValidationException($"Placement item {command.ItemId} not found.");

        string formIoSchemaJson, scoringRulesJson;
        if (command.AuthoringSchemaJson is not null)
        {
            var split = _splitter.Split(command.AuthoringSchemaJson);
            formIoSchemaJson = split.StudentSchemaJson;
            scoringRulesJson = split.ScoringRulesJson;
        }
        else
        {
            formIoSchemaJson = command.FormIoSchemaJson;
            scoringRulesJson = command.ScoringRulesJson;
        }

        var schemaResult = _validator.ValidateSchema(formIoSchemaJson);
        if (!schemaResult.IsValid)
            throw new PlacementItemValidationException(schemaResult.Error ?? "Invalid Form.io schema.");

        PlacementFormIoScoringValidator.ValidateAndParse(formIoSchemaJson, scoringRulesJson);

        var identityHash = PlacementItemSchemaLabel.ComputeIdentityHash(command.Skill, command.CefrLevel, formIoSchemaJson);
        var existingSameLevel = await _db.PlacementItemDefinitions
            .Where(i => i.Id != command.ItemId && i.Skill == command.Skill && i.CefrLevel == command.CefrLevel)
            .Select(i => i.FormIoSchemaJson)
            .ToListAsync(ct);
        var duplicate = existingSameLevel.Any(schema =>
            PlacementItemSchemaLabel.ComputeIdentityHash(command.Skill, command.CefrLevel, schema) == identityHash);
        if (duplicate)
            throw new PlacementItemValidationException("An item with this exact skill, level, and Form.io schema already exists.");

        try
        {
            item.Update(command.Skill, command.CefrLevel, command.ItemOrder, command.IsEnabled);
        }
        catch (ArgumentException ex)
        {
            throw new PlacementItemValidationException(ex.Message);
        }

        var rendererKind = Enum.TryParse<FormRendererKind>(command.RendererKind, ignoreCase: true, out var parsedKind) ? parsedKind : FormRendererKind.FormIo;
        item.SetFormIoAuthoring(formIoSchemaJson, scoringRulesJson, rendererKind);
        if (command.AuthoringSchemaJson is not null)
            item.SetAuthoringSchema(command.AuthoringSchemaJson);

        await _db.SaveChangesAsync(ct);

        return new AdminPlacementItemDto(
            item.Id, item.Skill, item.CefrLevel, item.ItemOrder, item.IsEnabled,
            item.FormIoSchemaJson, item.ScoringRulesJson, item.ScoringRulesVersion, item.RendererKind.ToString(),
            PlacementItemSchemaLabel.ExtractLabel(item.FormIoSchemaJson), item.AuthoringSchemaJson);
    }
}
