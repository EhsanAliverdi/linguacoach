using LinguaCoach.Application.Exercises;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Exercises;

/// <summary>
/// Phase K6 — archive/unarchive one or more <see cref="Domain.Entities.Exercise"/> rows.
/// Continue-on-error per id, mirroring <see cref="ResourceImport.ResourceBankArchiveHandler"/>.
/// </summary>
public sealed class ExerciseArchiveHandler : IExerciseArchiveHandler
{
    private readonly LinguaCoachDbContext _db;

    public ExerciseArchiveHandler(LinguaCoachDbContext db) => _db = db;

    public Task<ExerciseArchiveResult> ArchiveAsync(ArchiveExercisesCommand command, CancellationToken ct = default) =>
        ApplyAsync(command.Ids, archive: true, ct);

    public Task<ExerciseArchiveResult> UnarchiveAsync(UnarchiveExercisesCommand command, CancellationToken ct = default) =>
        ApplyAsync(command.Ids, archive: false, ct);

    private async Task<ExerciseArchiveResult> ApplyAsync(IReadOnlyList<Guid> ids, bool archive, CancellationToken ct)
    {
        var distinctIds = ids.Distinct().ToList();
        var entities = await _db.Exercises.Where(e => distinctIds.Contains(e.Id)).ToListAsync(ct);
        var found = entities.ToDictionary(e => e.Id);

        var items = new List<ExerciseArchiveItemResult>();
        var succeeded = 0;
        var failed = 0;

        foreach (var id in distinctIds)
        {
            if (!found.TryGetValue(id, out var entity))
            {
                items.Add(new ExerciseArchiveItemResult(id, false, "Exercise not found."));
                failed++;
                continue;
            }

            if (archive) entity.Archive(); else entity.Unarchive();
            items.Add(new ExerciseArchiveItemResult(id, true, null));
            succeeded++;
        }

        await _db.SaveChangesAsync(ct);

        return new ExerciseArchiveResult(distinctIds.Count, succeeded, failed, items);
    }
}
