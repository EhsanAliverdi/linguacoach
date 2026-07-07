using LinguaCoach.Application.ActivityTemplates;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ActivityTemplates;

public sealed class AdminActivityTemplateGetQueryHandler : IAdminActivityTemplateGetQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminActivityTemplateGetQueryHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<AdminActivityTemplateDto?> HandleAsync(GetAdminActivityTemplateQuery query, CancellationToken ct = default)
    {
        var template = await _db.ActivityTemplates.FirstOrDefaultAsync(t => t.Id == query.TemplateId, ct);
        return template is null ? null : ActivityTemplateMapper.ToDto(template);
    }
}
