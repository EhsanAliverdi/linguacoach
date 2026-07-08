using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase E2 — bounded-batch AI analysis of all not-yet-analyzed candidates for one import run.
/// Deliberately synchronous/batched rather than a background job: E2's scope is a conservative,
/// admin-triggered enrichment step, not a job-queue system. A true background-processed queue
/// for larger runs is Phase E7's job (see roadmap) — this cap exists specifically so an admin
/// can't accidentally trigger an unbounded, slow, expensive AI sweep from one HTTP call.
/// </summary>
public sealed class ResourceCandidateBatchAnalysisService : IResourceCandidateBatchAnalysisService
{
    // Conservative per-call ceiling. Chosen so a single HTTP request completes in a reasonable
    // time even if every candidate needs two AI calls (analysis + one retry) — 50 candidates *
    // up to 2 calls each is already a meaningful number of sequential AI round-trips for one
    // request to wait on. Documented explicitly per the Phase E2 spec's "pick a number" ask.
    public const int MaxCandidatesPerBatch = 50;

    private readonly LinguaCoachDbContext _db;
    private readonly IResourceCandidateAnalysisService _analysisService;
    private readonly IResourceCandidateValidationService _validationService;

    public ResourceCandidateBatchAnalysisService(
        LinguaCoachDbContext db,
        IResourceCandidateAnalysisService analysisService,
        IResourceCandidateValidationService validationService)
    {
        _db = db;
        _analysisService = analysisService;
        _validationService = validationService;
    }

    public async Task<ResourceCandidateBatchAnalysisResult> AnalyzePendingForRunAsync(
        Guid resourceImportRunId, CancellationToken ct = default)
    {
        // "Not yet analyzed" = AiAnalysisJson is still null. Re-analysis of already-analyzed
        // candidates is available one-at-a-time via the single-candidate endpoint; the batch
        // endpoint's job is sweeping up what's never been looked at.
        var candidateIds = await (
            from c in _db.ResourceCandidates
            join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
            where r.ResourceImportRunId == resourceImportRunId && c.AiAnalysisJson == null
            orderby c.CreatedAt
            select c.Id)
            .Take(MaxCandidatesPerBatch + 1) // +1 so we can tell if the true count exceeds the cap
            .ToListAsync(ct);

        var batchLimitReached = candidateIds.Count > MaxCandidatesPerBatch;
        var toProcess = batchLimitReached ? candidateIds.Take(MaxCandidatesPerBatch).ToList() : candidateIds;

        var succeeded = 0;
        var failed = 0;

        foreach (var candidateId in toProcess)
        {
            // Continue-on-error per candidate — matches ResourceImportService's per-row
            // discipline: one candidate's AI failure must never abort the rest of the batch.
            // ResourceCandidateAnalysisService itself already fails gracefully rather than
            // throwing, but this catch is defense-in-depth in case of an unexpected error
            // (e.g. a transient DB issue) elsewhere in the call.
            try
            {
                var result = await _analysisService.AnalyzeAsync(candidateId, ct);
                // Re-validate immediately so ValidationStatus reflects whatever the analysis
                // just wrote (or, on graceful analysis failure, reflects the untouched fields —
                // ValidateAsync is idempotent either way).
                await _validationService.ValidateAsync(candidateId, ct);
                if (result.Success) succeeded++; else failed++;
            }
            catch
            {
                failed++;
            }
        }

        return new ResourceCandidateBatchAnalysisResult(
            CandidatesConsidered: candidateIds.Count,
            CandidatesAnalyzed: toProcess.Count,
            SucceededCount: succeeded,
            FailedCount: failed,
            BatchLimitReached: batchLimitReached);
    }
}
