using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Constants;
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
/// Phase E10 — "Internal Bank Metadata Depth Expansion". Proves the deterministic, idempotent
/// metadata-repair seeder fills the internal lean rows' difficulty band (from CEFR) and focus tag
/// (from subskill) without inserting rows, overwriting existing metadata, touching non-internal or
/// untraceable rows, or introducing invalid subskills/difficulty. Real (non-AI) services against
/// SQLite in-memory, matching the other Resource* test files.
/// </summary>
public sealed class InternalBankMetadataDepthSeederTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ResourceImportService _importService;
    private readonly ResourceCandidateValidationService _validationService;
    private readonly ResourceCandidatePublishService _publishService;
    private readonly ResourceBankQueryService _queryService;

    public InternalBankMetadataDepthSeederTests()
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

    /// <summary>Runs the E8 pack (real pipeline) then the E9 backfill, so the lean rows have
    /// context tags + subskill but no difficulty/focus — the exact pre-E10 state.</summary>
    private async Task SeedE8ThenE9Async()
    {
        await InternalResourceSeedPackE8Seeder.SeedAsync(
            _db, _importService, _validationService, _publishService, NullLogger.Instance);
        await PublishedBankMetadataBackfillSeeder.RunAsync(_db, NullLogger.Instance);
    }

    private Task<int> RunDepthAsync() => InternalBankMetadataDepthSeeder.RunAsync(_db, NullLogger.Instance);

    // ── Enrichment happens; derivations are correct ─────────────────────────────

    [Fact]
    public async Task Enriches_vocabulary_with_derived_difficulty_and_focus_from_cefr_and_subskill()
    {
        await SeedE8ThenE9Async();
        (await RunDepthAsync()).Should().BeGreaterThan(0);

        // "responsible" is B1 vocab with subskill vocabulary.word_form in the E8 pack.
        var entry = await _db.CefrVocabularyEntries.FirstAsync(v => v.Word == "responsible");
        entry.DifficultyBand.Should().Be(3);            // B1 → 3
        entry.FocusTagsJson.Should().NotBeNull();
        entry.FocusTagsJson!.Should().Contain("word_form"); // subskill tail
        entry.Subskill.Should().Be("vocabulary.word_form"); // preserved
        entry.ContextTagsJson!.Should().Contain("workplace"); // preserved
    }

    [Fact]
    public async Task Enriches_grammar_with_derived_difficulty_and_focus()
    {
        await SeedE8ThenE9Async();
        await RunDepthAsync();

        var entry = await _db.CefrGrammarProfileEntries.FirstAsync(g => g.GrammarPoint == "Second conditional");
        entry.DifficultyBand.Should().Be(4);            // B2 → 4
        entry.FocusTagsJson!.Should().Contain("tense_aspect");
        entry.Subskill.Should().Be("grammar.tense_aspect");
    }

    [Fact]
    public async Task Enriches_reading_reference_with_derived_difficulty_and_focus()
    {
        await SeedE8ThenE9Async();
        await RunDepthAsync();

        var entry = await _db.CefrReadingReferences.FirstAsync(r => r.TextType == "announcement" && r.CefrLevel == "B1");
        entry.DifficultyBand.Should().Be(3);
        entry.FocusTagsJson!.Should().NotBeNull();
        entry.Subskill.Should().NotBeNull();
    }

    [Fact]
    public async Task Cefr_maps_to_expected_difficulty_bands_across_levels()
    {
        await SeedE8ThenE9Async();
        await RunDepthAsync();

        var byLevel = await _db.CefrVocabularyEntries
            .GroupBy(v => v.CefrLevel)
            .Select(g => new { Level = g.Key, Band = g.Min(v => v.DifficultyBand) })
            .ToListAsync();

        byLevel.Single(x => x.Level == "A1").Band.Should().Be(1);
        byLevel.Single(x => x.Level == "A2").Band.Should().Be(2);
        byLevel.Single(x => x.Level == "B1").Band.Should().Be(3);
        byLevel.Single(x => x.Level == "B2").Band.Should().Be(4);
    }

    // ── Safety: idempotent, no inserts, no overwrite ────────────────────────────

    [Fact]
    public async Task Is_idempotent_second_run_is_a_no_op()
    {
        await SeedE8ThenE9Async();
        (await RunDepthAsync()).Should().BeGreaterThan(0);
        (await RunDepthAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Never_inserts_new_bank_rows()
    {
        await SeedE8ThenE9Async();
        var before = await _db.CefrVocabularyEntries.CountAsync()
            + await _db.CefrGrammarProfileEntries.CountAsync()
            + await _db.CefrReadingReferences.CountAsync();

        await RunDepthAsync();

        var after = await _db.CefrVocabularyEntries.CountAsync()
            + await _db.CefrGrammarProfileEntries.CountAsync()
            + await _db.CefrReadingReferences.CountAsync();
        after.Should().Be(before);
    }

    [Fact]
    public async Task Does_not_overwrite_an_existing_difficulty_band_or_focus_tag()
    {
        await SeedE8ThenE9Async();

        // Pre-set richer metadata on one row to simulate authored/enriched content.
        var entry = await _db.CefrVocabularyEntries.FirstAsync(v => v.Word == "responsible");
        entry.SetSelectionMetadata(entry.Subskill, 5, entry.ContextTagsJson, "[\"custom_focus\"]");
        await _db.SaveChangesAsync();

        await RunDepthAsync();

        var reloaded = await _db.CefrVocabularyEntries.FirstAsync(v => v.Word == "responsible");
        reloaded.DifficultyBand.Should().Be(5);              // not overwritten to 3
        reloaded.FocusTagsJson!.Should().Contain("custom_focus"); // not overwritten
    }

    // ── Safety: skip non-internal + untraceable rows ────────────────────────────

    [Fact]
    public async Task Skips_a_non_internal_source_row()
    {
        var external = new CefrResourceSource("External Dataset", "CC-BY-4.0",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        external.ApproveForImport();
        _db.CefrResourceSources.Add(external);
        await _db.SaveChangesAsync();

        var row = new CefrVocabularyEntry(external.Id, "externalword", "B1");
        row.SetSelectionMetadata("vocabulary.receptive", null, "[\"general\"]", "[]");
        _db.CefrVocabularyEntries.Add(row);
        await _db.SaveChangesAsync();

        // Even a matching published candidate should not make a non-internal row eligible.
        await AddPublishedCandidateAsync(external.Id, nameof(CefrVocabularyEntry), row.Id, "vocabulary.receptive");

        (await RunDepthAsync()).Should().Be(0);
        (await _db.CefrVocabularyEntries.FirstAsync(v => v.Word == "externalword")).DifficultyBand.Should().BeNull();
    }

    [Fact]
    public async Task Skips_an_internal_row_with_no_traceable_candidate()
    {
        var internalSource = new CefrResourceSource("Internal Orphan", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        internalSource.ApproveForImport();
        _db.CefrResourceSources.Add(internalSource);
        await _db.SaveChangesAsync();

        var orphan = new CefrVocabularyEntry(internalSource.Id, "orphanword", "B1");
        orphan.SetSelectionMetadata("vocabulary.receptive", null, "[\"general\"]", "[]");
        _db.CefrVocabularyEntries.Add(orphan);
        await _db.SaveChangesAsync(); // no ResourceCandidate points at it

        (await RunDepthAsync()).Should().Be(0);
        (await _db.CefrVocabularyEntries.FirstAsync(v => v.Word == "orphanword")).DifficultyBand.Should().BeNull();
    }

    [Fact]
    public async Task Skips_an_internal_row_with_an_ambiguous_multi_candidate_match()
    {
        var internalSource = new CefrResourceSource("Internal Ambiguous", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        internalSource.ApproveForImport();
        _db.CefrResourceSources.Add(internalSource);
        await _db.SaveChangesAsync();

        var row = new CefrVocabularyEntry(internalSource.Id, "ambiguousword", "B1");
        row.SetSelectionMetadata("vocabulary.receptive", null, "[\"general\"]", "[]");
        _db.CefrVocabularyEntries.Add(row);
        await _db.SaveChangesAsync();

        await AddPublishedCandidateAsync(internalSource.Id, nameof(CefrVocabularyEntry), row.Id, "vocabulary.receptive");
        await AddPublishedCandidateAsync(internalSource.Id, nameof(CefrVocabularyEntry), row.Id, "vocabulary.receptive");

        (await RunDepthAsync()).Should().Be(0);
        (await _db.CefrVocabularyEntries.FirstAsync(v => v.Word == "ambiguousword")).DifficultyBand.Should().BeNull();
    }

    // ── Only valid taxonomy values / bands ──────────────────────────────────────

    [Fact]
    public async Task Only_uses_valid_subskills_and_difficulty_bands()
    {
        await SeedE8ThenE9Async();
        await RunDepthAsync();

        var validSubskills = new[] { "vocabulary", "grammar", "reading" }; // prefixes present in packs
        (await _db.CefrVocabularyEntries.Where(v => v.Subskill != null).Select(v => v.Subskill!).ToListAsync())
            .Should().OnlyContain(s => CurriculumSubskillConstants.IsValid(s));
        (await _db.CefrVocabularyEntries.Where(v => v.DifficultyBand != null).Select(v => v.DifficultyBand!.Value).ToListAsync())
            .Should().OnlyContain(b => b >= 1 && b <= 5);
        validSubskills.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Context_tags_are_preserved_after_enrichment()
    {
        await SeedE8ThenE9Async();
        await RunDepthAsync();

        var entry = await _db.CefrVocabularyEntries.FirstAsync(v => v.Word == "sunshine" || v.Word == "responsible");
        entry.ContextTagsJson.Should().NotBeNull();
        entry.ContextTagsJson!.Trim().Should().NotBe("[]");
    }

    // ── Discoverability of the enriched metadata via the query filters ──────────

    [Fact]
    public async Task Enriched_vocabulary_is_filterable_by_derived_difficulty_and_focus()
    {
        await SeedE8ThenE9Async();
        await RunDepthAsync();

        // B1 vocab now carries difficulty 3.
        var byDifficulty = await _queryService.ListVocabularyAsync(new ResourceBankListFilter(DifficultyBand: 3, CefrLevel: "B1"));
        byDifficulty.Items.Should().NotBeEmpty();
        byDifficulty.Items.Should().OnlyContain(i => i.DifficultyBand == 3);

        // A focus tag derived from a subskill tail present in the pack.
        var byFocus = await _queryService.ListVocabularyAsync(new ResourceBankListFilter(FocusTag: "collocation"));
        byFocus.Items.Should().NotBeEmpty();
        byFocus.Items.Should().OnlyContain(i => i.FocusTags != null && i.FocusTags.Contains("collocation"));
    }

    [Fact]
    public async Task Enriched_grammar_is_filterable_by_derived_difficulty()
    {
        await SeedE8ThenE9Async();
        await RunDepthAsync();

        var result = await _queryService.ListGrammarAsync(new ResourceBankListFilter(DifficultyBand: 4, CefrLevel: "B2"));
        result.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(i => i.DifficultyBand == 4);
    }

    [Fact]
    public async Task Enriched_reading_reference_is_filterable_by_derived_difficulty()
    {
        await SeedE8ThenE9Async();
        await RunDepthAsync();

        var result = await _queryService.ListReadingReferencesAsync(new ResourceBankListFilter(DifficultyBand: 2, CefrLevel: "A2"));
        result.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(i => i.DifficultyBand == 2);
    }

    [Fact]
    public async Task Unfiltered_browse_remains_backward_compatible()
    {
        await SeedE8ThenE9Async();
        var before = (await _queryService.ListVocabularyAsync(new ResourceBankListFilter(PageSize: 200))).TotalCount;

        await RunDepthAsync();

        var after = (await _queryService.ListVocabularyAsync(new ResourceBankListFilter(PageSize: 200))).TotalCount;
        after.Should().Be(before); // enrichment changes metadata, not row visibility/count
    }

    // ── Coverage improvement + English-only ─────────────────────────────────────

    [Fact]
    public async Task Most_internal_lean_rows_have_difficulty_and_focus_after_enrichment()
    {
        await SeedE8ThenE9Async();

        var totalLean = await _db.CefrVocabularyEntries.CountAsync()
            + await _db.CefrGrammarProfileEntries.CountAsync()
            + await _db.CefrReadingReferences.CountAsync();
        (await _db.CefrVocabularyEntries.CountAsync(v => v.DifficultyBand != null)).Should().Be(0); // pre-E10

        await RunDepthAsync();

        var withDifficulty = await _db.CefrVocabularyEntries.CountAsync(v => v.DifficultyBand != null)
            + await _db.CefrGrammarProfileEntries.CountAsync(g => g.DifficultyBand != null)
            + await _db.CefrReadingReferences.CountAsync(r => r.DifficultyBand != null);
        var withFocus = await _db.CefrVocabularyEntries.CountAsync(v => v.FocusTagsJson != null && v.FocusTagsJson != "[]")
            + await _db.CefrGrammarProfileEntries.CountAsync(g => g.FocusTagsJson != null && g.FocusTagsJson != "[]")
            + await _db.CefrReadingReferences.CountAsync(r => r.FocusTagsJson != null && r.FocusTagsJson != "[]");

        withDifficulty.Should().Be(totalLean); // every internal lean row now carries difficulty
        withFocus.Should().Be(totalLean);      // and a focus tag derived from its subskill
    }

    [Fact]
    public async Task Enriched_metadata_is_ascii_english_only()
    {
        await SeedE8ThenE9Async();
        await RunDepthAsync();

        var focusValues = await _db.CefrVocabularyEntries
            .Where(v => v.FocusTagsJson != null)
            .Select(v => v.FocusTagsJson!)
            .ToListAsync();
        focusValues.Should().OnlyContain(f =>
            System.Text.RegularExpressions.Regex.IsMatch(f, "^[\\x00-\\x7F]*$"));
    }

    [Fact]
    public async Task Survives_repeated_e8_and_depth_application()
    {
        await SeedE8ThenE9Async();
        await RunDepthAsync();

        // Re-running the E8 seeder is a no-op (source exists); re-running depth is a no-op.
        await InternalResourceSeedPackE8Seeder.SeedAsync(
            _db, _importService, _validationService, _publishService, NullLogger.Instance);
        (await RunDepthAsync()).Should().Be(0);

        (await _db.CefrVocabularyEntries.FirstAsync(v => v.Word == "responsible")).DifficultyBand.Should().Be(3);
    }

    // ── Helper: attach a published candidate to a bank row via the real staging chain ──

    private async Task AddPublishedCandidateAsync(Guid sourceId, string publishedEntityType, Guid publishedEntityId, string subskill)
    {
        var run = new ResourceImportRun(sourceId, ResourceImportMode.Json, "f.json", $"h-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        _db.ResourceImportRuns.Add(run);
        await _db.SaveChangesAsync();
        var raw = new ResourceRawRecord(run.Id, $"rh-{Guid.NewGuid():N}", "en", "row", rawJson: "{}");
        _db.ResourceRawRecords.Add(raw);
        await _db.SaveChangesAsync();

        var c = new ResourceCandidate(
            raw.Id, ResourceCandidateType.VocabularyEntry, "term", "{}", "en", "term",
            $"fp-{Guid.NewGuid():N}", ResourceCandidateValidationStatus.Passed);
        c.ApplyAnalysis("{}", "B1", 1.0, "vocabulary", subskill, null, "[\"general\"]", "[]", null, null, null, null, null, null, null);
        c.MarkPublished(publishedEntityType, publishedEntityId, DateTimeOffset.UtcNow, null);
        _db.ResourceCandidates.Add(c);
        await _db.SaveChangesAsync();
    }
}
