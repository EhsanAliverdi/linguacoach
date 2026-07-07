using LinguaCoach.Application.ActivityTemplates;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ActivityTemplates;

public sealed class AdminRemoveActivityTemplateHandler : IAdminRemoveActivityTemplateHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminRemoveActivityTemplateHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task HandleAsync(RemoveActivityTemplateCommand command, CancellationToken ct = default)
    {
        var template = await _db.ActivityTemplates.FirstOrDefaultAsync(t => t.Id == command.TemplateId, ct)
            ?? throw new ActivityTemplateValidationException($"Activity template {command.TemplateId} not found.");

        _db.ActivityTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
    }
}
