using LinguaCoach.Application.Modules;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Modules;

/// <summary>
/// Phase K6 — archive/unarchive one or more <see cref="Domain.Entities.Module"/> rows.
/// Continue-on-error per id, mirroring <see cref="ResourceImport.ResourceBankArchiveHandler"/>.
/// Never cascades to linked Lessons/Exercises — it only hides this Module row.
/// </summary>
public sealed class ModuleArchiveHandler : IModuleArchiveHandler
{
    private readonly LinguaCoachDbContext _db;

    public ModuleArchiveHandler(LinguaCoachDbContext db) => _db = db;

    public Task<ModuleArchiveResult> ArchiveAsync(ArchiveModulesCommand command, CancellationToken ct = default) =>
        ApplyAsync(command.Ids, archive: true, ct);

    public Task<ModuleArchiveResult> UnarchiveAsync(UnarchiveModulesCommand command, CancellationToken ct = default) =>
        ApplyAsync(command.Ids, archive: false, ct);

    private async Task<ModuleArchiveResult> ApplyAsync(IReadOnlyList<Guid> ids, bool archive, CancellationToken ct)
    {
        var distinctIds = ids.Distinct().ToList();
        var entities = await _db.Modules.Where(e => distinctIds.Contains(e.Id)).ToListAsync(ct);
        var found = entities.ToDictionary(e => e.Id);

        var items = new List<ModuleArchiveItemResult>();
        var succeeded = 0;
        var failed = 0;

        foreach (var id in distinctIds)
        {
            if (!found.TryGetValue(id, out var entity))
            {
                items.Add(new ModuleArchiveItemResult(id, false, "Module not found."));
                failed++;
                continue;
            }

            if (archive) entity.Archive(); else entity.Unarchive();
            items.Add(new ModuleArchiveItemResult(id, true, null));
            succeeded++;
        }

        await _db.SaveChangesAsync(ct);

        return new ModuleArchiveResult(distinctIds.Count, succeeded, failed, items);
    }
}
