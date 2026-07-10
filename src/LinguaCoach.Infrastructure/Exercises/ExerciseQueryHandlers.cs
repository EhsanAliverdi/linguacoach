using LinguaCoach.Application.Exercises;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Exercises;

public sealed class AdminExerciseListQueryHandler : IAdminExerciseListQuery
{
    private const int MaxPageSize = 200;
    private readonly LinguaCoachDbContext _db;

    public AdminExerciseListQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ExerciseListResult> HandleAsync(ListExercisesQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var filtered = _db.Exercises.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<AdminReviewStatus>(query.Status, ignoreCase: true, out var status))
            filtered = filtered.Where(a => a.ReviewStatus == status);
        if (!string.IsNullOrWhiteSpace(query.ActivityType))
            filtered = filtered.Where(a => a.ActivityType == query.ActivityType.Trim());
        if (!string.IsNullOrWhiteSpace(query.RendererType)
            && Enum.TryParse<ExerciseRendererType>(query.RendererType, ignoreCase: true, out var rendererType))
            filtered = filtered.Where(a => a.RendererType == rendererType);
        if (!string.IsNullOrWhiteSpace(query.CefrLevel))
            filtered = filtered.Where(a => a.CefrLevel == query.CefrLevel.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(query.Skill))
            filtered = filtered.Where(a => a.Skill == query.Skill.Trim());
        if (!string.IsNullOrWhiteSpace(query.Subskill))
            filtered = filtered.Where(a => a.Subskill == query.Subskill.Trim());
        if (query.DifficultyBand.HasValue)
            filtered = filtered.Where(a => a.DifficultyBand == query.DifficultyBand.Value);
        if (query.LessonId.HasValue)
            filtered = filtered.Where(a => a.LessonId == query.LessonId.Value);
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
        var links = await _db.ExerciseResourceLinks
            .Where(l => itemIds.Contains(l.ExerciseId))
            .ToListAsync(ct);
        var linksByItem = links.ToLookup(l => l.ExerciseId);

        return new ExerciseListResult(
            items.Select(a => ExerciseMappers.ToDto(a, linksByItem[a.Id].ToList())).ToList(),
            totalCount);
    }

    private static string TagNeedle(string tag) => $"\"{tag.Trim().ToLowerInvariant()}\"";
}

public sealed class AdminExerciseGetQueryHandler : IAdminExerciseGetQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminExerciseGetQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ExerciseDto?> HandleAsync(GetExerciseQuery query, CancellationToken ct = default)
    {
        var item = await _db.Exercises.FirstOrDefaultAsync(a => a.Id == query.Id, ct);
        if (item is null) return null;

        var links = await _db.ExerciseResourceLinks.Where(l => l.ExerciseId == item.Id).ToListAsync(ct);
        return ExerciseMappers.ToDto(item, links);
    }
}
