using LinguaCoach.Application.ActivityDefinitions;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ActivityDefinitions;

public sealed class AdminActivityDefinitionListQueryHandler : IAdminActivityDefinitionListQuery
{
    private const int MaxPageSize = 200;
    private readonly LinguaCoachDbContext _db;

    public AdminActivityDefinitionListQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ActivityDefinitionListResult> HandleAsync(ListActivityDefinitionsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var filtered = _db.ActivityDefinitions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<AdminReviewStatus>(query.Status, ignoreCase: true, out var status))
            filtered = filtered.Where(a => a.ReviewStatus == status);
        if (!string.IsNullOrWhiteSpace(query.ActivityType))
            filtered = filtered.Where(a => a.ActivityType == query.ActivityType.Trim());
        if (!string.IsNullOrWhiteSpace(query.RendererType)
            && Enum.TryParse<ActivityRendererType>(query.RendererType, ignoreCase: true, out var rendererType))
            filtered = filtered.Where(a => a.RendererType == rendererType);
        if (!string.IsNullOrWhiteSpace(query.CefrLevel))
            filtered = filtered.Where(a => a.CefrLevel == query.CefrLevel.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(query.Skill))
            filtered = filtered.Where(a => a.Skill == query.Skill.Trim());
        if (!string.IsNullOrWhiteSpace(query.Subskill))
            filtered = filtered.Where(a => a.Subskill == query.Subskill.Trim());
        if (query.DifficultyBand.HasValue)
            filtered = filtered.Where(a => a.DifficultyBand == query.DifficultyBand.Value);
        if (query.LearnItemId.HasValue)
            filtered = filtered.Where(a => a.LearnItemId == query.LearnItemId.Value);
        if (!string.IsNullOrWhiteSpace(query.ContextTag))
        {
            var needle = TagNeedle(query.ContextTag);
            filtered = filtered.Where(a => a.ContextTagsJson.ToLower().Contains(needle));
        }
        if (!string.IsNullOrWhiteSpace(query.FocusTag))
        {
            var needle = TagNeedle(query.FocusTag);
            filtered = filtered.Where(a => a.FocusTagsJson.ToLower().Contains(needle));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(a => a.Title.ToLower().Contains(search) || a.Instructions.ToLower().Contains(search));
        }

        var totalCount = await filtered.CountAsync(ct);

        var items = await filtered
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var itemIds = items.Select(a => a.Id).ToList();
        var links = await _db.ActivityResourceLinks
            .Where(l => itemIds.Contains(l.ActivityDefinitionId))
            .ToListAsync(ct);
        var linksByItem = links.ToLookup(l => l.ActivityDefinitionId);

        return new ActivityDefinitionListResult(
            items.Select(a => ActivityDefinitionMappers.ToDto(a, linksByItem[a.Id].ToList())).ToList(),
            totalCount);
    }

    private static string TagNeedle(string tag) => $"\"{tag.Trim().ToLowerInvariant()}\"";
}

public sealed class AdminActivityDefinitionGetQueryHandler : IAdminActivityDefinitionGetQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminActivityDefinitionGetQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ActivityDefinitionDto?> HandleAsync(GetActivityDefinitionQuery query, CancellationToken ct = default)
    {
        var item = await _db.ActivityDefinitions.FirstOrDefaultAsync(a => a.Id == query.Id, ct);
        if (item is null) return null;

        var links = await _db.ActivityResourceLinks.Where(l => l.ActivityDefinitionId == item.Id).ToListAsync(ct);
        return ActivityDefinitionMappers.ToDto(item, links);
    }
}
