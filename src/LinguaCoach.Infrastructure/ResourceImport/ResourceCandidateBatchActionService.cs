using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase K2 — batch approve/publish over an explicit set of candidate ids. Deliberately bounded
/// per call (see <see cref="MaxBatchSize"/>) and continue-on-error per item, mirroring
/// <see cref="ResourceCandidateBatchAnalysisService"/>'s discipline: one candidate's failure must
/// never abort the rest of the batch, and one HTTP call must never be asked to process an
/// unbounded number of rows. Delegates every actual mutation to the same single-candidate
/// handlers/services the row-level UI actions use (<see cref="IAdminResourceCandidateApproveHandler"/>,
/// <see cref="IResourceCandidatePublishService"/>) — no gate logic is duplicated here.
/// </summary>
public sealed class ResourceCandidateBatchActionService : IResourceCandidateBatchActionService
{
    // Matches ListAdminResourceCandidatesQuery's own pageSize clamp (200) — a batch action is
    // meant to sweep "the current filtered page," so the cap lines up with what one list call can
    // already return in one page.
    public const int MaxBatchSize = 200;

    private readonly LinguaCoachDbContext _db;
    private readonly IAdminResourceCandidateApproveHandler _approveHandler;
    private readonly IResourceCandidatePublishService _publishService;

    public ResourceCandidateBatchActionService(
        LinguaCoachDbContext db,
        IAdminResourceCandidateApproveHandler approveHandler,
        IResourceCandidatePublishService publishService)
    {
        _db = db;
        _approveHandler = approveHandler;
        _publishService = publishService;
    }

    public async Task<BatchResourceCandidateActionResult> ApproveAsync(
        BatchApproveResourceCandidatesCommand command, CancellationToken ct = default)
    {
        var (ids, limitReached) = Bound(command.CandidateIds);
        var items = new List<BatchResourceCandidateActionItemResult>();
        var succeeded = 0;
        var failed = 0;

        foreach (var id in ids)
        {
            try
            {
                await _approveHandler.HandleAsync(new ApproveResourceCandidateCommand(id, command.Notes), ct);
                items.Add(new BatchResourceCandidateActionItemResult(id, true, null));
                succeeded++;
            }
            catch (ResourceImportValidationException ex)
            {
                items.Add(new BatchResourceCandidateActionItemResult(id, false, ex.Message));
                failed++;
            }
        }

        return new BatchResourceCandidateActionResult(ids.Count, succeeded, failed, AlreadyPublishedCount: 0, limitReached, items);
    }

    public async Task<BatchResourceCandidateActionResult> PublishAsync(
        BatchPublishResourceCandidatesCommand command, Guid? publishedByUserId, CancellationToken ct = default)
    {
        var (ids, limitReached) = Bound(command.CandidateIds);
        return await PublishManyAsync(ids, limitReached, publishedByUserId, ct);
    }

    public async Task<BatchResourceCandidateActionResult> ApproveAndPublishAsync(
        BatchApproveAndPublishResourceCandidatesCommand command, Guid? publishedByUserId, CancellationToken ct = default)
    {
        var (ids, limitReached) = Bound(command.CandidateIds);

        // Approve first (idempotent no-op if already Approved), continue-on-error per id — a
        // candidate that fails to approve (e.g. not found) is still attempted for publish below so
        // its failure reason comes from the more specific publish gate rather than being silently
        // dropped, matching AdminResourceCandidateController.ApproveAndPublish's single-item shape.
        foreach (var id in ids)
        {
            try { await _approveHandler.HandleAsync(new ApproveResourceCandidateCommand(id, command.Notes), ct); }
            catch (ResourceImportValidationException) { /* surfaced by the publish attempt below */ }
        }

        return await PublishManyAsync(ids, limitReached, publishedByUserId, ct);
    }

    private async Task<BatchResourceCandidateActionResult> PublishManyAsync(
        IReadOnlyList<Guid> ids, bool limitReached, Guid? publishedByUserId, CancellationToken ct)
    {
        var alreadyPublished = await _db.ResourceCandidates
            .Where(c => ids.Contains(c.Id) && c.IsPublished)
            .Select(c => c.Id)
            .ToListAsync(ct);
        var alreadyPublishedSet = alreadyPublished.ToHashSet();

        var items = new List<BatchResourceCandidateActionItemResult>();
        var succeeded = 0;
        var failed = 0;

        foreach (var id in ids)
        {
            if (alreadyPublishedSet.Contains(id))
            {
                items.Add(new BatchResourceCandidateActionItemResult(id, true, null));
                continue;
            }

            try
            {
                var result = await _publishService.PublishAsync(id, publishedByUserId, ct);
                items.Add(new BatchResourceCandidateActionItemResult(
                    id, result.Success, result.Success ? null : string.Join("; ", result.Errors)));
                if (result.Success) succeeded++; else failed++;
            }
            catch (ResourceImportValidationException ex)
            {
                items.Add(new BatchResourceCandidateActionItemResult(id, false, ex.Message));
                failed++;
            }
        }

        return new BatchResourceCandidateActionResult(
            ids.Count, succeeded, failed, alreadyPublishedSet.Count, limitReached, items);
    }

    private static (IReadOnlyList<Guid> Ids, bool LimitReached) Bound(IReadOnlyList<Guid> candidateIds)
    {
        var distinct = candidateIds.Distinct().ToList();
        return distinct.Count > MaxBatchSize
            ? (distinct.Take(MaxBatchSize).ToList(), true)
            : (distinct, false);
    }
}
