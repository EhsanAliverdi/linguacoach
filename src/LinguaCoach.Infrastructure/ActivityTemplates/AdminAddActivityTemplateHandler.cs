using LinguaCoach.Application.ActivityTemplates;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ActivityTemplates;

public sealed class AdminAddActivityTemplateHandler : IAdminAddActivityTemplateHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly IFormIoSchemaValidationService _validator;

    public AdminAddActivityTemplateHandler(LinguaCoachDbContext db, IFormIoSchemaValidationService validator)
    {
        _db = db;
        _validator = validator;
    }

    public async Task<AdminActivityTemplateDto> HandleAsync(AddActivityTemplateCommand command, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(command.FormIoBaseSchemaJson))
        {
            var schemaResult = _validator.ValidateSchema(command.FormIoBaseSchemaJson);
            if (!schemaResult.IsValid)
                throw new ActivityTemplateValidationException(schemaResult.Error ?? "Invalid Form.io schema.");
        }

        var duplicateKey = await _db.ActivityTemplates.AnyAsync(t => t.Key == command.Key.Trim(), ct);
        if (duplicateKey)
            throw new ActivityTemplateValidationException($"An activity template with key '{command.Key}' already exists.");

        ActivityTemplate template;
        try
        {
            template = new ActivityTemplate(
                command.Key, command.Skill, command.CefrLevel, command.ActivityType,
                command.Subskill, command.PatternKey, command.ContextTagsJson, command.FocusTagsJson,
                command.CurriculumObjectiveKey, command.FormIoBaseSchemaJson, command.GenerationInstructions,
                command.ScoringModelJson, command.ValidationRulesJson,
                command.EstimatedDurationSeconds, command.AssetRequirementsJson);
        }
        catch (ArgumentException ex)
        {
            throw new ActivityTemplateValidationException(ex.Message);
        }

        _db.ActivityTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        return ActivityTemplateMapper.ToDto(template);
    }
}
