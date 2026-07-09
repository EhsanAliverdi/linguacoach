using LinguaCoach.Application.ModuleDefinitions;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ModuleDefinitions;

public sealed class AdminModuleDefinitionListQueryHandler : IAdminModuleDefinitionListQuery
{
    private const int MaxPageSize = 200;
    private readonly LinguaCoachDbContext _db;

    public AdminModuleDefinitionListQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ModuleDefinitionListResult> HandleAsync(ListModuleDefinitionsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var filtered = _db.ModuleDefinitions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<AdminReviewStatus>(query.Status, ignoreCase: true, out var status))
            filtered = filtered.Where(m => m.ReviewStatus == status);
        if (!string.IsNullOrWhiteSpace(query.CefrLevel))
            filtered = filtered.Where(m => m.CefrLevel == query.CefrLevel.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(query.Skill))
            filtered = filtered.Where(m => m.Skill == query.Skill.Trim());
        if (!string.IsNullOrWhiteSpace(query.Subskill))
            filtered = filtered.Where(m => m.Subskill == query.Subskill.Trim());
        if (query.DifficultyBand.HasValue)
            filtered = filtered.Where(m => m.DifficultyBand == query.DifficultyBand.Value);
        if (!string.IsNullOrWhiteSpace(query.ContextTag))
        {
            var needle = TagNeedle(query.ContextTag);
            filtered = filtered.Where(m => m.ContextTagsJson.ToLower().Contains(needle));
        }
        if (!string.IsNullOrWhiteSpace(query.FocusTag))
        {
            var needle = TagNeedle(query.FocusTag);
            filtered = filtered.Where(m => m.FocusTagsJson.ToLower().Contains(needle));
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(m => m.Title.ToLower().Contains(search)
                || (m.Description != null && m.Description.ToLower().Contains(search)));
        }
        if (query.LearnItemId.HasValue)
        {
            var moduleIds = _db.ModuleDefinitionLearnItemLinks
                .Where(l => l.LearnItemId == query.LearnItemId.Value)
                .Select(l => l.ModuleDefinitionId).Distinct();
            filtered = filtered.Where(m => moduleIds.Contains(m.Id));
        }
        if (query.ActivityDefinitionId.HasValue)
        {
            var moduleIds = _db.ModuleDefinitionActivityLinks
                .Where(l => l.ActivityDefinitionId == query.ActivityDefinitionId.Value)
                .Select(l => l.ModuleDefinitionId).Distinct();
            filtered = filtered.Where(m => moduleIds.Contains(m.Id));
        }

        var totalCount = await filtered.CountAsync(ct);

        var items = await filtered
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var itemIds = items.Select(m => m.Id).ToList();
        var learnItemLinks = await _db.ModuleDefinitionLearnItemLinks
            .Where(l => itemIds.Contains(l.ModuleDefinitionId)).ToListAsync(ct);
        var activityLinks = await _db.ModuleDefinitionActivityLinks
            .Where(l => itemIds.Contains(l.ModuleDefinitionId)).ToListAsync(ct);
        var learnLookup = learnItemLinks.ToLookup(l => l.ModuleDefinitionId);
        var activityLookup = activityLinks.ToLookup(l => l.ModuleDefinitionId);

        return new ModuleDefinitionListResult(
            items.Select(m => ModuleDefinitionMappers.ToDto(
                m, learnLookup[m.Id].ToList(), activityLookup[m.Id].ToList())).ToList(),
            totalCount);
    }

    private static string TagNeedle(string tag) => $"\"{tag.Trim().ToLowerInvariant()}\"";
}

public sealed class AdminModuleDefinitionGetQueryHandler : IAdminModuleDefinitionGetQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminModuleDefinitionGetQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ModuleDefinitionDto?> HandleAsync(GetModuleDefinitionQuery query, CancellationToken ct = default)
    {
        var item = await _db.ModuleDefinitions.FirstOrDefaultAsync(m => m.Id == query.Id, ct);
        if (item is null) return null;

        var learnItemLinks = await _db.ModuleDefinitionLearnItemLinks
            .Where(l => l.ModuleDefinitionId == item.Id).ToListAsync(ct);
        var activityLinks = await _db.ModuleDefinitionActivityLinks
            .Where(l => l.ModuleDefinitionId == item.Id).ToListAsync(ct);

        return ModuleDefinitionMappers.ToDto(item, learnItemLinks, activityLinks);
    }
}
