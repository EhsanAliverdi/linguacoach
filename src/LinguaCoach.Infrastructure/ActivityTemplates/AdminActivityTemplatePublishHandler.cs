using LinguaCoach.Application.ActivityTemplates;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ActivityTemplates;

public sealed class AdminActivityTemplatePublishHandler : IAdminActivityTemplatePublishHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminActivityTemplatePublishHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<AdminActivityTemplateDto> HandleAsync(SetActivityTemplatePublishedCommand command, CancellationToken ct = default)
    {
        var template = await _db.ActivityTemplates.FirstOrDefaultAsync(t => t.Id == command.TemplateId, ct)
            ?? throw new ActivityTemplateValidationException($"Activity template {command.TemplateId} not found.");

        try
        {
            if (command.Publish)
                template.Publish();
            else
                template.Unpublish();
        }
        catch (InvalidOperationException ex)
        {
            throw new ActivityTemplateValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        return ActivityTemplateMapper.ToDto(template);
    }
}
