using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase E8 — "Internal Resource Bank Depth Expansion". Exercises
/// <see cref="InternalResourceSeedPackE8Seeder"/> end-to-end against SQLite in-memory, matching the
/// E6 seeder test's conventions exactly. All three real (non-AI) services are constructed directly;
/// no live/real/mocked AI provider is involved anywhere. Proves the E8 pack flows through the full
/// staging → validation → approval → publish pipeline, publishes to the correct bank tables, covers
/// A1-B2, keeps workplace context a minority, and never bypasses the publish workflow.
/// </summary>
public sealed class InternalResourceSeedPackE8SeederTests : IDisposable
{
    // Expected E8 pack counts.
    private const int VocabCount = 40;
    private const int GrammarCount = 20;
    private const int ReadingRefCount = 16;
    private const int PassageCount = 8;
    private const int TotalCount = VocabCount + GrammarCount + ReadingRefCount + PassageCount;

    private readonly LinguaCoachDbContext _db;
    private readonly ResourceImportService _importService;
    private readonly ResourceCandidateValidationService _validationService;
    private readonly ResourceCandidatePublishService _publishService;
    private readonly ResourceBankQueryService _queryService;

    public InternalResourceSeedPackE8SeederTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _importService = new ResourceImportService(_db, new ActivityContentFingerprintService());
        _validationService = new ResourceCandidateValidationService(_db, new FormIoSchemaValidationService());
        _publishService = new ResourceCandidatePublishService(_db);
        _queryService = new ResourceBankQueryService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private Task RunSeederAsync() => InternalResourceSeedPackE8Seeder.SeedAsync(
        _db, _importService, _validationService, _publishService, NullLogger.Instance);

    private static IReadOnlyList<string> CefrLevels => new[] { "A1", "A2", "B1", "B2" };

    // ── Source registration + idempotency ───────────────────────────────────────

    [Fact]
    public async Task Seed_creates_exactly_one_internal_e8_source_marked_original()
    {
        await RunSeederAsync();

        var sources = await _db.CefrResourceSources
            .Where(s => s.Name == InternalResourceSeedPackE8Seeder.SourceName)
            .ToListAsync();

        sources.Should().HaveCount(1);
        sources[0].LanguageCode.Should().Be("en");
        sources[0].IsImportApproved.Should().BeTrue();
        sources[0].AllowsStudentDisplay.Should().BeTrue();
        sources[0].AllowsCommercialUse.Should().BeTrue();
        sources[0].LicenseType.Should().Be("Internal/Original");
    }

    [Fact]
    public async Task Rerunning_the_full_seeder_is_a_safe_no_op_with_no_duplicate_rows()
    {
        await RunSeederAsync();
        await RunSeederAsync();

        (await _db.CefrResourceSources.CountAsync(s => s.Name == InternalResourceSeedPackE8Seeder.SourceName)).Should().Be(1);
        var source = await _db.CefrResourceSources.FirstAsync(s => s.Name == InternalResourceSeedPackE8Seeder.SourceName);
        (await _db.ResourceImportRuns.CountAsync(r => r.CefrResourceSourceId == source.Id)).Should().Be(4);
        (await _db.ResourceCandidates.CountAsync()).Should().Be(TotalCount);
    }

    // ── Raw records → candidates ────────────────────────────────────────────────

    [Fact]
    public async Task Seed_stages_every_row_as_a_candidate_with_no_rejections()
    {
        await RunSeederAsync();

        var source = await _db.CefrResourceSources.FirstAsync(s => s.Name == InternalResourceSeedPackE8Seeder.SourceName);
        var runs = await _db.ResourceImportRuns.Where(r => r.CefrResourceSourceId == source.Id).ToListAsync();

        runs.Should().HaveCount(4);
        runs.Sum(r => r.TotalRecordCount).Should().Be(TotalCount);
        runs.Sum(r => r.SucceededCount).Should().Be(TotalCount);
        runs.Sum(r => r.RejectedCount).Should().Be(0);

        (await _db.ResourceCandidates.CountAsync()).Should().Be(TotalCount);
    }

    // ── Validation passes deterministically, no AI ──────────────────────────────

    [Fact]
    public async Task Every_e8_candidate_validates_as_Passed()
    {
        await RunSeederAsync();

        var candidateIds = await _db.ResourceCandidates.Select(c => c.Id).ToListAsync();
        candidateIds.Should().HaveCount(TotalCount);

        foreach (var id in candidateIds)
        {
            var result = await _validationService.ValidateAsync(id);
            result.Status.Should().Be(ResourceCandidateValidationStatus.Passed.ToString(),
                because: $"candidate {id} errors: {string.Join(";", result.Errors)}");
        }
    }

    // ── Publish targets + counts ────────────────────────────────────────────────

    [Fact]
    public async Task Seed_publishes_every_candidate_to_its_correct_bank_table()
    {
        await RunSeederAsync();

        var source = await _db.CefrResourceSources.FirstAsync(s => s.Name == InternalResourceSeedPackE8Seeder.SourceName);

        (await CountFromSource(_db.CefrVocabularyEntries.Select(v => v.SourceId), source.Id)).Should().Be(VocabCount);
        (await CountFromSource(_db.CefrGrammarProfileEntries.Select(g => g.SourceId), source.Id)).Should().Be(GrammarCount);
        (await CountFromSource(_db.CefrReadingReferences.Select(r => r.SourceId), source.Id)).Should().Be(ReadingRefCount);
        (await CountFromSource(_db.CefrReadingPassages.Select(p => p.SourceId), source.Id)).Should().Be(PassageCount);

        (await _db.ResourceCandidates.CountAsync(c => c.IsPublished)).Should().Be(TotalCount);
    }

    private static Task<int> CountFromSource(IQueryable<Guid> sourceIds, Guid sourceId) =>
        sourceIds.CountAsync(id => id == sourceId);

    // ── CEFR coverage ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Published_content_covers_A1_A2_B1_and_B2_in_every_category()
    {
        await RunSeederAsync();

        var source = await _db.CefrResourceSources.FirstAsync(s => s.Name == InternalResourceSeedPackE8Seeder.SourceName);

        var vocabLevels = await _db.CefrVocabularyEntries.Where(v => v.SourceId == source.Id).Select(v => v.CefrLevel).Distinct().ToListAsync();
        var grammarLevels = await _db.CefrGrammarProfileEntries.Where(g => g.SourceId == source.Id).Select(g => g.CefrLevel).Distinct().ToListAsync();
        var refLevels = await _db.CefrReadingReferences.Where(r => r.SourceId == source.Id).Select(r => r.CefrLevel).Distinct().ToListAsync();
        var passageLevels = await _db.CefrReadingPassages.Where(p => p.SourceId == source.Id).Select(p => p.CefrLevel).Distinct().ToListAsync();

        vocabLevels.Should().Contain(CefrLevels);
        grammarLevels.Should().Contain(CefrLevels);
        refLevels.Should().Contain(CefrLevels);
        passageLevels.Should().Contain(CefrLevels);
    }

    [Fact]
    public async Task No_single_cefr_level_dominates_the_vocabulary_pack()
    {
        await RunSeederAsync();

        var source = await _db.CefrResourceSources.FirstAsync(s => s.Name == InternalResourceSeedPackE8Seeder.SourceName);
        var byLevel = await _db.CefrVocabularyEntries
            .Where(v => v.SourceId == source.Id)
            .GroupBy(v => v.CefrLevel)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync();

        // Balanced 10 per level — no level should hold more than half the pack.
        byLevel.Should().OnlyContain(x => x.Count <= VocabCount / 2);
    }

    // ── Discoverability via the E5 read surface ─────────────────────────────────

    [Fact]
    public async Task Published_vocabulary_is_discoverable_via_query_service_search()
    {
        await RunSeederAsync();

        var result = await _queryService.ListVocabularyAsync(new ResourceBankListFilter(SearchText: "persuade"));
        result.Items.Should().Contain(i => i.Word == "persuade" && i.CefrLevel == "B2");
    }

    [Fact]
    public async Task Published_grammar_is_discoverable_via_query_service_search()
    {
        await RunSeederAsync();

        var result = await _queryService.ListGrammarAsync(new ResourceBankListFilter(SearchText: "second conditional"));
        result.Items.Should().Contain(i => i.GrammarPoint == "Second conditional" && i.CefrLevel == "B2");
    }

    [Fact]
    public async Task Published_short_reading_reference_is_discoverable_and_within_excerpt_limit()
    {
        await RunSeederAsync();

        var source = await _db.CefrResourceSources.FirstAsync(s => s.Name == InternalResourceSeedPackE8Seeder.SourceName);
        var refs = await _db.CefrReadingReferences.Where(r => r.SourceId == source.Id).ToListAsync();

        refs.Should().HaveCount(ReadingRefCount);
        refs.Should().OnlyContain(r =>
            r.ReferenceExcerpt == null || r.ReferenceExcerpt.Length <= ResourceCandidatePublishService.MaxReadingExcerptLength);
    }

    [Fact]
    public async Task Published_full_reading_passage_is_discoverable_via_query_service_search()
    {
        await RunSeederAsync();

        var result = await _queryService.ListReadingPassagesAsync(new ResourceBankListFilter(SearchText: "zoo"));
        result.Items.Should().Contain(i => i.Title == "A Visit to the Zoo" && i.CefrLevel == "A1");
    }

    // ── ReadingPassage vs ReadingReference routing by length ────────────────────

    [Fact]
    public async Task Long_passages_route_to_CefrReadingPassage_and_short_ones_to_CefrReadingReference()
    {
        await RunSeederAsync();

        var source = await _db.CefrResourceSources.FirstAsync(s => s.Name == InternalResourceSeedPackE8Seeder.SourceName);

        var passages = await _db.CefrReadingPassages.Where(p => p.SourceId == source.Id).ToListAsync();
        passages.Should().OnlyContain(p => p.PassageText.Length > ResourceCandidatePublishService.MaxReadingExcerptLength);

        var refs = await _db.CefrReadingReferences.Where(r => r.SourceId == source.Id).ToListAsync();
        refs.Should().OnlyContain(r =>
            r.ReferenceExcerpt != null && r.ReferenceExcerpt.Length <= ResourceCandidatePublishService.MaxReadingExcerptLength);
    }

    // ── Phase E8 metadata mapping: focus tags + difficulty band on full passages ─

    [Fact]
    public async Task Full_passages_carry_context_focus_and_difficulty_metadata_from_the_deterministic_mapping()
    {
        await RunSeederAsync();

        var passage = await _db.CefrReadingPassages.FirstAsync(p => p.Title == "A Visit to the Zoo");
        passage.CefrLevel.Should().Be("A1");
        passage.PrimarySkill.Should().Be("reading");
        passage.Subskill.Should().Be("reading.gist");
        passage.DifficultyBand.Should().Be(1);
        passage.ContextTagsJson.Should().NotBeNull();
        passage.ContextTagsJson!.Should().Contain("daily");
        passage.FocusTagsJson.Should().NotBeNull();
        passage.FocusTagsJson!.Should().Contain("main_idea");
        passage.WordCount.Should().BeGreaterThan(20);
        passage.EstimatedReadingMinutes.Should().BeGreaterThan(0);
    }

    // ── Context balance: workplace is a minority ────────────────────────────────

    [Fact]
    public async Task Workplace_is_not_the_majority_context_across_the_pack()
    {
        await RunSeederAsync();

        // Context tags are stored durably on CefrReadingPassage; for the other bank types the tags
        // live on the ResourceCandidate. Count workplace-tagged candidates across the whole E8 pack.
        var source = await _db.CefrResourceSources.FirstAsync(s => s.Name == InternalResourceSeedPackE8Seeder.SourceName);

        var candidateContextTags = await (
            from c in _db.ResourceCandidates
            join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
            join run in _db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
            where run.CefrResourceSourceId == source.Id && c.ContextTagsJson != null
            select c.ContextTagsJson!)
            .ToListAsync();

        candidateContextTags.Should().HaveCount(TotalCount);
        var workplaceCount = candidateContextTags.Count(t => t.Contains("workplace"));
        workplaceCount.Should().BeLessThan(TotalCount / 2,
            because: "the E8 pack must default to general English, with workplace as a minority context");
    }

    // ── English-only / no bilingual seed content ────────────────────────────────

    [Fact]
    public async Task All_e8_candidates_are_english_and_contain_no_non_latin_seed_content()
    {
        await RunSeederAsync();

        var source = await _db.CefrResourceSources.FirstAsync(s => s.Name == InternalResourceSeedPackE8Seeder.SourceName);
        var candidates = await (
            from c in _db.ResourceCandidates
            join r in _db.ResourceRawRecords on c.ResourceRawRecordId equals r.Id
            join run in _db.ResourceImportRuns on r.ResourceImportRunId equals run.Id
            where run.CefrResourceSourceId == source.Id
            select new { c.LanguageCode, c.NormalizedJson })
            .ToListAsync();

        candidates.Should().OnlyContain(c => c.LanguageCode == "en");
        // No Persian/Arabic-script (or any non-ASCII) seed content anywhere in the pack.
        candidates.Should().OnlyContain(c =>
            System.Text.RegularExpressions.Regex.IsMatch(c.NormalizedJson, "^[\\x00-\\x7F]*$"));
    }

    // ── Provenance / no direct-final-table bypass ───────────────────────────────

    [Fact]
    public async Task Every_published_e8_bank_row_traces_back_to_a_published_candidate_from_the_e8_source()
    {
        await RunSeederAsync();

        var source = await _db.CefrResourceSources.FirstAsync(s => s.Name == InternalResourceSeedPackE8Seeder.SourceName);

        var vocabIds = await _db.CefrVocabularyEntries.Where(v => v.SourceId == source.Id).Select(v => v.Id).ToListAsync();
        var grammarIds = await _db.CefrGrammarProfileEntries.Where(g => g.SourceId == source.Id).Select(g => g.Id).ToListAsync();
        var refIds = await _db.CefrReadingReferences.Where(r => r.SourceId == source.Id).Select(r => r.Id).ToListAsync();
        var passageIds = await _db.CefrReadingPassages.Where(p => p.SourceId == source.Id).Select(p => p.Id).ToListAsync();

        vocabIds.Should().HaveCount(VocabCount);
        grammarIds.Should().HaveCount(GrammarCount);
        refIds.Should().HaveCount(ReadingRefCount);
        passageIds.Should().HaveCount(PassageCount);

        foreach (var id in vocabIds) await AssertTracesToPublishedCandidate(id, nameof(LinguaCoach.Domain.Entities.CefrVocabularyEntry));
        foreach (var id in grammarIds) await AssertTracesToPublishedCandidate(id, nameof(LinguaCoach.Domain.Entities.CefrGrammarProfileEntry));
        foreach (var id in refIds) await AssertTracesToPublishedCandidate(id, nameof(LinguaCoach.Domain.Entities.CefrReadingReference));
        foreach (var id in passageIds) await AssertTracesToPublishedCandidate(id, nameof(LinguaCoach.Domain.Entities.CefrReadingPassage));
    }

    private async Task AssertTracesToPublishedCandidate(Guid publishedEntityId, string publishedEntityType)
    {
        var candidate = await _db.ResourceCandidates.AsNoTracking().FirstOrDefaultAsync(
            c => c.PublishedEntityId == publishedEntityId && c.PublishedEntityType == publishedEntityType);

        candidate.Should().NotBeNull(because: $"{publishedEntityType} {publishedEntityId} must trace back to the candidate that published it");
        candidate!.IsPublished.Should().BeTrue();
        candidate.PublishedAtUtc.Should().NotBeNull();
    }

    // ── Coexistence with the E6/E7 pack ─────────────────────────────────────────

    [Fact]
    public async Task E8_and_E6_packs_coexist_as_two_independent_sources()
    {
        await InternalResourceSeedPackSeeder.SeedAsync(
            _db, _importService, _validationService, _publishService, NullLogger.Instance);
        await RunSeederAsync();

        (await _db.CefrResourceSources.CountAsync(s => s.Name == InternalResourceSeedPackSeeder.SourceName)).Should().Be(1);
        (await _db.CefrResourceSources.CountAsync(s => s.Name == InternalResourceSeedPackE8Seeder.SourceName)).Should().Be(1);

        // Combined published depth = E6/E7 (32/12/10/10) + E8 (40/20/16/8).
        (await _db.CefrVocabularyEntries.CountAsync()).Should().Be(32 + VocabCount);
        (await _db.CefrGrammarProfileEntries.CountAsync()).Should().Be(12 + GrammarCount);
        (await _db.CefrReadingReferences.CountAsync()).Should().Be(10 + ReadingRefCount);
        (await _db.CefrReadingPassages.CountAsync()).Should().Be(10 + PassageCount);
    }
}
