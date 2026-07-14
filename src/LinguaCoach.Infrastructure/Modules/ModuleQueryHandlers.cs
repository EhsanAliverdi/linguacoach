using LinguaCoach.Application.Modules;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Modules;

public sealed class AdminModuleListQueryHandler : IAdminModuleListQuery
{
    private const int MaxPageSize = 200;
    private readonly LinguaCoachDbContext _db;

    public AdminModuleListQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ModuleListResult> HandleAsync(ListModulesQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var filtered = _db.Modules.Where(m => !m.IsArchived);

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
        if (query.LessonId.HasValue)
        {
            var moduleIds = _db.ModuleLessonLinks
                .Where(l => l.LessonId == query.LessonId.Value)
                .Select(l => l.ModuleId).Distinct();
            filtered = filtered.Where(m => moduleIds.Contains(m.Id));
        }
        if (query.ExerciseId.HasValue)
        {
            var moduleIds = _db.ModuleExerciseLinks
                .Where(l => l.ExerciseId == query.ExerciseId.Value)
                .Select(l => l.ModuleId).Distinct();
            filtered = filtered.Where(m => moduleIds.Contains(m.Id));
        }

        var totalCount = await filtered.CountAsync(ct);

        var items = await filtered
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var itemIds = items.Select(m => m.Id).ToList();
        var lessonLinks = await _db.ModuleLessonLinks
            .Where(l => itemIds.Contains(l.ModuleId)).ToListAsync(ct);
        var exerciseLinks = await _db.ModuleExerciseLinks
            .Where(l => itemIds.Contains(l.ModuleId)).ToListAsync(ct);
        var learnLookup = lessonLinks.ToLookup(l => l.ModuleId);
        var activityLookup = exerciseLinks.ToLookup(l => l.ModuleId);

        return new ModuleListResult(
            items.Select(m => ModuleMappers.ToDto(
                m, learnLookup[m.Id].ToList(), activityLookup[m.Id].ToList())).ToList(),
            totalCount);
    }

    private static string TagNeedle(string tag) => $"\"{tag.Trim().ToLowerInvariant()}\"";
}

public sealed class AdminModuleGetQueryHandler : IAdminModuleGetQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminModuleGetQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ModuleDto?> HandleAsync(GetModuleQuery query, CancellationToken ct = default)
    {
        var item = await _db.Modules.FirstOrDefaultAsync(m => m.Id == query.Id, ct);
        if (item is null) return null;

        var lessonLinks = await _db.ModuleLessonLinks
            .Where(l => l.ModuleId == item.Id).ToListAsync(ct);
        var exerciseLinks = await _db.ModuleExerciseLinks
            .Where(l => l.ModuleId == item.Id).ToListAsync(ct);

        return ModuleMappers.ToDto(item, lessonLinks, exerciseLinks);
    }
}
