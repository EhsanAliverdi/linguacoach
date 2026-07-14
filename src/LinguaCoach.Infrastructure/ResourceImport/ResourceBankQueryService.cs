using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase I0 — read-only browse/search over the single consolidated <see cref="ResourceBankItem"/>
/// table (replaces the four typed Cefr* tables). Only rows Phase E4's
/// <see cref="ResourceCandidatePublishService"/> actually wrote can ever appear here — nothing else
/// in this codebase writes to this table, so no additional "is this row approved" filter is
/// needed; querying the table directly is by construction "published items only".
///
/// <see cref="ResourceBankItem"/> carries no forward reference to the <see cref="ResourceCandidate"/>
/// that produced it, so detail-view traceability is a reverse lookup: find the
/// <see cref="ResourceCandidate"/> whose <see cref="ResourceCandidate.PublishedEntityType"/>/
/// <see cref="ResourceCandidate.PublishedEntityId"/> match the bank row being viewed (the same
/// fields <see cref="ResourceCandidatePublishService.PublishAsync"/> set via
/// <see cref="ResourceCandidate.MarkPublished"/>). A bank row with no matching candidate (e.g.
/// seeded directly, bypassing the publish workflow) returns
/// <see cref="ResourceBankTraceabilityDto.Unavailable"/> rather than throwing.
///
/// The typed <c>List*Async</c>/<c>Get*DetailAsync</c> methods below still exist because
/// <c>TodayBankResourceSelector</c> (student-facing, real-time) depends on their typed DTO shapes —
/// they are now thin type-filtered projections over the one table, each real DB-paginated queries
/// (no more in-memory scan). <see cref="ListUnifiedAsync"/> (Phase H1) is likewise now a genuine
/// single-table DB-paginated query instead of the old four-way in-memory concat/sort/page.
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
        var query = FilteredQuery(PublishedResourceType.Vocabulary, filter);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x => x.Entry.ContentJson.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(ct);
        var page_ = await Page(query, page, pageSize).ToListAsync(ct);

        var items = page_
            .Select(x =>
            {
                var c = ResourceBankItemContent.Deserialize<VocabularyContent>(x.Entry.ContentJson);
                return new ResourceBankVocabularyListItemDto(
                    x.Entry.Id, c.Word, x.Entry.CefrLevel, c.PartOfSpeech, c.Notes,
                    x.Source.Id, x.Source.Name, x.Entry.CreatedAt,
                    x.Entry.Subskill, x.Entry.DifficultyBand,
                    ParseJsonStringArray(x.Entry.ContextTagsJson), ParseJsonStringArray(x.Entry.FocusTagsJson));
            })
            .ToList();

        return new ResourceBankVocabularyListResult(items, totalCount);
    }

    public async Task<ResourceBankVocabularyDetailDto?> GetVocabularyDetailAsync(Guid id, CancellationToken ct = default)
    {
        var loaded = await LoadDetailAsync(PublishedResourceType.Vocabulary, id, ct);
        if (loaded is null) return null;

        var c = ResourceBankItemContent.Deserialize<VocabularyContent>(loaded.Entry.ContentJson);
        var traceability = await BuildTraceabilityAsync("CefrVocabularyEntry", id, ct);

        return new ResourceBankVocabularyDetailDto(
            loaded.Entry.Id, c.Word, loaded.Entry.CefrLevel, c.PartOfSpeech,
            c.Notes, loaded.Entry.CreatedAt, ToSourceInfoDto(loaded.Source), traceability,
            loaded.Entry.Subskill, loaded.Entry.DifficultyBand,
            ParseJsonStringArray(loaded.Entry.ContextTagsJson), ParseJsonStringArray(loaded.Entry.FocusTagsJson));
    }

    // ── Grammar ─────────────────────────────────────────────────────────────────

    public async Task<ResourceBankGrammarListResult> ListGrammarAsync(
        ResourceBankListFilter filter, CancellationToken ct = default)
    {
        var (page, pageSize) = NormalizePaging(filter);
        var query = FilteredQuery(PublishedResourceType.Grammar, filter);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x => x.Entry.ContentJson.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(ct);
        var page_ = await Page(query, page, pageSize).ToListAsync(ct);

        var items = page_
            .Select(x =>
            {
                var c = ResourceBankItemContent.Deserialize<GrammarContent>(x.Entry.ContentJson);
                return new ResourceBankGrammarListItemDto(
                    x.Entry.Id, c.GrammarPoint, x.Entry.CefrLevel, c.Description,
                    x.Source.Id, x.Source.Name, x.Entry.CreatedAt,
                    x.Entry.Subskill, x.Entry.DifficultyBand,
                    ParseJsonStringArray(x.Entry.ContextTagsJson), ParseJsonStringArray(x.Entry.FocusTagsJson));
            })
            .ToList();

        return new ResourceBankGrammarListResult(items, totalCount);
    }

    public async Task<ResourceBankGrammarDetailDto?> GetGrammarDetailAsync(Guid id, CancellationToken ct = default)
    {
        var loaded = await LoadDetailAsync(PublishedResourceType.Grammar, id, ct);
        if (loaded is null) return null;

        var c = ResourceBankItemContent.Deserialize<GrammarContent>(loaded.Entry.ContentJson);
        var traceability = await BuildTraceabilityAsync("CefrGrammarProfileEntry", id, ct);

        return new ResourceBankGrammarDetailDto(
            loaded.Entry.Id, c.GrammarPoint, loaded.Entry.CefrLevel, c.Description,
            loaded.Entry.CreatedAt, ToSourceInfoDto(loaded.Source), traceability,
            loaded.Entry.Subskill, loaded.Entry.DifficultyBand,
            ParseJsonStringArray(loaded.Entry.ContextTagsJson), ParseJsonStringArray(loaded.Entry.FocusTagsJson));
    }

    // ── Reading references ─────────────────────────────────────────────────────

    public async Task<ResourceBankReadingReferenceListResult> ListReadingReferencesAsync(
        ResourceBankListFilter filter, CancellationToken ct = default)
    {
        var (page, pageSize) = NormalizePaging(filter);
        var query = FilteredQuery(PublishedResourceType.ReadingReference, filter);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x => x.Entry.ContentJson.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(ct);
        var page_ = await Page(query, page, pageSize).ToListAsync(ct);

        var items = page_
            .Select(x =>
            {
                var c = ResourceBankItemContent.Deserialize<ReadingReferenceContent>(x.Entry.ContentJson);
                return new ResourceBankReadingReferenceListItemDto(
                    x.Entry.Id, x.Entry.CefrLevel, c.TextType, c.DifficultyNotes, c.ReferenceExcerpt,
                    x.Source.Id, x.Source.Name, x.Entry.CreatedAt,
                    x.Entry.Subskill, x.Entry.DifficultyBand,
                    ParseJsonStringArray(x.Entry.ContextTagsJson), ParseJsonStringArray(x.Entry.FocusTagsJson));
            })
            .ToList();

        return new ResourceBankReadingReferenceListResult(items, totalCount);
    }

    public async Task<ResourceBankReadingReferenceDetailDto?> GetReadingReferenceDetailAsync(Guid id, CancellationToken ct = default)
    {
        var loaded = await LoadDetailAsync(PublishedResourceType.ReadingReference, id, ct);
        if (loaded is null) return null;

        var c = ResourceBankItemContent.Deserialize<ReadingReferenceContent>(loaded.Entry.ContentJson);
        var traceability = await BuildTraceabilityAsync("CefrReadingReference", id, ct);

        return new ResourceBankReadingReferenceDetailDto(
            loaded.Entry.Id, loaded.Entry.CefrLevel, c.TextType, c.DifficultyNotes,
            c.ReferenceExcerpt, loaded.Entry.CreatedAt, ToSourceInfoDto(loaded.Source), traceability,
            loaded.Entry.Subskill, loaded.Entry.DifficultyBand,
            ParseJsonStringArray(loaded.Entry.ContextTagsJson), ParseJsonStringArray(loaded.Entry.FocusTagsJson));
    }

    // ── Reading passages (Phase E7 — full-length passages, distinct from ReadingReference) ────

    public async Task<ResourceBankReadingPassageListResult> ListReadingPassagesAsync(
        ResourceBankListFilter filter, CancellationToken ct = default)
    {
        var (page, pageSize) = NormalizePaging(filter);
        var query = FilteredQuery(PublishedResourceType.ReadingPassage, filter);

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var search = filter.SearchText.Trim().ToLowerInvariant();
            query = query.Where(x => x.Entry.ContentJson.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(ct);
        var page_ = await Page(query, page, pageSize).ToListAsync(ct);

        var items = page_
            .Select(x =>
            {
                var c = ResourceBankItemContent.Deserialize<ReadingPassageContent>(x.Entry.ContentJson);
                return new ResourceBankReadingPassageListItemDto(
                    x.Entry.Id, c.Title, x.Entry.CefrLevel, c.WordCount, c.EstimatedReadingMinutes,
                    x.Entry.Subskill, x.Source.Id, x.Source.Name, x.Entry.CreatedAt);
            })
            .ToList();

        return new ResourceBankReadingPassageListResult(items, totalCount);
    }

    public async Task<ResourceBankReadingPassageDetailDto?> GetReadingPassageDetailAsync(Guid id, CancellationToken ct = default)
    {
        var loaded = await LoadDetailAsync(PublishedResourceType.ReadingPassage, id, ct);
        if (loaded is null) return null;

        var c = ResourceBankItemContent.Deserialize<ReadingPassageContent>(loaded.Entry.ContentJson);
        var traceability = await BuildTraceabilityAsync("CefrReadingPassage", id, ct);

        return new ResourceBankReadingPassageDetailDto(
            loaded.Entry.Id, c.Title, c.PassageText, c.Summary,
            loaded.Entry.CefrLevel, loaded.Entry.DifficultyBand, c.PrimarySkill, loaded.Entry.Subskill,
            ParseJsonStringArray(c.TopicTagsJson), ParseJsonStringArray(loaded.Entry.ContextTagsJson),
            ParseJsonStringArray(loaded.Entry.FocusTagsJson), c.WordCount, c.EstimatedReadingMinutes,
            c.AttributionText, c.QualityScore, loaded.Entry.CreatedAt,
            ToSourceInfoDto(loaded.Source), traceability);
    }

    // ── Unified read model (Phase H1, now a real single-table query per Phase I0) ─────────────

    public async Task<UnifiedResourceBankListResult> ListUnifiedAsync(
        UnifiedResourceBankListFilter filter, CancellationToken ct = default)
    {
        var (page, pageSize) = NormalizePaging(filter.Page, filter.PageSize);

        var query =
            from e in _db.ResourceBankItems
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            where !e.IsArchived
            select new QueryRow { Entry = e, Source = s };

        if (filter.Type.HasValue)
        {
            var domainType = ToDomainType(filter.Type.Value);
            query = query.Where(x => x.Entry.Type == domainType);
        }
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
            query = query.Where(x => x.Entry.ContentJson.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(ct);

        var page_ = await query
            .OrderBy(x => x.Entry.Type)
            .ThenBy(x => x.Entry.CefrLevel)
            .ThenBy(x => x.Entry.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = page_.Select(ToUnifiedDto).ToList();

        // Skill filtering happens in memory after mapping, since "Skill" is a constant-per-type
        // value for three of the four types and only genuinely per-row on ReadingPassage.
        if (!string.IsNullOrWhiteSpace(filter.Skill))
        {
            var skill = filter.Skill.Trim();
            items = items.Where(i => string.Equals(i.Skill, skill, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        items = await WithLinkedCountsAsync(items, ct);

        return new UnifiedResourceBankListResult(items, totalCount);
    }

    private static UnifiedResourceBankItemDto ToUnifiedDto(QueryRow x)
    {
        var entry = x.Entry;
        var source = x.Source;

        return entry.Type switch
        {
            PublishedResourceType.Vocabulary => MapVocabulary(entry, source),
            PublishedResourceType.Grammar => MapGrammar(entry, source),
            PublishedResourceType.ReadingReference => MapReadingReference(entry, source),
            PublishedResourceType.ReadingPassage => MapReadingPassage(entry, source),
            PublishedResourceType.Writing => MapWritingPrompt(entry, source),
            PublishedResourceType.Listening => MapListening(entry, source),
            PublishedResourceType.Speaking => MapSpeaking(entry, source),
            _ => throw new InvalidOperationException($"Unknown PublishedResourceType '{entry.Type}'."),
        };
    }

    private static UnifiedResourceBankItemDto MapListening(ResourceBankItem entry, CefrResourceSource source)
    {
        var c = ResourceBankItemContent.Deserialize<ListeningPassageContent>(entry.ContentJson);
        return new UnifiedResourceBankItemDto(
            entry.Id, UnifiedResourceBankItemType.Listening, c.Title, Truncate(c.Transcript, 160),
            entry.CefrLevel, "Listening", entry.Subskill,
            ParseJsonStringArray(entry.ContextTagsJson), ParseJsonStringArray(entry.FocusTagsJson),
            entry.DifficultyBand, source.Id, source.Name, entry.ContentFingerprint, "Published",
            entry.CreatedAt, entry.UpdatedAt, "CefrListeningPassage", null,
            null, null, null, entry.IsArchived);
    }

    private static UnifiedResourceBankItemDto MapSpeaking(ResourceBankItem entry, CefrResourceSource source)
    {
        var c = ResourceBankItemContent.Deserialize<SpeakingPromptContent>(entry.ContentJson);
        return new UnifiedResourceBankItemDto(
            entry.Id, UnifiedResourceBankItemType.Speaking, c.Title, Truncate(c.PromptText, 160),
            entry.CefrLevel, "Speaking", entry.Subskill,
            ParseJsonStringArray(entry.ContextTagsJson), ParseJsonStringArray(entry.FocusTagsJson),
            entry.DifficultyBand, source.Id, source.Name, entry.ContentFingerprint, "Published",
            entry.CreatedAt, entry.UpdatedAt, "CefrSpeakingPrompt", null,
            null, null, null, entry.IsArchived);
    }

    private static UnifiedResourceBankItemDto MapWritingPrompt(ResourceBankItem entry, CefrResourceSource source)
    {
        var c = ResourceBankItemContent.Deserialize<WritingPromptContent>(entry.ContentJson);
        return new UnifiedResourceBankItemDto(
            entry.Id, UnifiedResourceBankItemType.Writing, c.Title, Truncate(c.PromptText, 160),
            entry.CefrLevel, "Writing", entry.Subskill,
            ParseJsonStringArray(entry.ContextTagsJson), ParseJsonStringArray(entry.FocusTagsJson),
            entry.DifficultyBand, source.Id, source.Name, entry.ContentFingerprint, "Published",
            entry.CreatedAt, entry.UpdatedAt, "CefrWritingPrompt", null,
            null, null, null, entry.IsArchived);
    }

    private static UnifiedResourceBankItemDto MapVocabulary(ResourceBankItem entry, CefrResourceSource source)
    {
        var c = ResourceBankItemContent.Deserialize<VocabularyContent>(entry.ContentJson);
        return new UnifiedResourceBankItemDto(
            entry.Id, UnifiedResourceBankItemType.Vocabulary, c.Word, c.Notes,
            entry.CefrLevel, "Vocabulary", entry.Subskill,
            ParseJsonStringArray(entry.ContextTagsJson), ParseJsonStringArray(entry.FocusTagsJson),
            entry.DifficultyBand, source.Id, source.Name, entry.ContentFingerprint, "Published",
            entry.CreatedAt, entry.UpdatedAt, "CefrVocabularyEntry", null,
            null, null, null, entry.IsArchived);
    }

    private static UnifiedResourceBankItemDto MapGrammar(ResourceBankItem entry, CefrResourceSource source)
    {
        var c = ResourceBankItemContent.Deserialize<GrammarContent>(entry.ContentJson);
        return new UnifiedResourceBankItemDto(
            entry.Id, UnifiedResourceBankItemType.Grammar, c.GrammarPoint, c.Description,
            entry.CefrLevel, "Grammar", entry.Subskill,
            ParseJsonStringArray(entry.ContextTagsJson), ParseJsonStringArray(entry.FocusTagsJson),
            entry.DifficultyBand, source.Id, source.Name, entry.ContentFingerprint, "Published",
            entry.CreatedAt, entry.UpdatedAt, "CefrGrammarProfileEntry", null,
            null, null, null, entry.IsArchived);
    }

    private static UnifiedResourceBankItemDto MapReadingReference(ResourceBankItem entry, CefrResourceSource source)
    {
        var c = ResourceBankItemContent.Deserialize<ReadingReferenceContent>(entry.ContentJson);
        return new UnifiedResourceBankItemDto(
            entry.Id, UnifiedResourceBankItemType.ReadingReference,
            !string.IsNullOrWhiteSpace(c.TextType) ? c.TextType! : "Reading reference",
            Truncate(c.ReferenceExcerpt, 160),
            entry.CefrLevel, "Reading", entry.Subskill,
            ParseJsonStringArray(entry.ContextTagsJson), ParseJsonStringArray(entry.FocusTagsJson),
            entry.DifficultyBand, source.Id, source.Name, entry.ContentFingerprint, "Published",
            entry.CreatedAt, entry.UpdatedAt, "CefrReadingReference", null,
            null, null, null, entry.IsArchived);
    }

    private static UnifiedResourceBankItemDto MapReadingPassage(ResourceBankItem entry, CefrResourceSource source)
    {
        var c = ResourceBankItemContent.Deserialize<ReadingPassageContent>(entry.ContentJson);
        return new UnifiedResourceBankItemDto(
            entry.Id, UnifiedResourceBankItemType.ReadingPassage, c.Title, c.Summary,
            entry.CefrLevel, c.PrimarySkill, entry.Subskill,
            ParseJsonStringArray(entry.ContextTagsJson), ParseJsonStringArray(entry.FocusTagsJson),
            entry.DifficultyBand, source.Id, source.Name, entry.ContentFingerprint, "Published",
            entry.CreatedAt, entry.UpdatedAt, "CefrReadingPassage", null,
            null, null, null, entry.IsArchived);
    }

    /// <summary>Phase K3 — single-row lookup, including archived rows (see interface doc). Powers
    /// the admin detail-page route so a direct link/reload always resolves the item, even one that
    /// was archived after being linked from elsewhere.</summary>
    public async Task<UnifiedResourceBankItemDto?> GetUnifiedByIdAsync(Guid id, CancellationToken ct = default)
    {
        var row = await (
            from e in _db.ResourceBankItems
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            where e.Id == id
            select new QueryRow { Entry = e, Source = s })
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;

        var dto = ToUnifiedDto(row);
        var withCounts = await WithLinkedCountsAsync(new List<UnifiedResourceBankItemDto> { dto }, ct);
        return withCounts[0];
    }

    /// <summary>Phase K5 — see <see cref="IResourceBankQueryService.GetEditDtoAsync"/>'s doc
    /// comment. Deserializes the full, untruncated <see cref="ResourceBankItemContent"/> record
    /// for this item's own <see cref="Domain.Enums.PublishedResourceType"/> rather than reusing
    /// <see cref="ToUnifiedDto"/>'s lossy display projection.</summary>
    public async Task<ResourceBankItemEditDto?> GetEditDtoAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.ResourceBankItems.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null) return null;

        var contextTags = ParseJsonStringArray(entry.ContextTagsJson);
        var focusTags = ParseJsonStringArray(entry.FocusTagsJson);

        var empty = new ResourceBankItemEditDto(
            Id: entry.Id, Type: default, CefrLevel: entry.CefrLevel, Subskill: entry.Subskill, DifficultyBand: entry.DifficultyBand,
            ContextTags: contextTags, FocusTags: focusTags,
            Word: null, PartOfSpeech: null, Notes: null, GrammarPoint: null, Description: null,
            TextType: null, DifficultyNotes: null, ReferenceExcerpt: null,
            Title: null, PassageText: null, Summary: null,
            PromptText: null, Genre: null, SuggestedMinWords: null,
            Transcript: null, SuggestedDurationSeconds: null, ImageUrl: null);

        switch (entry.Type)
        {
            case PublishedResourceType.Vocabulary:
            {
                var c = ResourceBankItemContent.Deserialize<VocabularyContent>(entry.ContentJson);
                return empty with { Type = UnifiedResourceBankItemType.Vocabulary, Word = c.Word, PartOfSpeech = c.PartOfSpeech, Notes = c.Notes };
            }
            case PublishedResourceType.Grammar:
            {
                var c = ResourceBankItemContent.Deserialize<GrammarContent>(entry.ContentJson);
                return empty with { Type = UnifiedResourceBankItemType.Grammar, GrammarPoint = c.GrammarPoint, Description = c.Description };
            }
            case PublishedResourceType.ReadingReference:
            {
                var c = ResourceBankItemContent.Deserialize<ReadingReferenceContent>(entry.ContentJson);
                return empty with { Type = UnifiedResourceBankItemType.ReadingReference, TextType = c.TextType, DifficultyNotes = c.DifficultyNotes, ReferenceExcerpt = c.ReferenceExcerpt };
            }
            case PublishedResourceType.ReadingPassage:
            {
                var c = ResourceBankItemContent.Deserialize<ReadingPassageContent>(entry.ContentJson);
                return empty with { Type = UnifiedResourceBankItemType.ReadingPassage, Title = c.Title, PassageText = c.PassageText, Summary = c.Summary };
            }
            case PublishedResourceType.Writing:
            {
                var c = ResourceBankItemContent.Deserialize<WritingPromptContent>(entry.ContentJson);
                return empty with { Type = UnifiedResourceBankItemType.Writing, Title = c.Title, PromptText = c.PromptText, Genre = c.Genre, SuggestedMinWords = c.SuggestedMinWords };
            }
            case PublishedResourceType.Listening:
            {
                var c = ResourceBankItemContent.Deserialize<ListeningPassageContent>(entry.ContentJson);
                return empty with { Type = UnifiedResourceBankItemType.Listening, Title = c.Title, Transcript = c.Transcript };
            }
            case PublishedResourceType.Speaking:
            {
                var c = ResourceBankItemContent.Deserialize<SpeakingPromptContent>(entry.ContentJson);
                return empty with { Type = UnifiedResourceBankItemType.Speaking, Title = c.Title, PromptText = c.PromptText, SuggestedDurationSeconds = c.SuggestedDurationSeconds, ImageUrl = c.ImageUrl };
            }
            default:
                return null;
        }
    }

    private static PublishedResourceType ToDomainType(UnifiedResourceBankItemType type) => type switch
    {
        UnifiedResourceBankItemType.Vocabulary => PublishedResourceType.Vocabulary,
        UnifiedResourceBankItemType.Grammar => PublishedResourceType.Grammar,
        UnifiedResourceBankItemType.ReadingReference => PublishedResourceType.ReadingReference,
        UnifiedResourceBankItemType.ReadingPassage => PublishedResourceType.ReadingPassage,
        UnifiedResourceBankItemType.Writing => PublishedResourceType.Writing,
        UnifiedResourceBankItemType.Listening => PublishedResourceType.Listening,
        UnifiedResourceBankItemType.Speaking => PublishedResourceType.Speaking,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    /// <summary>Phase H3/H4/H5 — populates <see cref="UnifiedResourceBankItemDto.LinkedLearnCount"/>
    /// from <c>LessonResourceLink</c>, <see cref="UnifiedResourceBankItemDto.LinkedActivityCount"/>
    /// from <c>ExerciseResourceLink</c>, and <see cref="UnifiedResourceBankItemDto.LinkedModuleCount"/>
    /// from the distinct set of Modules reachable via either link chain (resource → Lesson →
    /// Module, or resource → Activity → Module) — 0 when nothing references the row, never null
    /// once this runs.</summary>
    private async Task<List<UnifiedResourceBankItemDto>> WithLinkedCountsAsync(
        List<UnifiedResourceBankItemDto> items, CancellationToken ct)
    {
        if (items.Count == 0) return items;

        var ids = items.Select(i => i.Id).ToList();
        var learnCounts = await _db.LessonResourceLinks
            .Where(l => ids.Contains(l.ResourceId))
            .GroupBy(l => new { l.ResourceType, l.ResourceId })
            .Select(g => new { g.Key.ResourceType, g.Key.ResourceId, Count = g.Select(l => l.LessonId).Distinct().Count() })
            .ToListAsync(ct);
        var activityCounts = await _db.ExerciseResourceLinks
            .Where(l => ids.Contains(l.ResourceId))
            .GroupBy(l => new { l.ResourceType, l.ResourceId })
            .Select(g => new { g.Key.ResourceType, g.Key.ResourceId, Count = g.Select(l => l.ExerciseId).Distinct().Count() })
            .ToListAsync(ct);

        var moduleViaLesson = await
            (from rl in _db.LessonResourceLinks
             where ids.Contains(rl.ResourceId)
             join ml in _db.ModuleLessonLinks on rl.LessonId equals ml.LessonId
             select new { rl.ResourceType, rl.ResourceId, ml.ModuleId })
            .ToListAsync(ct);
        var moduleViaActivity = await
            (from rl in _db.ExerciseResourceLinks
             where ids.Contains(rl.ResourceId)
             join ml in _db.ModuleExerciseLinks on rl.ExerciseId equals ml.ExerciseId
             select new { rl.ResourceType, rl.ResourceId, ml.ModuleId })
            .ToListAsync(ct);
        var moduleCounts = moduleViaLesson.Concat(moduleViaActivity)
            .GroupBy(x => new { x.ResourceType, x.ResourceId })
            .Select(g => new { g.Key.ResourceType, g.Key.ResourceId, Count = g.Select(x => x.ModuleId).Distinct().Count() })
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

    private static bool MatchesUnifiedType(PublishedResourceType linkType, UnifiedResourceBankItemType unifiedType) =>
        (linkType, unifiedType) switch
        {
            (PublishedResourceType.Vocabulary, UnifiedResourceBankItemType.Vocabulary) => true,
            (PublishedResourceType.Grammar, UnifiedResourceBankItemType.Grammar) => true,
            (PublishedResourceType.ReadingReference, UnifiedResourceBankItemType.ReadingReference) => true,
            (PublishedResourceType.ReadingPassage, UnifiedResourceBankItemType.ReadingPassage) => true,
            (PublishedResourceType.Writing, UnifiedResourceBankItemType.Writing) => true,
            (PublishedResourceType.Listening, UnifiedResourceBankItemType.Listening) => true,
            (PublishedResourceType.Speaking, UnifiedResourceBankItemType.Speaking) => true,
            _ => false
        };

    // ── Shared query helpers ─────────────────────────────────────────────────────

    private IQueryable<QueryRow> FilteredQuery(PublishedResourceType type, ResourceBankListFilter filter)
    {
        var query =
            from e in _db.ResourceBankItems
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            where e.Type == type
            select new QueryRow { Entry = e, Source = s };

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

        return query;
    }

    private sealed class QueryRow
    {
        public ResourceBankItem Entry { get; set; } = null!;
        public CefrResourceSource Source { get; set; } = null!;
    }

    private static IQueryable<QueryRow> Page(IQueryable<QueryRow> query, int page, int pageSize) =>
        query.OrderByDescending(x => x.Entry.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize);

    private async Task<QueryRow?> LoadDetailAsync(PublishedResourceType type, Guid id, CancellationToken ct) =>
        await (
            from e in _db.ResourceBankItems
            join s in _db.CefrResourceSources on e.SourceId equals s.Id
            where e.Id == id && e.Type == type
            select new QueryRow { Entry = e, Source = s })
            .FirstOrDefaultAsync(ct);

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
