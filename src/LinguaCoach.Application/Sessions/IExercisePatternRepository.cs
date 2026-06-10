using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Sessions;

/// <summary>
/// Read-only query interface for ExercisePatternDefinition records.
/// Seeded at startup by ExercisePatternSeeder. Read-only at runtime in MVP.
/// </summary>
public interface IExercisePatternRepository
{
    /// <summary>Returns the pattern definition for the given key, or null if not found.</summary>
    Task<ExercisePatternDefinition?> GetByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>Returns all active pattern definitions.</summary>
    Task<IReadOnlyList<ExercisePatternDefinition>> GetAllActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns all active pattern definitions compatible with the given ExerciseKind.
    /// Compatible means the kind appears in the pattern's CompatibleKindsJson array.
    /// </summary>
    Task<IReadOnlyList<ExercisePatternDefinition>> GetByKindAsync(ExerciseKind kind, CancellationToken ct = default);
}
