using System.Text;
using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
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
/// Phase E6 — "First Real English Resource Depth". Exercises
/// <see cref="InternalResourceSeedPackSeeder"/> end-to-end against SQLite in-memory, matching every
/// other Resource* test file's convention in this directory. All three real (non-AI) services are
/// constructed directly — <see cref="ResourceImportService"/>, <see cref="ResourceCandidateValidationService"/>,
/// and <see cref="ResourceCandidatePublishService"/> take no <c>IAiProvider</c> dependency at all,
/// so there is no live/real/mocked AI call anywhere in this suite by construction (see the
/// "No AI provider is ever invoked" section below for the explicit proof).
/// </summary>
public sealed class InternalResourceSeedPackSeederTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ResourceImportService _importService;
    private readonly ResourceCandidateValidationService _validationService;
    private readonly ResourceCandidatePublishService _publishService;
    private readonly ResourceBankQueryService _queryService;

    public InternalResourceSeedPackSeederTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _importService = new ResourceImportService(_db, new ActivityContentFingerprintService(), new ResourceCandidateContentSerializer());
        _validationService = new ResourceCandidateValidationService(_db, new FormIoSchemaValidationService());
        _publishService = new ResourceCandidatePublishService(_db, new ResourceCandidateContentSerializer());
        _queryService = new ResourceBankQueryService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private Task RunSeederAsync() => InternalResourceSeedPackSeeder.SeedAsync(
        _db, _importService, _validationService, _publishService, NullLogger.Instance);

    // ── Source registration is idempotent ───────────────────────────────────────

    [Fact]
    public async Task Seed_creates_exactly_one_internal_source_named_as_expected()
    {
        await RunSeederAsync();

        var sources = await _db.CefrResourceSources
            .Where(s => s.Name == InternalResourceSeedPackSeeder.SourceName)
            .ToListAsync();

        sources.Should().HaveCount(1);
        sources[0].LanguageCode.Should().Be("en");
        sources[0].IsImportApproved.Should().BeTrue();
        sources[0].AllowsStudentDisplay.Should().BeTrue();
        sources[0].AllowsCommercialUse.Should().BeTrue();
        // Must never claim to be an external CC-BY/CEFR-J/CMUdict source.
        sources[0].LicenseType.Should().Be("Internal/Original");
    }

    [Fact]
    public async Task Rerunning_the_seeder_does_not_create_a_duplicate_source()
    {
        await RunSeederAsync();
        await RunSeederAsync();

        (await _db.CefrResourceSources.CountAsync(s => s.Name == InternalResourceSeedPackSeeder.SourceName))
            .Should().Be(1);
    }

    // ── Import run + raw record staging ─────────────────────────────────────────

    [Fact]
    public async Task Seed_creates_one_import_run_per_content_group_with_expected_record_counts()
    {
        await RunSeederAsync();

        var source = await _db.CefrResourceSources.FirstAsync(s => s.Name == InternalResourceSeedPackSeeder.SourceName);
        var runs = await _db.ResourceImportRuns.Where(r => r.CefrResourceSourceId == source.Id).ToListAsync();

        // One run per JSON file: vocabulary, grammar, reading references, reading passages.
        runs.Should().HaveCount(4);
        runs.Sum(r => r.TotalRecordCount).Should().Be(32 + 12 + 10 + 10);
        runs.Sum(r => r.SucceededCount).Should().Be(32 + 12 + 10 + 10);
        runs.Sum(r => r.RejectedCount).Should().Be(0);
    }

    [Fact]
    public async Task A_sample_raw_record_carries_the_expected_staged_content()
    {
        await RunSeederAsync();

        var raw = await _db.ResourceRawRecords
            .Where(r => r.RawJson != null && r.RawJson.Contains("\"word\":\"apple\""))
            .FirstOrDefaultAsync();

        raw.Should().NotBeNull();
        raw!.RawJson.Should().Contain("\"cefrLevel\":\"A1\"");
        raw.DetectedLanguageCode.Should().Be("en");
    }

    // ── Deterministic (non-AI) classification at import time ───────────────────

    [Fact]
    public async Task Candidates_have_CefrLevel_PrimarySkill_Subskill_populated_at_creation_from_raw_content()
    {
        await RunSeederAsync();

        var appleCandidate = await _db.ResourceCandidates
            .FirstAsync(c => c.CandidateType == ResourceCandidateType.VocabularyEntry && c.CanonicalText == "apple");

        appleCandidate.CefrLevel.Should().Be("A1");
        appleCandidate.PrimarySkill.Should().Be("vocabulary");
        appleCandidate.Subskill.Should().Be("vocabulary.receptive");

        // The marker proves this came from the Phase E6 deterministic row-mapping path, not from
        // an AI provider's advisory analysis.
        appleCandidate.AiAnalysisJson.Should().Contain("import-row-deterministic-mapping");
        appleCandidate.CefrConfidence.Should().Be(1.0);
    }

    [Fact]
    public async Task Grammar_and_reading_candidates_also_get_deterministic_classification()
    {
        await RunSeederAsync();

        var grammarCandidate = await _db.ResourceCandidates.FirstAsync(
            c => c.CandidateType == ResourceCandidateType.GrammarProfileEntry
                 && c.CanonicalText == "Present Simple for habits");
        grammarCandidate.CefrLevel.Should().Be("A1");
        grammarCandidate.PrimarySkill.Should().Be("grammar");
        grammarCandidate.Subskill.Should().Be("grammar.tense_aspect");

        var readingCandidate = await _db.ResourceCandidates.FirstAsync(
            c => c.CandidateType == ResourceCandidateType.ReadingPassage
                 && c.CanonicalText == "A Morning Routine");
        readingCandidate.CefrLevel.Should().Be("A1");
        readingCandidate.PrimarySkill.Should().Be("reading");
        readingCandidate.Subskill.Should().Be("reading.gist");
    }

    // ── Validation passes deterministically, no AI involved ─────────────────────

    [Fact]
    public async Task All_staged_candidates_validate_as_Passed_without_any_ai_provider_dependency()
    {
        await RunSeederAsync();

        // ResourceCandidateValidationService's constructor (db, IFormIoSchemaValidationService)
        // has no IAiProvider parameter at all — there is no way for this call to reach an AI
        // provider, live or mocked. This assertion proves the *outcome* (Passed); the constructor
        // signature itself is the structural proof of "no AI dependency".
        var candidateIds = await _db.ResourceCandidates.Select(c => c.Id).ToListAsync();
        candidateIds.Should().HaveCount(32 + 12 + 10 + 10);

        foreach (var id in candidateIds)
        {
            var result = await _validationService.ValidateAsync(id);
            result.Status.Should().Be(ResourceCandidateValidationStatus.Passed.ToString(),
                because: $"candidate {id} errors: {string.Join(";", result.Errors)}");
        }
    }

    // ── Publish targets + counts ─────────────────────────────────────────────────

    [Fact]
    public async Task Seed_publishes_every_candidate_to_its_correct_bank_table()
    {
        await RunSeederAsync();

        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(32);
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Grammar)).Should().Be(12);
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.ReadingReference)).Should().Be(10);
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.ReadingPassage)).Should().Be(10);

        var publishedCandidates = await _db.ResourceCandidates.Where(c => c.IsPublished).ToListAsync();
        publishedCandidates.Should().HaveCount(32 + 12 + 10 + 10);
    }

    [Fact]
    public async Task Seeded_reading_passages_are_full_length_and_carry_expected_metadata()
    {
        await RunSeederAsync();

        var passageItems = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.ReadingPassage).ToListAsync();
        var passageItem = passageItems.First(x =>
            ResourceBankItemContent.Deserialize<ReadingPassageContent>(x.ContentJson).Title == "Starting a New Job");
        var passage = ResourceBankItemContent.Deserialize<ReadingPassageContent>(passageItem.ContentJson);

        passageItem.CefrLevel.Should().Be("B1");
        passage.PrimarySkill.Should().Be("reading");
        passage.WordCount.Should().BeGreaterThan(20);
        passage.PassageText.Length.Should().BeGreaterThan(ResourceCandidatePublishService.MaxReadingExcerptLength);
    }

    [Fact]
    public async Task Published_vocabulary_row_has_correctly_mapped_fields()
    {
        await RunSeederAsync();

        var vocabItems = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Vocabulary).ToListAsync();
        var entryItem = vocabItems.First(x =>
            ResourceBankItemContent.Deserialize<VocabularyContent>(x.ContentJson).Word == "collaborate");
        var entry = ResourceBankItemContent.Deserialize<VocabularyContent>(entryItem.ContentJson);

        entryItem.CefrLevel.Should().Be("B2");
        entry.PartOfSpeech.Should().Be("verb");
        entry.Notes.Should().Contain("work together");
    }

    // ── Browse/search via the E5 read surface ───────────────────────────────────

    [Fact]
    public async Task Seeded_vocabulary_is_findable_via_ResourceBankQueryService_search()
    {
        await RunSeederAsync();

        var result = await _queryService.ListVocabularyAsync(new ResourceBankListFilter(SearchText: "collaborate"));

        result.Items.Should().Contain(i => i.Word == "collaborate" && i.CefrLevel == "B2");
    }

    [Fact]
    public async Task Seeded_reading_reference_is_findable_and_within_the_excerpt_limit()
    {
        await RunSeederAsync();

        var result = await _queryService.ListReadingReferencesAsync(new ResourceBankListFilter(CefrLevel: "A1"));

        result.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(
            i => i.ReferenceExcerpt == null || i.ReferenceExcerpt.Length <= ResourceCandidatePublishService.MaxReadingExcerptLength);
    }

    [Fact]
    public async Task Seeded_reading_passage_is_findable_via_ResourceBankQueryService_search()
    {
        await RunSeederAsync();

        var result = await _queryService.ListReadingPassagesAsync(new ResourceBankListFilter(SearchText: "new job"));

        result.Items.Should().Contain(i => i.Title == "Starting a New Job" && i.CefrLevel == "B1");
    }

    // ── Reverse traceability back to candidate/run/source ───────────────────────

    [Fact]
    public async Task Published_vocabulary_entry_traces_back_to_its_candidate_run_and_source()
    {
        await RunSeederAsync();

        var vocabItems = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Vocabulary).ToListAsync();
        var entry = vocabItems.First(x =>
            ResourceBankItemContent.Deserialize<VocabularyContent>(x.ContentJson).Word == "apple");
        var detail = await _queryService.GetVocabularyDetailAsync(entry.Id);

        detail.Should().NotBeNull();
        detail!.Traceability.TraceabilityAvailable.Should().BeTrue();
        detail.Traceability.CandidateId.Should().NotBeNull();
        detail.Traceability.ResourceImportRunId.Should().NotBeNull();
        detail.Source.SourceName.Should().Be(InternalResourceSeedPackSeeder.SourceName);
    }

    // ── Explicit proof: nothing bypasses the publish service ────────────────────

    [Fact]
    public async Task Every_published_bank_row_resolves_back_to_a_ResourceCandidate_marked_published_by_the_publish_workflow()
    {
        // This is the core E6 architectural guarantee: no CefrVocabularyEntry/CefrGrammarProfileEntry/
        // CefrReadingReference row may ever be constructed anywhere except inside
        // ResourceCandidatePublishService. We prove it here indirectly but conclusively: every bank
        // row produced by this seeder must have a matching ResourceCandidate whose IsPublished flag
        // and PublishedEntityId/PublishedEntityType exactly match that row — a hallmark that only
        // ResourceCandidate.MarkPublished (called solely from ResourceCandidatePublishService) can
        // produce. A row inserted by any other path (e.g. a direct DbSet.Add bypass) would have no
        // ResourceCandidate pointing back to it and would fail this assertion.
        await RunSeederAsync();

        var vocabIds = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Vocabulary).Select(v => v.Id).ToListAsync();
        var grammarIds = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Grammar).Select(g => g.Id).ToListAsync();
        var readingIds = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.ReadingReference).Select(r => r.Id).ToListAsync();
        var readingPassageIds = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.ReadingPassage).Select(r => r.Id).ToListAsync();

        vocabIds.Should().NotBeEmpty();
        grammarIds.Should().NotBeEmpty();
        readingIds.Should().NotBeEmpty();
        readingPassageIds.Should().NotBeEmpty();

        foreach (var id in vocabIds)
            await AssertTracesToPublishedCandidate(id, "CefrVocabularyEntry");
        foreach (var id in grammarIds)
            await AssertTracesToPublishedCandidate(id, "CefrGrammarProfileEntry");
        foreach (var id in readingIds)
            await AssertTracesToPublishedCandidate(id, "CefrReadingReference");
        foreach (var id in readingPassageIds)
            await AssertTracesToPublishedCandidate(id, "CefrReadingPassage");
    }

    private async Task AssertTracesToPublishedCandidate(Guid publishedEntityId, string publishedEntityType)
    {
        var candidate = await _db.ResourceCandidates.AsNoTracking().FirstOrDefaultAsync(
            c => c.PublishedEntityId == publishedEntityId && c.PublishedEntityType == publishedEntityType);

        candidate.Should().NotBeNull(because: $"{publishedEntityType} {publishedEntityId} must trace back to the candidate that published it");
        candidate!.IsPublished.Should().BeTrue();
        candidate.PublishedAtUtc.Should().NotBeNull();
    }

    // ── Idempotency: a full second run is a safe no-op ──────────────────────────

    [Fact]
    public async Task Rerunning_the_full_seeder_creates_no_duplicate_rows_anywhere()
    {
        await RunSeederAsync();
        await RunSeederAsync();

        (await _db.CefrResourceSources.CountAsync(s => s.Name == InternalResourceSeedPackSeeder.SourceName)).Should().Be(1);
        (await _db.ResourceImportRuns.CountAsync()).Should().Be(4);
        (await _db.ResourceRawRecords.CountAsync()).Should().Be(32 + 12 + 10 + 10);
        (await _db.ResourceCandidates.CountAsync()).Should().Be(32 + 12 + 10 + 10);
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(32);
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Grammar)).Should().Be(12);
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.ReadingReference)).Should().Be(10);
        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.ReadingPassage)).Should().Be(10);
    }
}
