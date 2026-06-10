using System.Text.Json;
using LinguaCoach.Application.Sessions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Sessions;

/// <summary>
/// EF Core implementation of IExercisePatternRepository.
/// Patterns are seeded at startup and read-only at runtime.
/// GetByKind filters in-memory after loading active patterns — the table is small (O(10) rows).
/// </summary>
public sealed class ExercisePatternRepository : IExercisePatternRepository
{
    private readonly LinguaCoachDbContext _db;

    public ExercisePatternRepository(LinguaCoachDbContext db) => _db = db;

    public async Task<ExercisePatternDefinition?> GetByKeyAsync(
        string key, CancellationToken ct = default)
        => await _db.ExercisePatterns
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Key == key && p.IsActive, ct);

    public async Task<IReadOnlyList<ExercisePatternDefinition>> GetAllActiveAsync(
        CancellationToken ct = default)
        => await _db.ExercisePatterns
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Key)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ExercisePatternDefinition>> GetByKindAsync(
        ExerciseKind kind, CancellationToken ct = default)
    {
        var kindInt = (int)kind;
        var all = await GetAllActiveAsync(ct);

        return all
            .Where(p => PatternCompatibleWithKind(p.CompatibleKindsJson, kindInt))
            .ToList();
    }

    private static bool PatternCompatibleWithKind(string compatibleKindsJson, int kindInt)
    {
        try
        {
            var kinds = JsonSerializer.Deserialize<int[]>(compatibleKindsJson);
            return kinds?.Contains(kindInt) == true;
        }
        catch
        {
            return false;
        }
    }
}
