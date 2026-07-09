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

        if (!string.IsNullOrWhiteSpace(filter.Subskill))
        {
            var subskill = filter.Subskill.Trim();
            query = query.Where(x => x.Entry.Subskill == subskill);
        }
        if (filter.DifficultyBand.HasValue)
            query = query.Where(x => x.Entry.DifficultyBand == filter.DifficultyBand.Value);
        if (!string.IsNullOrWhiteSpace(filter.ContextTag))
        {
            var needle = TagNeedle(filter.ContextTag);
            query = query.Where(x => x.Entry.ContextTagsJson != null && x.Entry.ContextTagsJson.ToLower().Contains(needle));
        }
        if (!string.IsNullOrWhiteSpace(filter.FocusTag))
        {
            var needle = TagNeedle(filter.FocusTag);
            query = query.Where(x => x.Entry.FocusTagsJson != null && x.Entry.FocusTagsJson.ToLower().Contains(needle));
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
                x.Source.Id, x.Source.Name, x.Entry.CreatedAt,
                x.Entry.Subskill, x.Entry.DifficultyBand,
                ParseJsonStringArray(x.Entry.ContextTagsJson), ParseJsonStringArray(x.Entry.FocusTagsJson)))
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
            loaded.Entry.Notes, loaded.Entry.CreatedAt, ToSourceInfoDto(loaded.Source), traceability,
            loaded.Entry.Subskill, loaded.Entry.DifficultyBand,
            ParseJsonStringArray(loaded.Entry.ContextTagsJson), ParseJsonStringArray(loaded.Entry.FocusTagsJson));
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

        if (!string.IsNullOrWhiteSpace(filter.Subskill))
        {
            var subskill = filter.Subskill.Trim();
            query = query.Where(x => x.Entry.Subskill == subskill);
        }
        if (filter.DifficultyBand.HasValue)
            query = query.Where(x => x.Entry.DifficultyBand == filter.DifficultyBand.Value);
        if (!string.IsNullOrWhiteSpace(filter.ContextTag))
        {
            var needle = TagNeedle(filter.ContextTag);
            query = query.Where(x => x.Entry.ContextTagsJson != null && x.Entry.ContextTagsJson.ToLower().Contains(needle));
        }
        if (!string.IsNullOrWhiteSpace(filter.FocusTag))
        {
            var needle = TagNeedle(filter.FocusTag);
            query = query.Where(x => x.Entry.FocusTagsJson != null && x.Entry.FocusTagsJson.ToLower().Contains(needle));
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
                x.Source.Id, x.Source.Name, x.Entry.CreatedAt,
                x.Entry.Subskill, x.Entry.DifficultyBand,
                ParseJsonStringArray(x.Entry.ContextTagsJson), ParseJsonStringArray(x.Entry.FocusTagsJson)))
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
            loaded.Entry.CreatedAt, ToSourceInfoDto(loaded.Source), traceability,
            loaded.Entry.Subskill, loaded.Entry.DifficultyBand,
            ParseJsonStringArray(loaded.Entry.ContextTagsJson), ParseJsonStringArray(loaded.Entry.FocusTagsJson));
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

        if (!string.IsNullOrWhiteSpace(filter.Subskill))
        {
            var subskill = filter.Subskill.Trim();
            query = query.Where(x => x.Entry.Subskill == subskill);
        }
        if (filter.DifficultyBand.HasValue)
            query = query.Where(x => x.Entry.DifficultyBand == filter.DifficultyBand.Value);
        if (!string.IsNullOrWhiteSpace(filter.ContextTag))
        {
            var needle = TagNeedle(filter.ContextTag);
            query = query.Where(x => x.Entry.ContextTagsJson != null && x.Entry.ContextTagsJson.ToLower().Contains(needle));
        }
        if (!string.IsNullOrWhiteSpace(filter.FocusTag))
        {
            var needle = TagNeedle(filter.FocusTag);
            query = query.Where(x => x.Entry.FocusTagsJson != null && x.Entry.FocusTagsJson.ToLower().Contains(needle));
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
                x.Source.Id, x.Source.Name, x.Entry.CreatedAt,
                x.Entry.Subskill, x.Entry.DifficultyBand,
                ParseJsonStringArray(x.Entry.ContextTagsJson), ParseJsonStringArray(x.Entry.FocusTagsJson)))
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
            loaded.Entry.ReferenceExcerpt, loaded.Entry.CreatedAt, ToSourceInfoDto(loaded.Source), traceability,
            loaded.Entry.Subskill, loaded.Entry.DifficultyBand,
            ParseJsonStringArray(loaded.Entry.ContextTagsJson), ParseJsonStringArray(loaded.Entry.FocusTagsJson));
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

    // ── Unified read model (Phase H1) ─────────────────────────────────────────────
    //
    // Aggregates all four typed bank tables into one filtered/paginated view for the unified
    // admin Resource Bank page. Each Build*Async helper below applies the same per-type filters
    // the typed List*Async methods above already use (search/CEFR/source/subskill/difficulty/
    // context/focus), then materializes the (already-filtered, currently small) result set into
    // memory and maps it onto the shared UnifiedResourceBankItemDto shape.
    //
    // Sorting and pagination happen in memory over the merged, already-filtered set from up to
    // four tables. At current content volume (dozens of rows per type, from internal seed packs
    // only — no external dataset imported) this is simple and safe. It is a documented
    // limitation, not an oversight: a genuinely large multi-table cross-page query would need a
    // real unified projection (e.g. a DB view or the Option A physical table discussed in
    // docs/architecture/product-model-realignment-h0.md §4) — out of scope for H1.
    //
    // Skill filtering happens in memory after mapping, since "Skill" is a constant-per-type value
    // for three of the four tables (Vocabulary/Grammar/ReadingReference) and only genuinely
    // per-row on CefrReadingPassage.PrimarySkill.

    public async Task<UnifiedResourceBankListResult> ListUnifiedAsync(
        UnifiedResourceBankListFilter filter, CancellationToken ct = default)
    {
        var (page, pageSize) = NormalizePaging(filter.Page, filter.PageSize);

        var items = new List<UnifiedResourceBankItemDto>();

        if (filter.Type is null or UnifiedResourceBankItemType.Vocabulary)
            items.AddRange(await BuildUnifiedVocabularyAsync(filter, ct));
        if (filter.Type is null or UnifiedResourceBankItemType.Grammar)
            items.AddRange(await BuildUnifiedGrammarAsync(filter, ct));
        if (filter.Type is null or UnifiedResourceBankItemType.ReadingReference)
            items.AddRange(await BuildUnifiedReadingReferenceAsync(filter, ct));
        if (filter.Type is null or UnifiedResourceBankItemType.ReadingPassage)
            items.AddRange(await BuildUnifiedReadingPassageAsync(filter, ct));

        if (!string.IsNullOrWhiteSpace(filter.Skill))
        {
            var skill = filter.Skill.Trim();
            items = items.Where(i => string.Equals(i.Skill, skill, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var ordered = items
            .OrderBy(i => i.Type)
            .ThenBy(i => i.CefrLevel, StringComparer.Ordinal)
            .ThenBy(i => i.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalCount = ordered.Count;
        var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        paged = await WithLinkedCountsAsync(paged, ct);

        return new UnifiedResourceBankListResult(paged, totalCount);
    }

    /// <summary>Phase H3/H4/H5 — populates <see cref="UnifiedResourceBankItemDto.LinkedLearnCount"/>
    /// from <c>LearnItemResourceLink</c>, <see cref="UnifiedResourceBankItemDto.LinkedActivityCount"/>
    /// from <c>ActivityResourceLink</c>, and <see cref="UnifiedResourceBankItemDto.LinkedModuleCount"/>
    /// from the distinct set of Modules reachable via either link chain (resource → Learn Item →
    /// Module, or resource → Activity → Module) — 0 when nothing references the row, never null
    /// once this runs.</summary>
    private async Task<List<UnifiedResourceBankItemDto>> WithLinkedCountsAsync(
        List<UnifiedResourceBankItemDto> items, CancellationToken ct)
    {
        if (items.Count == 0) return items;

        var ids = items.Select(i => i.Id).ToList();
        var learnCounts = await _db.LearnItemResourceLinks
            .Where(l => ids.Contains(l.ResourceId))
            .GroupBy(l => new { l.ResourceType, l.ResourceId })
            .Select(g => new { g.Key.ResourceType, g.Key.ResourceId, Count = g.Select(l => l.LearnItemId).Distinct().Count() })
            .ToListAsync(ct);
        var activityCounts = await _db.ActivityResourceLinks
            .Where(l => ids.Contains(l.ResourceId))
            .GroupBy(l => new { l.ResourceType, l.ResourceId })
            .Select(g => new { g.Key.ResourceType, g.Key.ResourceId, Count = g.Select(l => l.ActivityDefinitionId).Distinct().Count() })
            .ToListAsync(ct);

        var moduleViaLearnItem = await
            (from rl in _db.LearnItemResourceLinks
             where ids.Contains(rl.ResourceId)
             join ml in _db.ModuleDefinitionLearnItemLinks on rl.LearnItemId equals ml.LearnItemId
             select new { rl.ResourceType, rl.ResourceId, ml.ModuleDefinitionId })
            .ToListAsync(ct);
        var moduleViaActivity = await
            (from rl in _db.ActivityResourceLinks
             where ids.Contains(rl.ResourceId)
             join ml in _db.ModuleDefinitionActivityLinks on rl.ActivityDefinitionId equals ml.ActivityDefinitionId
             select new { rl.ResourceType, rl.ResourceId, ml.ModuleDefinitionId })
            .ToListAsync(ct);
        var moduleCounts = moduleViaLearnItem.Concat(moduleViaActivity)
            .GroupBy(x => new { x.ResourceType, x.ResourceId })
            .Select(g => new { g.Key.ResourceType, g.Key.ResourceId, Count = g.Select(x => x.ModuleDefinitionId).Distinct().Count() })
            .ToList();

        return items
            .Select(i => i with
            {
                LinkedLearnCount = learnCounts
                    .Where(c => c.ResourceId == i.Id && MatchesUnifiedType(c.ResourceType, i.Type))
                    .Select(c => (int?)c.Count)
                    .FirstOrDefault() ?? 0,
                LinkedActivityCount = activityCounts
                    .Where(c => c.ResourceId == i.Id && MatchesUnifiedType(c.ResourceType, i.Type))
                    .Select(c => (int?)c.Count)
                    .FirstOrDefault() ?? 0,
                LinkedModuleCount = moduleCounts
                    .Where(c => c.ResourceId == i.Id && MatchesUnifiedType(c.ResourceType, i.Type))
                    .Select(c => (int?)c.Count)
                    .FirstOrDefault() ?? 0,
            })
            .ToList();
    }

    private static bool MatchesUnifiedType(Domain.Enums.PublishedResourceType linkType, UnifiedResourceBankItemType unifiedType) =>
        (linkType, unifiedType) switch
        {
            (Domain.Enums.PublishedResourceType.Vocabulary, UnifiedResourceBankItemType.Vocabulary) => true,
            (Domain.Enums.PublishedResourceType.Grammar, UnifiedResourceBankItemType.Grammar) => true,
            (Domain.Enums.PublishedResourceType.ReadingReference, UnifiedResourceBankItemType.ReadingReference) => true,
            (Domain.Enums.PublishedResourceType.ReadingPassage, UnifiedResourceBankItemType.ReadingPassage) => true,
            _ => false
        };

    private async Task<List<UnifiedResourceBankItemDto>> BuildUnifiedVocabularyAsync(
        UnifiedResourceBankListFilter filter, CancellationToken ct)
    {
        var query =
            from e in _db.CefrVocabularyEntries
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            select new { Entry = e, Source = s };

        if (filter.SourceId.HasValue)
            query = query.Where(x => x.Entry.SourceId == filter.SourceId.Value);
        if (!string.IsNullOrWhiteSpace(filter.CefrLevel))
            query = query.Where(x => x.Entry.CefrLevel == filter.CefrLevel);
        if (!string.IsNullOrWhiteSpace(filter.Subskill))
            query = query.Where(x => x.Entry.Subskill == filter.Subskill.Trim());
        if (filter.DifficultyBand.HasValue)
            query = query.Where(x => x.Entry.DifficultyBand == filter.DifficultyBand.Value);
        if (!string.IsNullOrWhiteSpace(filter.ContextTag))
        {
            var needle = TagNeedle(filter.ContextTag);
            query = query.Where(x => x.Entry.ContextTagsJson != null && x.Entry.ContextTagsJson.ToLower().Contains(needle));
        }
        if (!string.IsNullOrWhiteSpace(filter.FocusTag))
        {
            var needle = TagNeedle(filter.FocusTag);
            query = query.Where(x => x.Entry.FocusTagsJson != null && x.Entry.FocusTagsJson.ToLower().Contains(needle));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Entry.Word.ToLower().Contains(search)
                || (x.Entry.Notes != null && x.Entry.Notes.ToLower().Contains(search)));
        }

        var loaded = await query.ToListAsync(ct);

        return loaded
            .Select(x => new UnifiedResourceBankItemDto(
                x.Entry.Id, UnifiedResourceBankItemType.Vocabulary, x.Entry.Word, x.Entry.Notes,
                x.Entry.CefrLevel, "Vocabulary", x.Entry.Subskill,
                ParseJsonStringArray(x.Entry.ContextTagsJson), ParseJsonStringArray(x.Entry.FocusTagsJson),
                x.Entry.DifficultyBand, x.Source.Id, x.Source.Name, null, "Published",
                x.Entry.CreatedAt, null, nameof(CefrVocabularyEntry), "/admin/resource-banks/vocabulary",
                null, null, null))
            .ToList();
    }

    private async Task<List<UnifiedResourceBankItemDto>> BuildUnifiedGrammarAsync(
        UnifiedResourceBankListFilter filter, CancellationToken ct)
    {
        var query =
            from e in _db.CefrGrammarProfileEntries
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            select new { Entry = e, Source = s };

        if (filter.SourceId.HasValue)
            query = query.Where(x => x.Entry.SourceId == filter.SourceId.Value);
        if (!string.IsNullOrWhiteSpace(filter.CefrLevel))
            query = query.Where(x => x.Entry.CefrLevel == filter.CefrLevel);
        if (!string.IsNullOrWhiteSpace(filter.Subskill))
            query = query.Where(x => x.Entry.Subskill == filter.Subskill.Trim());
        if (filter.DifficultyBand.HasValue)
            query = query.Where(x => x.Entry.DifficultyBand == filter.DifficultyBand.Value);
        if (!string.IsNullOrWhiteSpace(filter.ContextTag))
        {
            var needle = TagNeedle(filter.ContextTag);
            query = query.Where(x => x.Entry.ContextTagsJson != null && x.Entry.ContextTagsJson.ToLower().Contains(needle));
        }
        if (!string.IsNullOrWhiteSpace(filter.FocusTag))
        {
            var needle = TagNeedle(filter.FocusTag);
            query = query.Where(x => x.Entry.FocusTagsJson != null && x.Entry.FocusTagsJson.ToLower().Contains(needle));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Entry.GrammarPoint.ToLower().Contains(search)
                || (x.Entry.Description != null && x.Entry.Description.ToLower().Contains(search)));
        }

        var loaded = await query.ToListAsync(ct);

        return loaded
            .Select(x => new UnifiedResourceBankItemDto(
                x.Entry.Id, UnifiedResourceBankItemType.Grammar, x.Entry.GrammarPoint, x.Entry.Description,
                x.Entry.CefrLevel, "Grammar", x.Entry.Subskill,
                ParseJsonStringArray(x.Entry.ContextTagsJson), ParseJsonStringArray(x.Entry.FocusTagsJson),
                x.Entry.DifficultyBand, x.Source.Id, x.Source.Name, null, "Published",
                x.Entry.CreatedAt, null, nameof(CefrGrammarProfileEntry), "/admin/resource-banks/grammar",
                null, null, null))
            .ToList();
    }

    private async Task<List<UnifiedResourceBankItemDto>> BuildUnifiedReadingReferenceAsync(
        UnifiedResourceBankListFilter filter, CancellationToken ct)
    {
        var query =
            from e in _db.CefrReadingReferences
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            select new { Entry = e, Source = s };

        if (filter.SourceId.HasValue)
            query = query.Where(x => x.Entry.SourceId == filter.SourceId.Value);
        if (!string.IsNullOrWhiteSpace(filter.CefrLevel))
            query = query.Where(x => x.Entry.CefrLevel == filter.CefrLevel);
        if (!string.IsNullOrWhiteSpace(filter.Subskill))
            query = query.Where(x => x.Entry.Subskill == filter.Subskill.Trim());
        if (filter.DifficultyBand.HasValue)
            query = query.Where(x => x.Entry.DifficultyBand == filter.DifficultyBand.Value);
        if (!string.IsNullOrWhiteSpace(filter.ContextTag))
        {
            var needle = TagNeedle(filter.ContextTag);
            query = query.Where(x => x.Entry.ContextTagsJson != null && x.Entry.ContextTagsJson.ToLower().Contains(needle));
        }
        if (!string.IsNullOrWhiteSpace(filter.FocusTag))
        {
            var needle = TagNeedle(filter.FocusTag);
            query = query.Where(x => x.Entry.FocusTagsJson != null && x.Entry.FocusTagsJson.ToLower().Contains(needle));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.Entry.TextType != null && x.Entry.TextType.ToLower().Contains(search))
                || (x.Entry.DifficultyNotes != null && x.Entry.DifficultyNotes.ToLower().Contains(search))
                || (x.Entry.ReferenceExcerpt != null && x.Entry.ReferenceExcerpt.ToLower().Contains(search)));
        }

        var loaded = await query.ToListAsync(ct);

        return loaded
            .Select(x => new UnifiedResourceBankItemDto(
                x.Entry.Id, UnifiedResourceBankItemType.ReadingReference,
                !string.IsNullOrWhiteSpace(x.Entry.TextType) ? x.Entry.TextType! : "Reading reference",
                Truncate(x.Entry.ReferenceExcerpt, 160),
                x.Entry.CefrLevel, "Reading", x.Entry.Subskill,
                ParseJsonStringArray(x.Entry.ContextTagsJson), ParseJsonStringArray(x.Entry.FocusTagsJson),
                x.Entry.DifficultyBand, x.Source.Id, x.Source.Name, null, "Published",
                x.Entry.CreatedAt, null, nameof(CefrReadingReference), "/admin/resource-banks/reading-references",
                null, null, null))
            .ToList();
    }

    private async Task<List<UnifiedResourceBankItemDto>> BuildUnifiedReadingPassageAsync(
        UnifiedResourceBankListFilter filter, CancellationToken ct)
    {
        var query =
            from e in _db.CefrReadingPassages
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            select new { Entry = e, Source = s };

        if (filter.SourceId.HasValue)
            query = query.Where(x => x.Entry.SourceId == filter.SourceId.Value);
        if (!string.IsNullOrWhiteSpace(filter.CefrLevel))
            query = query.Where(x => x.Entry.CefrLevel == filter.CefrLevel);
        if (!string.IsNullOrWhiteSpace(filter.Subskill))
            query = query.Where(x => x.Entry.Subskill == filter.Subskill.Trim());
        if (filter.DifficultyBand.HasValue)
            query = query.Where(x => x.Entry.DifficultyBand == filter.DifficultyBand.Value);
        if (!string.IsNullOrWhiteSpace(filter.ContextTag))
        {
            var needle = TagNeedle(filter.ContextTag);
            query = query.Where(x => x.Entry.ContextTagsJson != null && x.Entry.ContextTagsJson.ToLower().Contains(needle));
        }
        if (!string.IsNullOrWhiteSpace(filter.FocusTag))
        {
            var needle = TagNeedle(filter.FocusTag);
            query = query.Where(x => x.Entry.FocusTagsJson != null && x.Entry.FocusTagsJson.ToLower().Contains(needle));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Entry.Title.ToLower().Contains(search)
                || x.Entry.PassageText.ToLower().Contains(search)
                || (x.Entry.Summary != null && x.Entry.Summary.ToLower().Contains(search)));
        }

        var loaded = await query.ToListAsync(ct);

        return loaded
            .Select(x => new UnifiedResourceBankItemDto(
                x.Entry.Id, UnifiedResourceBankItemType.ReadingPassage, x.Entry.Title, x.Entry.Summary,
                x.Entry.CefrLevel, x.Entry.PrimarySkill, x.Entry.Subskill,
                ParseJsonStringArray(x.Entry.ContextTagsJson), ParseJsonStringArray(x.Entry.FocusTagsJson),
                x.Entry.DifficultyBand, x.Source.Id, x.Source.Name, x.Entry.ContentFingerprint, "Published",
                x.Entry.CreatedAt, x.Entry.UpdatedAtUtc, nameof(CefrReadingPassage), "/admin/resource-banks/reading-passages",
                null, null, null))
            .ToList();
    }

    private static string? Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "…";
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize) =>
        (Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize));

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

    /// <summary>Phase E9 — builds the quoted, lowercased needle for a tag-containment filter, e.g.
    /// context tag "general" → <c>"general"</c> (with the JSON quotes). Matching the quoted token
    /// against the lowercased tag-JSON text avoids a substring false positive (e.g. "work" matching
    /// "workplace") and translates to a portable SQL LIKE on both PostgreSQL and SQLite.</summary>
    private static string TagNeedle(string? tag) => $"\"{tag!.Trim().ToLowerInvariant()}\"";

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
