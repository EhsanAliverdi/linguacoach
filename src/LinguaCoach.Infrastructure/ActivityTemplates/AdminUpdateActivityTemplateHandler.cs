using LinguaCoach.Application.ActivityTemplates;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ActivityTemplates;

public sealed class AdminUpdateActivityTemplateHandler : IAdminUpdateActivityTemplateHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly IFormIoSchemaValidationService _validator;

    public AdminUpdateActivityTemplateHandler(LinguaCoachDbContext db, IFormIoSchemaValidationService validator)
    {
        _db = db;
        _validator = validator;
    }

    public async Task<AdminActivityTemplateDto> HandleAsync(UpdateActivityTemplateCommand command, CancellationToken ct = default)
    {
        var template = await _db.ActivityTemplates.FirstOrDefaultAsync(t => t.Id == command.TemplateId, ct)
            ?? throw new ActivityTemplateValidationException($"Activity template {command.TemplateId} not found.");

        if (!string.IsNullOrWhiteSpace(command.FormIoBaseSchemaJson))
        {
            var schemaResult = _validator.ValidateSchema(command.FormIoBaseSchemaJson);
            if (!schemaResult.IsValid)
                throw new ActivityTemplateValidationException(schemaResult.Error ?? "Invalid Form.io schema.");
        }

        try
        {
            template.Update(
                command.Skill, command.CefrLevel, command.ActivityType, command.Subskill, command.PatternKey,
                command.ContextTagsJson, command.FocusTagsJson, command.CurriculumObjectiveKey,
                command.EstimatedDurationSeconds, command.AssetRequirementsJson);
        }
        catch (ArgumentException ex)
        {
            throw new ActivityTemplateValidationException(ex.Message);
        }

        template.SetSchema(
            command.FormIoBaseSchemaJson, command.ScoringModelJson, command.ValidationRulesJson,
            command.GenerationInstructions);

        await _db.SaveChangesAsync(ct);

        return ActivityTemplateMapper.ToDto(template);
    }
}
