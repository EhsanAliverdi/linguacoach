using LinguaCoach.Application.ActivityTemplates;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ActivityTemplates;

public sealed class AdminActivityTemplateListQueryHandler : IAdminActivityTemplateListQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminActivityTemplateListQueryHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<AdminActivityTemplateListResult> HandleAsync(ListAdminActivityTemplatesQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = _db.ActivityTemplates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Skill) && !string.Equals(query.Skill, "all", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(t => t.Skill == query.Skill);

        if (!string.IsNullOrWhiteSpace(query.CefrLevel) && !string.Equals(query.CefrLevel, "all", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(t => t.CefrLevel == query.CefrLevel);

        if (!string.IsNullOrWhiteSpace(query.ReviewStatus)
            && Enum.TryParse<AdminReviewStatus>(query.ReviewStatus, ignoreCase: true, out var reviewStatus))
            filtered = filtered.Where(t => t.ReviewStatus == reviewStatus);

        if (!string.IsNullOrWhiteSpace(query.Search))
            filtered = filtered.Where(t => t.Key.Contains(query.Search));

        var totalCount = await filtered.CountAsync(ct);
        var overallTotalCount = await _db.ActivityTemplates.CountAsync(ct);
        var publishedCount = await _db.ActivityTemplates.CountAsync(t => t.IsPublished, ct);
        var skillCount = await _db.ActivityTemplates.Select(t => t.Skill).Distinct().CountAsync(ct);

        var items = await filtered
            .OrderBy(t => t.Skill).ThenBy(t => t.CefrLevel).ThenBy(t => t.Key)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new AdminActivityTemplateListResult(
            items.Select(ActivityTemplateMapper.ToDto).ToList(),
            totalCount, overallTotalCount, publishedCount, skillCount);
    }
}
