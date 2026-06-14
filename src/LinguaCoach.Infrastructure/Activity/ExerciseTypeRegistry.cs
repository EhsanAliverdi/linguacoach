using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Activity;

public sealed class ExerciseTypeRegistry : IExerciseTypeRegistry
{
    private readonly LinguaCoachDbContext _db;

    public ExerciseTypeRegistry(LinguaCoachDbContext db) => _db = db;

    public static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        return key.Trim().Replace('-', '_').Replace(' ', '_').ToLowerInvariant();
    }

    public async Task<ExerciseTypeRegistryEntry?> GetByKeyAsync(string exerciseTypeKey, CancellationToken ct = default)
    {
        var key = NormalizeKey(exerciseTypeKey);
        var item = await _db.ExerciseTypeDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == key, ct);
        return item is null ? null : ToEntry(item);
    }

    public async Task<IReadOnlyList<ExerciseTypeRegistryEntry>> GetGenerationEligibleAsync(CancellationToken ct = default) =>
        await ToEntriesAsync(QueryReady(), ct);

    public async Task<IReadOnlyList<ExerciseTypeRegistryEntry>> GetByPrimarySkillAsync(string primarySkill, CancellationToken ct = default)
    {
        var skill = NormalizeSkill(primarySkill);
        return await ToEntriesAsync(_db.ExerciseTypeDefinitions.AsNoTracking()
            .Where(e => e.PrimarySkill == skill)
            .OrderBy(e => e.DisplayName), ct);
    }

    public async Task<IReadOnlyList<ExerciseTypeRegistryEntry>> GetForPracticeGymAsync(CancellationToken ct = default) =>
        await ToEntriesAsync(QueryReady().Where(e => e.SupportsPracticeGym), ct);

    public async Task<IReadOnlyList<ExerciseTypeRegistryEntry>> GetForTodayAsync(CancellationToken ct = default) =>
        await ToEntriesAsync(QueryReady().Where(e => e.SupportsTodayLesson), ct);

    public async Task<IReadOnlyList<ExerciseTypeRegistryEntry>> GetEligibleExerciseTypesForSkillAsync(
        string primarySkill,
        ExerciseTypeSupportContext supportContext = ExerciseTypeSupportContext.Any,
        CancellationToken ct = default)
    {
        var skill = NormalizeSkill(primarySkill);
        var query = QueryReady().Where(e => e.PrimarySkill == skill);
        query = supportContext switch
        {
            ExerciseTypeSupportContext.PracticeGym => query.Where(e => e.SupportsPracticeGym),
            ExerciseTypeSupportContext.Today => query.Where(e => e.SupportsTodayLesson),
            _ => query
        };
        return await ToEntriesAsync(query, ct);
    }

    public async Task<string?> ResolveRendererKeyAsync(string exerciseTypeKey, CancellationToken ct = default) =>
        (await GetByKeyAsync(exerciseTypeKey, ct))?.RendererKey;

    public async Task<string?> ResolveEvaluatorKeyAsync(string exerciseTypeKey, CancellationToken ct = default) =>
        (await GetByKeyAsync(exerciseTypeKey, ct))?.EvaluatorKey;

    public async Task<string?> ResolveGenerationPromptKeyAsync(string exerciseTypeKey, CancellationToken ct = default) =>
        (await GetByKeyAsync(exerciseTypeKey, ct))?.GenerationPromptKey;

    public async Task<ActivityType?> ResolveLegacyActivityTypeAsync(string exerciseTypeKey, CancellationToken ct = default) =>
        (await GetByKeyAsync(exerciseTypeKey, ct))?.LegacyActivityType;

    public async Task<string?> ResolveExercisePatternKeyAsync(string exerciseTypeKey, CancellationToken ct = default) =>
        (await GetByKeyAsync(exerciseTypeKey, ct))?.ExercisePatternKey;

    private IQueryable<ExerciseTypeDefinition> QueryReady() =>
        _db.ExerciseTypeDefinitions.AsNoTracking()
            .Where(e => e.IsEnabled && e.ImplementationStatus == "ready")
            .OrderBy(e => e.DisplayName);

    private static string NormalizeSkill(string skill) =>
        string.IsNullOrWhiteSpace(skill) ? string.Empty : skill.Trim().ToLowerInvariant();

    private static ExerciseTypeRegistryEntry ToEntry(ExerciseTypeDefinition e) => new(
        e.Key,
        e.DisplayName,
        e.Description,
        e.PrimarySkill,
        LinguaCoach.Infrastructure.Admin.ExerciseTypeCatalogService.ParseSkillsForRegistry(e.SecondarySkillsJson),
        e.Category,
        e.IsEnabled,
        e.ImplementationStatus,
        e.IsAvailableForGeneration,
        e.RendererKey,
        e.EvaluatorKey,
        e.GenerationPromptKey,
        e.LegacyActivityType,
        e.ExercisePatternKey,
        e.EstimatedDurationMinutes,
        e.RequiresAudio,
        e.RequiresImage,
        e.SupportsPracticeGym,
        e.SupportsTodayLesson);

    private static async Task<IReadOnlyList<ExerciseTypeRegistryEntry>> ToEntriesAsync(
        IQueryable<ExerciseTypeDefinition> query,
        CancellationToken ct) => (await query.ToListAsync(ct)).Select(ToEntry).ToList();
}
