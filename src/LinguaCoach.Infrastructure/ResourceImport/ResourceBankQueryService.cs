using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase E5 — read-only browse/search over the three published Cefr* bank tables
/// (<see cref="CefrVocabularyEntry"/>/<see cref="CefrGrammarProfileEntry"/>/
/// <see cref="CefrReadingReference"/>). Only rows Phase E4's <see cref="ResourceCandidatePublishService"/>
/// actually wrote can ever appear here — nothing else in this codebase writes to those tables, so
/// no additional "is this row approved" filter is needed; querying the table directly is by
/// construction "published items only".
///
/// None of the three bank entities carries a forward reference to the <see cref="ResourceCandidate"/>
/// that produced it, so detail-view traceability is a reverse lookup: find the
/// <see cref="ResourceCandidate"/> whose <see cref="ResourceCandidate.PublishedEntityType"/>/
/// <see cref="ResourceCandidate.PublishedEntityId"/> match the bank row being viewed (the same
/// fields <see cref="ResourceCandidatePublishService.PublishAsync"/> set via
/// <see cref="ResourceCandidate.MarkPublished"/>). A bank row with no matching candidate (e.g.
/// seeded directly, bypassing the publish workflow) returns
/// <see cref="ResourceBankTraceabilityDto.Unavailable"/> rather than throwing.
///
/// Plain filter + sort only, per this phase's explicit scope — no relevance ranking, no
/// embeddings. Sort is newest-first by <c>BaseEntity.CreatedAt</c> (all three bank entities and
/// ResourceCandidate inherit this) — there is no dedicated "published at" timestamp on the bank
/// rows themselves (only ResourceCandidate.PublishedAtUtc, surfaced via Traceability), so CreatedAt
/// is the closest available proxy and is a documented choice, not an oversight.
/// </summary>
public sealed class ResourceBankQueryService : IResourceBankQueryService
{
    // Matches AdminResourceCandidateListQueryHandler's page-size cap for consistency.
    private const int MaxPageSize = 200;

    private readonly LinguaCoachDbContext _db;

    public ResourceBankQueryService(LinguaCoachDbContext db)
    {
        _db = db;
    }

    // ── Vocabulary ──────────────────────────────────────────────────────────────

    public async Task<ResourceBankVocabularyListResult> ListVocabularyAsync(
        ResourceBankListFilter filter, CancellationToken ct = default)
    {
        var (page, pageSize) = NormalizePaging(filter);

        var query =
            from e in _db.CefrVocabularyEntries
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            select new { Entry = e, Source = s };

        if (filter.SourceId.HasValue)
            query = query.Where(x => x.Entry.SourceId == filter.SourceId.Value);

        if (!string.IsNullOrWhiteSpace(filter.CefrLevel))
            query = query.Where(x => x.Entry.CefrLevel == filter.CefrLevel);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Entry.Word.ToLower().Contains(search)
                || (x.Entry.Notes != null && x.Entry.Notes.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(ct);

        var page_ = await query
            .OrderByDescending(x => x.Entry.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = page_
            .Select(x => new ResourceBankVocabularyListItemDto(
                x.Entry.Id, x.Entry.Word, x.Entry.CefrLevel, x.Entry.PartOfSpeech, x.Entry.Notes,
                x.Source.Id, x.Source.Name, x.Entry.CreatedAt))
            .ToList();

        return new ResourceBankVocabularyListResult(items, totalCount);
    }

    public async Task<ResourceBankVocabularyDetailDto?> GetVocabularyDetailAsync(Guid id, CancellationToken ct = default)
    {
        var loaded = await (
            from e in _db.CefrVocabularyEntries
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            where e.Id == id
            select new { Entry = e, Source = s })
            .FirstOrDefaultAsync(ct);

        if (loaded is null)
            return null;

        var traceability = await BuildTraceabilityAsync(nameof(CefrVocabularyEntry), id, ct);

        return new ResourceBankVocabularyDetailDto(
            loaded.Entry.Id, loaded.Entry.Word, loaded.Entry.CefrLevel, loaded.Entry.PartOfSpeech,
            loaded.Entry.Notes, loaded.Entry.CreatedAt, ToSourceInfoDto(loaded.Source), traceability);
    }

    // ── Grammar ─────────────────────────────────────────────────────────────────

    public async Task<ResourceBankGrammarListResult> ListGrammarAsync(
        ResourceBankListFilter filter, CancellationToken ct = default)
    {
        var (page, pageSize) = NormalizePaging(filter);

        var query =
            from e in _db.CefrGrammarProfileEntries
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            select new { Entry = e, Source = s };

        if (filter.SourceId.HasValue)
            query = query.Where(x => x.Entry.SourceId == filter.SourceId.Value);

        if (!string.IsNullOrWhiteSpace(filter.CefrLevel))
            query = query.Where(x => x.Entry.CefrLevel == filter.CefrLevel);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Entry.GrammarPoint.ToLower().Contains(search)
                || (x.Entry.Description != null && x.Entry.Description.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(ct);

        var page_ = await query
            .OrderByDescending(x => x.Entry.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = page_
            .Select(x => new ResourceBankGrammarListItemDto(
                x.Entry.Id, x.Entry.GrammarPoint, x.Entry.CefrLevel, x.Entry.Description,
                x.Source.Id, x.Source.Name, x.Entry.CreatedAt))
            .ToList();

        return new ResourceBankGrammarListResult(items, totalCount);
    }

    public async Task<ResourceBankGrammarDetailDto?> GetGrammarDetailAsync(Guid id, CancellationToken ct = default)
    {
        var loaded = await (
            from e in _db.CefrGrammarProfileEntries
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            where e.Id == id
            select new { Entry = e, Source = s })
            .FirstOrDefaultAsync(ct);

        if (loaded is null)
            return null;

        var traceability = await BuildTraceabilityAsync(nameof(CefrGrammarProfileEntry), id, ct);

        return new ResourceBankGrammarDetailDto(
            loaded.Entry.Id, loaded.Entry.GrammarPoint, loaded.Entry.CefrLevel, loaded.Entry.Description,
            loaded.Entry.CreatedAt, ToSourceInfoDto(loaded.Source), traceability);
    }

    // ── Reading references ─────────────────────────────────────────────────────

    public async Task<ResourceBankReadingReferenceListResult> ListReadingReferencesAsync(
        ResourceBankListFilter filter, CancellationToken ct = default)
    {
        var (page, pageSize) = NormalizePaging(filter);

        var query =
            from e in _db.CefrReadingReferences
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            select new { Entry = e, Source = s };

        if (filter.SourceId.HasValue)
            query = query.Where(x => x.Entry.SourceId == filter.SourceId.Value);

        if (!string.IsNullOrWhiteSpace(filter.CefrLevel))
            query = query.Where(x => x.Entry.CefrLevel == filter.CefrLevel);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.Entry.TextType != null && x.Entry.TextType.ToLower().Contains(search))
                || (x.Entry.DifficultyNotes != null && x.Entry.DifficultyNotes.ToLower().Contains(search))
                || (x.Entry.ReferenceExcerpt != null && x.Entry.ReferenceExcerpt.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(ct);

        var page_ = await query
            .OrderByDescending(x => x.Entry.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = page_
            .Select(x => new ResourceBankReadingReferenceListItemDto(
                x.Entry.Id, x.Entry.CefrLevel, x.Entry.TextType, x.Entry.DifficultyNotes, x.Entry.ReferenceExcerpt,
                x.Source.Id, x.Source.Name, x.Entry.CreatedAt))
            .ToList();

        return new ResourceBankReadingReferenceListResult(items, totalCount);
    }

    public async Task<ResourceBankReadingReferenceDetailDto?> GetReadingReferenceDetailAsync(Guid id, CancellationToken ct = default)
    {
        var loaded = await (
            from e in _db.CefrReadingReferences
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            where e.Id == id
            select new { Entry = e, Source = s })
            .FirstOrDefaultAsync(ct);

        if (loaded is null)
            return null;

        var traceability = await BuildTraceabilityAsync(nameof(CefrReadingReference), id, ct);

        return new ResourceBankReadingReferenceDetailDto(
            loaded.Entry.Id, loaded.Entry.CefrLevel, loaded.Entry.TextType, loaded.Entry.DifficultyNotes,
            loaded.Entry.ReferenceExcerpt, loaded.Entry.CreatedAt, ToSourceInfoDto(loaded.Source), traceability);
    }

    // ── Reading passages (Phase E7 — full-length passages, distinct from ReadingReference) ────

    public async Task<ResourceBankReadingPassageListResult> ListReadingPassagesAsync(
        ResourceBankListFilter filter, CancellationToken ct = default)
    {
        var (page, pageSize) = NormalizePaging(filter);

        var query =
            from e in _db.CefrReadingPassages
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            select new { Entry = e, Source = s };

        if (filter.SourceId.HasValue)
            query = query.Where(x => x.Entry.SourceId == filter.SourceId.Value);

        if (!string.IsNullOrWhiteSpace(filter.CefrLevel))
            query = query.Where(x => x.Entry.CefrLevel == filter.CefrLevel);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Entry.Title.ToLower().Contains(search)
                || x.Entry.PassageText.ToLower().Contains(search)
                || (x.Entry.Summary != null && x.Entry.Summary.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(ct);

        var page_ = await query
            .OrderByDescending(x => x.Entry.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = page_
            .Select(x => new ResourceBankReadingPassageListItemDto(
                x.Entry.Id, x.Entry.Title, x.Entry.CefrLevel, x.Entry.WordCount, x.Entry.EstimatedReadingMinutes,
                x.Entry.Subskill, x.Source.Id, x.Source.Name, x.Entry.CreatedAt))
            .ToList();

        return new ResourceBankReadingPassageListResult(items, totalCount);
    }

    public async Task<ResourceBankReadingPassageDetailDto?> GetReadingPassageDetailAsync(Guid id, CancellationToken ct = default)
    {
        var loaded = await (
            from e in _db.CefrReadingPassages
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            where e.Id == id
            select new { Entry = e, Source = s })
            .FirstOrDefaultAsync(ct);

        if (loaded is null)
            return null;

        var traceability = await BuildTraceabilityAsync(nameof(CefrReadingPassage), id, ct);

        return new ResourceBankReadingPassageDetailDto(
            loaded.Entry.Id, loaded.Entry.Title, loaded.Entry.PassageText, loaded.Entry.Summary,
            loaded.Entry.CefrLevel, loaded.Entry.DifficultyBand, loaded.Entry.PrimarySkill, loaded.Entry.Subskill,
            ParseJsonStringArray(loaded.Entry.TopicTagsJson), ParseJsonStringArray(loaded.Entry.ContextTagsJson),
            ParseJsonStringArray(loaded.Entry.FocusTagsJson), loaded.Entry.WordCount, loaded.Entry.EstimatedReadingMinutes,
            loaded.Entry.AttributionText, loaded.Entry.QualityScore, loaded.Entry.CreatedAt,
            ToSourceInfoDto(loaded.Source), traceability);
    }

    // ── Shared helpers ──────────────────────────────────────────────────────────

    private static IReadOnlyList<string> ParseJsonStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                return Array.Empty<string>();

            var list = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    list.Add(item.GetString()!);
            }
            return list;
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static (int Page, int PageSize) NormalizePaging(ResourceBankListFilter filter) =>
        (Math.Max(filter.Page, 1), Math.Clamp(filter.PageSize, 1, MaxPageSize));

    private static ResourceCandidateSourceInfoDto ToSourceInfoDto(CefrResourceSource source) => new(
        source.Id, source.Name, source.LicenseType, source.SourceUrl, source.DownloadUrl,
        source.AttributionText, source.AllowsStudentDisplay, source.AllowsCommercialUse);

    /// <summary>Reverse lookup: finds the ResourceCandidate (if any) whose PublishedEntityType/
    /// PublishedEntityId match this bank row, joining through to its ResourceImportRunId the same
    /// way AdminResourceCandidateListQueryHandler/ResourceCandidatePublishService already do.
    /// Never throws when no match exists — returns Unavailable instead.</summary>
    private async Task<ResourceBankTraceabilityDto> BuildTraceabilityAsync(
        string publishedEntityType, Guid publishedEntityId, CancellationToken ct)
    {
        var loaded = await (
            from c in _db.ResourceCandidates
            join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
            join run in _db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
            where c.PublishedEntityType == publishedEntityType && c.PublishedEntityId == publishedEntityId
            select new { Candidate = c, Run = run })
            .FirstOrDefaultAsync(ct);

        if (loaded is null)
            return ResourceBankTraceabilityDto.Unavailable;

        return new ResourceBankTraceabilityDto(
            true, loaded.Candidate.Id, loaded.Run.Id, loaded.Candidate.ContentFingerprint,
            loaded.Candidate.QualityScore, loaded.Candidate.CreatedAt, loaded.Candidate.PublishedAtUtc,
            loaded.Candidate.PublishedByUserId);
    }
}
