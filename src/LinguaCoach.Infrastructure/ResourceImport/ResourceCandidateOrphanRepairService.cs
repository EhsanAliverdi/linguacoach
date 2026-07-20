using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>See <see cref="IResourceCandidateOrphanRepairService"/> for the full rationale.</summary>
public sealed class ResourceCandidateOrphanRepairService : IResourceCandidateOrphanRepairService
{
    // The exact typed-table names Phase I0 dropped. Any candidate whose PublishedEntityType is one
    // of these is unconditionally orphaned today — resource_bank_items has no row of any of these
    // shapes anymore (everything is a single PublishedResourceType-discriminated table now).
    private static readonly string[] DeadPublishedEntityTypes =
        ["CefrGrammarProfileEntry", "CefrReadingReference", "CefrReadingPassage"];

    // The only CandidateTypes that ever mapped to a DeadPublishedEntityTypes value (Vocabulary
    // published straight into the still-live ResourceBankItem table and was never affected).
    // Used to re-find candidates a prior, partial run of this same repair already unstuck (cleared
    // IsPublished/PublishedEntityType) but never successfully republished — those are no longer
    // caught by the IsPublished-based query below, since this repair's own first step is what
    // cleared that flag.
    private static readonly ResourceCandidateType[] AffectedCandidateTypes =
        [ResourceCandidateType.GrammarProfileEntry, ResourceCandidateType.ReadingPassage];

    private readonly LinguaCoachDbContext _db;
    private readonly IResourceCandidatePublishService _publishService;
    private readonly ILogger<ResourceCandidateOrphanRepairService> _logger;

    public ResourceCandidateOrphanRepairService(
        LinguaCoachDbContext db, IResourceCandidatePublishService publishService,
        ILogger<ResourceCandidateOrphanRepairService> logger)
    {
        _db = db;
        _publishService = publishService;
        _logger = logger;
    }

    public async Task<OrphanedPublishRepairResult> RepairOrphanedPublishReferencesAsync(CancellationToken ct = default)
    {
        // Case A — still marked published, pointing at a dead Phase I0 table (never touched by this
        // repair before).
        var stillOrphaned = await _db.ResourceCandidates
            .Where(c => c.IsPublished && c.PublishedEntityType != null
                && DeadPublishedEntityTypes.Contains(c.PublishedEntityType))
            .ToListAsync(ct);

        // Case B — a prior, partial run of this repair already cleared IsPublished/PublishedEntityType
        // (Case A's own first step) but the subsequent republish attempt failed (e.g. the provenance
        // gate, before BackfillMissingImportPackagesAsync existed) — never left "stuck" in the
        // IsPublished sense, but never actually reached the bank either. Re-findable only by
        // CandidateType + admin approval, since the IsPublished-based signal was already cleared.
        var previouslyUnstuckButUnpublished = await _db.ResourceCandidates
            .Where(c => !c.IsPublished && c.PublishedEntityType == null
                && AffectedCandidateTypes.Contains(c.CandidateType)
                && c.ReviewStatus == ResourceCandidateReviewStatus.Approved)
            .ToListAsync(ct);

        var candidates = stillOrphaned
            .Concat(previouslyUnstuckButUnpublished)
            .DistinctBy(c => c.Id)
            .ToList();

        await BackfillMissingImportPackagesAsync(candidates, ct);

        var items = new List<OrphanedPublishRepairItemResult>();

        foreach (var candidate in candidates)
        {
            var candidateTypeName = candidate.CandidateType.ToString();

            // Defensive re-check per RepairOrphanedPublishReference's contract — never repair a
            // reference that actually still resolves to a real bank row.
            var stillResolves = candidate.PublishedEntityId is { } existingId
                && await _db.ResourceBankItems.AsNoTracking().AnyAsync(b => b.Id == existingId, ct);
            if (stillResolves)
            {
                items.Add(new OrphanedPublishRepairItemResult(
                    candidate.Id, candidateTypeName, false, null,
                    "Skipped — PublishedEntityId unexpectedly resolves to a real bank item; not orphaned."));
                continue;
            }

            try
            {
                // Only Case A candidates are still marked published — Case B ones were already
                // cleared by a prior run and calling this again would throw.
                if (candidate.IsPublished)
                {
                    candidate.RepairOrphanedPublishReference();
                    await _db.SaveChangesAsync(ct);
                }

                var publishResult = await _publishService.PublishAsync(candidate.Id, publishedByUserId: null, ct);
                if (publishResult.Success)
                {
                    items.Add(new OrphanedPublishRepairItemResult(
                        candidate.Id, candidateTypeName, true, publishResult.PublishedEntityId, null));
                }
                else
                {
                    items.Add(new OrphanedPublishRepairItemResult(
                        candidate.Id, candidateTypeName, false, null, string.Join("; ", publishResult.Errors)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ResourceCandidateOrphanRepairService: failed to repair candidate {CandidateId}.", candidate.Id);
                items.Add(new OrphanedPublishRepairItemResult(candidate.Id, candidateTypeName, false, null, ex.Message));
            }
        }

        return new OrphanedPublishRepairResult(
            FoundCount: candidates.Count,
            RepairedCount: items.Count(i => i.Repaired),
            FailedCount: items.Count(i => !i.Repaired),
            Items: items);
    }

    /// <summary>Some of these candidates' ResourceImportRuns predate the Phase 4.2 mandatory
    /// publish-provenance gate — they were created by an internal seed-pack seeder before that
    /// seeder started creating its own self-approved ImportPackage/ImportProfile, and because
    /// every seeder in this codebase is idempotent-by-source-name, it never re-ran to pick up the
    /// gate once its source already existed. Without this backfill, PublishAsync's live provenance
    /// check would hard-block every one of them permanently — not because of any real content,
    /// license, or approval problem (the source itself is already internally approved for import/
    /// student-display/commercial-use), but purely because the historical run has no
    /// ImportPackageId. This replicates exactly what InternalResourceSeedPackSeeder /
    /// InternalResourceSeedPackE8Seeder's own SeedApprovedPackageAsync helper creates for freshly
    /// seeded content, so backfilled runs end up provenance-identical to a fresh run.</summary>
    private async Task BackfillMissingImportPackagesAsync(
        IReadOnlyList<ResourceCandidate> candidates, CancellationToken ct)
    {
        var rawRecordIds = candidates.Select(c => c.ResourceRawRecordId).ToList();

        var runsNeedingPackage = await (
            from rrr in _db.ResourceRawRecords
            join run in _db.ResourceImportRuns on rrr.ResourceImportRunId equals run.Id
            where rawRecordIds.Contains(rrr.Id) && run.ImportPackageId == null
            select run)
            .Distinct()
            .ToListAsync(ct);

        if (runsNeedingPackage.Count == 0) return;

        foreach (var sourceGroup in runsNeedingPackage.GroupBy(r => r.CefrResourceSourceId))
        {
            var source = await _db.CefrResourceSources.FirstAsync(s => s.Id == sourceGroup.Key, ct);

            var package = new ImportPackage(source.Id, source.Name, DateTimeOffset.UtcNow);
            _db.ImportPackages.Add(package);
            await _db.SaveChangesAsync(ct);

            var plan = new ImportProfile(
                package.Id, 1, profileJson: "{}", sampleAssetIds: Array.Empty<Guid>(),
                estimatedCandidateCount: sourceGroup.Count(), createdAtUtc: DateTimeOffset.UtcNow);
            plan.SubmitForApproval();
            plan.Approve(approvedByUserId: null, DateTimeOffset.UtcNow, approvedCostCeiling: 0m);
            _db.ImportProfiles.Add(plan);
            package.ApproveProfile(plan.Id);
            await _db.SaveChangesAsync(ct);

            foreach (var run in sourceGroup)
                run.AssignRetroactiveImportPackage(package.Id);

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "ResourceCandidateOrphanRepairService: backfilled ImportPackage {PackageId} for {RunCount} " +
                "pre-Phase-4.2 runs from source '{SourceName}'.",
                package.Id, sourceGroup.Count(), source.Name);
        }
    }
}
