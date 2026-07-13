using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

public sealed class AdminResourceCandidateListQueryHandler : IAdminResourceCandidateListQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceCandidateListQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceCandidateListResult> HandleAsync(
        ListAdminResourceCandidatesQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        // Join through raw record -> import run -> source so filters on source/run are possible
        // without denormalizing those FKs onto the candidate row.
        var joined =
            from c in _db.ResourceCandidates
            join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
            join run in _db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
            select new { Candidate = c, RawRecord = r, Run = run };

        if (query.ImportRunId.HasValue)
            joined = joined.Where(x => x.Run.Id == query.ImportRunId.Value);

        if (query.SourceId.HasValue)
            joined = joined.Where(x => x.Run.CefrResourceSourceId == query.SourceId.Value);

        if (!string.IsNullOrWhiteSpace(query.CandidateType)
            && Enum.TryParse<ResourceCandidateType>(query.CandidateType, ignoreCase: true, out var candidateType))
            joined = joined.Where(x => x.Candidate.CandidateType == candidateType);

        if (!string.IsNullOrWhiteSpace(query.ValidationStatus)
            && Enum.TryParse<ResourceCandidateValidationStatus>(query.ValidationStatus, ignoreCase: true, out var validationStatus))
            joined = joined.Where(x => x.Candidate.ValidationStatus == validationStatus);

        if (!string.IsNullOrWhiteSpace(query.ReviewStatus)
            && Enum.TryParse<AdminReviewStatus>(query.ReviewStatus, ignoreCase: true, out var reviewStatus))
            joined = joined.Where(x => x.Candidate.ReviewStatus == reviewStatus);

        if (!string.IsNullOrWhiteSpace(query.LanguageCode))
            joined = joined.Where(x => x.Candidate.LanguageCode == query.LanguageCode.Trim().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(query.CefrLevel))
            joined = joined.Where(x => x.Candidate.CefrLevel == query.CefrLevel);

        if (!string.IsNullOrWhiteSpace(query.Search))
            joined = joined.Where(x => x.Candidate.SearchText.Contains(query.Search.Trim().ToLowerInvariant()));

        if (query.PublishableOnly == true)
        {
            joined = joined.Where(x => !x.Candidate.IsPublished
                && (x.Candidate.ValidationStatus == ResourceCandidateValidationStatus.Passed
                    || x.Candidate.ValidationStatus == ResourceCandidateValidationStatus.NeedsReview));
        }

        var totalCount = await joined.CountAsync(ct);
        var overallTotalCount = await _db.ResourceCandidates.CountAsync(ct);

        var page_ = await joined
            .OrderByDescending(x => x.Candidate.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = page_
            .Select(x => ResourceImportMappers.ToDto(x.Candidate, x.Run.Id, x.Run.CefrResourceSourceId))
            .ToList();

        return new AdminResourceCandidateListResult(items, totalCount, overallTotalCount);
    }
}

/// <summary>Phase K2 — review-state summary powering the Import Content page's headline counts
/// (see AdminResourceCandidateReviewSummaryDto's doc comment for why Passed/NeedsReview/Blocked
/// are reported separately).</summary>
public sealed class AdminResourceCandidateReviewSummaryQueryHandler : IAdminResourceCandidateReviewSummaryQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceCandidateReviewSummaryQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceCandidateReviewSummaryDto> HandleAsync(
        GetAdminResourceCandidateReviewSummaryQuery query, CancellationToken ct = default)
    {
        var candidates =
            from c in _db.ResourceCandidates
            join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
            join run in _db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
            select new { Candidate = c, Run = run };

        if (query.ImportRunId.HasValue)
            candidates = candidates.Where(x => x.Run.Id == query.ImportRunId.Value);

        if (query.SourceId.HasValue)
            candidates = candidates.Where(x => x.Run.CefrResourceSourceId == query.SourceId.Value);

        var rows = await candidates
            .Select(x => new { x.Candidate.IsPublished, x.Candidate.ValidationStatus })
            .ToListAsync(ct);

        var publishedCount = rows.Count(r => r.IsPublished);
        var passedCount = rows.Count(r => !r.IsPublished && r.ValidationStatus == ResourceCandidateValidationStatus.Passed);
        var needsReviewCount = rows.Count(r => !r.IsPublished && r.ValidationStatus == ResourceCandidateValidationStatus.NeedsReview);
        var blockedCount = rows.Count(r => !r.IsPublished
            && r.ValidationStatus is ResourceCandidateValidationStatus.Failed or ResourceCandidateValidationStatus.Pending);

        return new AdminResourceCandidateReviewSummaryDto(
            TotalCount: rows.Count,
            PublishedCount: publishedCount,
            PassedCount: passedCount,
            NeedsReviewCount: needsReviewCount,
            BlockedCount: blockedCount,
            PublishableCount: passedCount + needsReviewCount);
    }
}

public sealed class AdminResourceCandidateGetQueryHandler : IAdminResourceCandidateGetQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceCandidateGetQueryHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceCandidateDto?> HandleAsync(GetAdminResourceCandidateQuery query, CancellationToken ct = default)
    {
        var result =
            await (from c in _db.ResourceCandidates
                   join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
                   join run in _db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
                   where c.Id == query.CandidateId
                   select new { Candidate = c, Run = run })
                .FirstOrDefaultAsync(ct);

        return result is null
            ? null
            : ResourceImportMappers.ToDto(result.Candidate, result.Run.Id, result.Run.CefrResourceSourceId);
    }
}

public sealed class AdminResourceCandidateNotesHandler : IAdminResourceCandidateNotesHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceCandidateNotesHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceCandidateDto> HandleAsync(
        SetResourceCandidateAdminNotesCommand command, CancellationToken ct = default)
    {
        var result =
            await (from c in _db.ResourceCandidates
                   join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
                   join run in _db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
                   where c.Id == command.CandidateId
                   select new { Candidate = c, Run = run })
                .FirstOrDefaultAsync(ct)
            ?? throw new ResourceImportValidationException($"Resource candidate '{command.CandidateId}' was not found.");

        result.Candidate.SetAdminNotes(command.AdminNotes);
        await _db.SaveChangesAsync(ct);

        return ResourceImportMappers.ToDto(result.Candidate, result.Run.Id, result.Run.CefrResourceSourceId);
    }
}

/// <summary>Phase E4 — admin approval step, separate from ValidationStatus (deterministic) and
/// from IResourceCandidatePublishService (which never runs here).</summary>
public sealed class AdminResourceCandidateApproveHandler : IAdminResourceCandidateApproveHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceCandidateApproveHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceCandidateDto> HandleAsync(
        ApproveResourceCandidateCommand command, CancellationToken ct = default)
    {
        var result =
            await (from c in _db.ResourceCandidates
                   join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
                   join run in _db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
                   where c.Id == command.CandidateId
                   select new { Candidate = c, Run = run })
                .FirstOrDefaultAsync(ct)
            ?? throw new ResourceImportValidationException($"Resource candidate '{command.CandidateId}' was not found.");

        result.Candidate.Approve(command.Notes);
        await _db.SaveChangesAsync(ct);

        return ResourceImportMappers.ToDto(result.Candidate, result.Run.Id, result.Run.CefrResourceSourceId);
    }
}

/// <summary>Phase E4 — admin rejection. Blocked outright for an already-published candidate (see
/// ResourceCandidate.Reject's doc comment) — surfaced to the caller as a
/// ResourceImportValidationException so the controller can return 400 rather than 500.</summary>
public sealed class AdminResourceCandidateRejectHandler : IAdminResourceCandidateRejectHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminResourceCandidateRejectHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminResourceCandidateDto> HandleAsync(
        RejectResourceCandidateCommand command, CancellationToken ct = default)
    {
        var result =
            await (from c in _db.ResourceCandidates
                   join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
                   join run in _db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
                   where c.Id == command.CandidateId
                   select new { Candidate = c, Run = run })
                .FirstOrDefaultAsync(ct)
            ?? throw new ResourceImportValidationException($"Resource candidate '{command.CandidateId}' was not found.");

        try
        {
            result.Candidate.Reject(command.Reason);
        }
        catch (ArgumentException ex)
        {
            throw new ResourceImportValidationException(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            throw new ResourceImportValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        return ResourceImportMappers.ToDto(result.Candidate, result.Run.Id, result.Run.CefrResourceSourceId);
    }
}
