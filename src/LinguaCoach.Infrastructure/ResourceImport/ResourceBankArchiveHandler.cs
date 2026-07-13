using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase K3 — archive/unarchive one or more <see cref="Domain.Entities.ResourceBankItem"/> rows.
/// Continue-on-error per id (a missing id is reported as a per-item failure, never aborts the
/// rest of the batch) — same discipline as <see cref="ResourceCandidateBatchActionService"/>.
/// </summary>
public sealed class ResourceBankArchiveHandler : IResourceBankArchiveHandler
{
    private readonly LinguaCoachDbContext _db;

    public ResourceBankArchiveHandler(LinguaCoachDbContext db) => _db = db;

    public Task<ResourceBankArchiveResult> ArchiveAsync(ArchiveResourceBankItemsCommand command, CancellationToken ct = default) =>
        ApplyAsync(command.Ids, archive: true, ct);

    public Task<ResourceBankArchiveResult> UnarchiveAsync(UnarchiveResourceBankItemsCommand command, CancellationToken ct = default) =>
        ApplyAsync(command.Ids, archive: false, ct);

    private async Task<ResourceBankArchiveResult> ApplyAsync(IReadOnlyList<Guid> ids, bool archive, CancellationToken ct)
    {
        var distinctIds = ids.Distinct().ToList();
        var entities = await _db.ResourceBankItems.Where(e => distinctIds.Contains(e.Id)).ToListAsync(ct);
        var found = entities.ToDictionary(e => e.Id);

        var items = new List<ResourceBankArchiveItemResult>();
        var succeeded = 0;
        var failed = 0;

        foreach (var id in distinctIds)
        {
            if (!found.TryGetValue(id, out var entity))
            {
                items.Add(new ResourceBankArchiveItemResult(id, false, "Resource Bank item not found."));
                failed++;
                continue;
            }

            if (archive) entity.Archive(); else entity.Unarchive();
            items.Add(new ResourceBankArchiveItemResult(id, true, null));
            succeeded++;
        }

        await _db.SaveChangesAsync(ct);

        return new ResourceBankArchiveResult(distinctIds.Count, succeeded, failed, items);
    }
}
