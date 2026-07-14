using LinguaCoach.Application.Lessons;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Lessons;

/// <summary>
/// Phase K6 — archive/unarchive one or more <see cref="Domain.Entities.Lesson"/> rows.
/// Continue-on-error per id, mirroring <see cref="ResourceImport.ResourceBankArchiveHandler"/>.
/// </summary>
public sealed class LessonArchiveHandler : ILessonArchiveHandler
{
    private readonly LinguaCoachDbContext _db;

    public LessonArchiveHandler(LinguaCoachDbContext db) => _db = db;

    public Task<LessonArchiveResult> ArchiveAsync(ArchiveLessonsCommand command, CancellationToken ct = default) =>
        ApplyAsync(command.Ids, archive: true, ct);

    public Task<LessonArchiveResult> UnarchiveAsync(UnarchiveLessonsCommand command, CancellationToken ct = default) =>
        ApplyAsync(command.Ids, archive: false, ct);

    private async Task<LessonArchiveResult> ApplyAsync(IReadOnlyList<Guid> ids, bool archive, CancellationToken ct)
    {
        var distinctIds = ids.Distinct().ToList();
        var entities = await _db.Lessons.Where(e => distinctIds.Contains(e.Id)).ToListAsync(ct);
        var found = entities.ToDictionary(e => e.Id);

        var items = new List<LessonArchiveItemResult>();
        var succeeded = 0;
        var failed = 0;

        foreach (var id in distinctIds)
        {
            if (!found.TryGetValue(id, out var entity))
            {
                items.Add(new LessonArchiveItemResult(id, false, "Lesson not found."));
                failed++;
                continue;
            }

            if (archive) entity.Archive(); else entity.Unarchive();
            items.Add(new LessonArchiveItemResult(id, true, null));
            succeeded++;
        }

        await _db.SaveChangesAsync(ct);

        return new LessonArchiveResult(distinctIds.Count, succeeded, failed, items);
    }
}
