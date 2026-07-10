using LinguaCoach.Application.Lessons;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Lessons;

public sealed class AdminLessonListQueryHandler : IAdminLessonListQuery
{
    private const int MaxPageSize = 200;
    private readonly LinguaCoachDbContext _db;

    public AdminLessonListQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<LessonListResult> HandleAsync(ListLessonsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var filtered = _db.Lessons.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<AdminReviewStatus>(query.Status, ignoreCase: true, out var status))
            filtered = filtered.Where(i => i.ReviewStatus == status);
        if (!string.IsNullOrWhiteSpace(query.CefrLevel))
            filtered = filtered.Where(i => i.CefrLevel == query.CefrLevel.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(query.Skill))
            filtered = filtered.Where(i => i.Skill == query.Skill.Trim());
        if (!string.IsNullOrWhiteSpace(query.Subskill))
            filtered = filtered.Where(i => i.Subskill == query.Subskill.Trim());
        if (query.DifficultyBand.HasValue)
            filtered = filtered.Where(i => i.DifficultyBand == query.DifficultyBand.Value);
        if (!string.IsNullOrWhiteSpace(query.ContextTag))
        {
            var needle = TagNeedle(query.ContextTag);
            filtered = filtered.Where(i => i.ContextTagsJson.ToLower().Contains(needle));
        }
        if (!string.IsNullOrWhiteSpace(query.FocusTag))
        {
            var needle = TagNeedle(query.FocusTag);
            filtered = filtered.Where(i => i.FocusTagsJson.ToLower().Contains(needle));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(i => i.Title.ToLower().Contains(search) || i.Body.ToLower().Contains(search));
        }

        if (query.ResourceId.HasValue || !string.IsNullOrWhiteSpace(query.ResourceType))
        {
            var linkQuery = _db.LessonResourceLinks.AsQueryable();
            if (query.ResourceId.HasValue)
                linkQuery = linkQuery.Where(l => l.ResourceId == query.ResourceId.Value);
            if (!string.IsNullOrWhiteSpace(query.ResourceType)
                && Enum.TryParse<PublishedResourceType>(query.ResourceType, ignoreCase: true, out var resourceType))
                linkQuery = linkQuery.Where(l => l.ResourceType == resourceType);

            var matchingLessonIds = linkQuery.Select(l => l.LessonId).Distinct();
            filtered = filtered.Where(i => matchingLessonIds.Contains(i.Id));
        }

        var totalCount = await filtered.CountAsync(ct);

        var items = await filtered
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var itemIds = items.Select(i => i.Id).ToList();
        var links = await _db.LessonResourceLinks
            .Where(l => itemIds.Contains(l.LessonId))
            .ToListAsync(ct);
        var linksByItem = links.ToLookup(l => l.LessonId);

        return new LessonListResult(
            items.Select(i => LessonMappers.ToDto(i, linksByItem[i.Id].ToList())).ToList(),
            totalCount);
    }

    private static string TagNeedle(string tag) => $"\"{tag.Trim().ToLowerInvariant()}\"";
}

public sealed class AdminLessonGetQueryHandler : IAdminLessonGetQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminLessonGetQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<LessonDto?> HandleAsync(GetLessonQuery query, CancellationToken ct = default)
    {
        var item = await _db.Lessons.FirstOrDefaultAsync(i => i.Id == query.Id, ct);
        if (item is null) return null;

        var links = await _db.LessonResourceLinks.Where(l => l.LessonId == item.Id).ToListAsync(ct);
        return LessonMappers.ToDto(item, links);
    }
}
