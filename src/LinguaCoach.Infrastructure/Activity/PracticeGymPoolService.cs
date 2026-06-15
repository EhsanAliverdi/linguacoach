using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Pool lookup over <c>PracticeActivityCache</c> (PatternKey doubles as the
/// exercise type key for pattern-backed exercise types). Reservation is the
/// existing <see cref="PracticeCacheStatus.Assigned"/> status.
/// </summary>
public sealed class PracticeGymPoolService : IPracticeGymPoolService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IExerciseTypeRegistry _registry;
    private readonly ILogger<PracticeGymPoolService> _logger;

    public PracticeGymPoolService(
        LinguaCoachDbContext db,
        IExerciseTypeRegistry registry,
        ILogger<PracticeGymPoolService> logger)
    {
        _db = db;
        _registry = registry;
        _logger = logger;
    }

    public async Task<PracticeGymPoolItemDto?> FindReadyForExerciseTypeAsync(
        Guid studentProfileId, string exerciseTypeKey, CancellationToken ct = default)
    {
        var entry = await _registry.GetByKeyAsync(exerciseTypeKey, ct);
        if (entry is null
            || !entry.IsEnabled
            || !entry.ImplementationStatus.Equals("ready", StringComparison.OrdinalIgnoreCase)
            || !entry.SupportsPracticeGym
            || string.IsNullOrWhiteSpace(entry.ExercisePatternKey))
            return null;

        return await ReserveReadyItemAsync(studentProfileId, entry.ExercisePatternKey, entry.Key, entry.PrimarySkill, ct);
    }

    public async Task<PracticeGymPoolItemDto?> FindReadyForSkillAsync(
        Guid studentProfileId, string primarySkill, CancellationToken ct = default)
    {
        var eligible = await _registry.GetEligibleExerciseTypesForSkillAsync(
            primarySkill, ExerciseTypeSupportContext.PracticeGym, ct);

        foreach (var entry in eligible)
        {
            if (string.IsNullOrWhiteSpace(entry.ExercisePatternKey))
                continue;

            var item = await ReserveReadyItemAsync(studentProfileId, entry.ExercisePatternKey, entry.Key, entry.PrimarySkill, ct);
            if (item is not null)
                return item;
        }

        return null;
    }

    public async Task MarkConsumedAsync(Guid poolItemId, CancellationToken ct = default)
    {
        var cache = await _db.PracticeActivityCache.FirstOrDefaultAsync(c => c.Id == poolItemId, ct);
        if (cache is null) return;

        cache.MarkCompleted();
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid poolItemId, string reason, CancellationToken ct = default)
    {
        var cache = await _db.PracticeActivityCache.FirstOrDefaultAsync(c => c.Id == poolItemId, ct);
        if (cache is null) return;

        cache.MarkFailed();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "PracticeGymPoolService: pool item {PoolItemId} marked failed. Reason={Reason}",
            poolItemId, reason);
    }

    private async Task<PracticeGymPoolItemDto?> ReserveReadyItemAsync(
        Guid studentProfileId, string patternKey, string exerciseTypeKey, string primarySkill, CancellationToken ct)
    {
        var excludedIds = new HashSet<Guid>();

        while (true)
        {
            var cache = await _db.PracticeActivityCache
                .Where(c => c.StudentProfileId == studentProfileId
                         && c.PatternKey == patternKey
                         && c.Status == PracticeCacheStatus.Ready
                         && c.LearningActivityId.HasValue
                         && !excludedIds.Contains(c.Id)
                         && (c.ExpiresAtUtc == null || c.ExpiresAtUtc > DateTime.UtcNow))
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (cache is null)
                return null;

            var activityExists = await _db.LearningActivities
                .AnyAsync(a => a.Id == cache.LearningActivityId!.Value && a.IsActive, ct);
            if (!activityExists)
            {
                cache.MarkExpired();
                await _db.SaveChangesAsync(ct);
                excludedIds.Add(cache.Id);
                continue;
            }

            cache.MarkAssigned();
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.Entry(cache).State = EntityState.Detached;
                excludedIds.Add(cache.Id);
                continue;
            }

            return new PracticeGymPoolItemDto(cache.Id, cache.LearningActivityId!.Value, exerciseTypeKey, primarySkill);
        }
    }
}
