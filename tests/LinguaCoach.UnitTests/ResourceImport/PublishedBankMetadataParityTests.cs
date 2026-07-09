using FluentAssertions;
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
/// Phase E9 — "Published Bank Metadata Parity for Context-Aware Selection". Proves the publish
/// mapping now carries context/focus/subskill/difficulty metadata onto the lean Cefr* bank rows,
/// that the idempotent backfill repairs pre-E9 rows only where safely traceable, and that
/// CefrReadingPassage's existing metadata does not regress. Real (non-AI) services against SQLite
/// in-memory, matching the other Resource* test files.
/// </summary>
public sealed class PublishedBankMetadataParityTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ResourceImportService _importService;
    private readonly ResourceCandidateValidationService _validationService;
    private readonly ResourceCandidatePublishService _publishService;

    public PublishedBankMetadataParityTests()
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
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private Task RunE8SeederAsync() => InternalResourceSeedPackE8Seeder.SeedAsync(
        _db, _importService, _validationService, _publishService, NullLogger.Instance);

    // ── Publish mapping carries metadata onto the lean tables ───────────────────

    [Fact]
    public async Task Publish_maps_context_subskill_metadata_onto_vocabulary_rows()
    {
        await RunE8SeederAsync();

        // E8 "responsible" is B1 workplace vocab with subskill vocabulary.word_form.
        var entry = await _db.CefrVocabularyEntries.FirstAsync(v => v.Word == "responsible");
        entry.Subskill.Should().Be("vocabulary.word_form");
        entry.ContextTagsJson.Should().NotBeNull();
        entry.ContextTagsJson!.Should().Contain("workplace");
    }

    [Fact]
    public async Task Publish_maps_context_subskill_metadata_onto_grammar_rows()
    {
        await RunE8SeederAsync();

        var entry = await _db.CefrGrammarProfileEntries.FirstAsync(g => g.GrammarPoint == "Second conditional");
        entry.Subskill.Should().Be("grammar.tense_aspect");
        entry.ContextTagsJson.Should().NotBeNull();
        entry.ContextTagsJson!.Should().Contain("study");
    }

    [Fact]
    public async Task Publish_maps_context_subskill_metadata_onto_reading_reference_rows()
    {
        await RunE8SeederAsync();

        var entry = await _db.CefrReadingReferences.FirstAsync(r => r.TextType == "announcement" && r.CefrLevel == "B1");
        entry.Subskill.Should().NotBeNull();
        entry.ContextTagsJson.Should().NotBeNull();
        entry.ContextTagsJson!.Should().Contain("social");
    }

    [Fact]
    public async Task Publish_does_not_regress_full_passage_metadata()
    {
        await RunE8SeederAsync();

        var passage = await _db.CefrReadingPassages.FirstAsync(p => p.Title == "A Visit to the Zoo");
        passage.DifficultyBand.Should().Be(1);
        passage.ContextTagsJson.Should().NotBeNull();
        passage.FocusTagsJson!.Should().Contain("main_idea");
    }

    [Fact]
    public async Task Lean_tables_do_not_invent_metadata_the_candidate_never_carried()
    {
        // The E6/E7 pack authored vocabulary rows with context tags + subskill but no difficulty
        // band and no focus tags; difficulty stays null and focus tags stay the empty array the
        // candidate carried — nothing is invented.
        await InternalResourceSeedPackSeeder.SeedAsync(
            _db, _importService, _validationService, _publishService, NullLogger.Instance);

        var vocab = await _db.CefrVocabularyEntries.FirstAsync(v => v.Word == "apple");
        vocab.DifficultyBand.Should().BeNull();
        vocab.FocusTagsJson.Should().Be("[]"); // empty, not fabricated
        // The context tag and subskill that WERE authored are retained.
        vocab.ContextTagsJson.Should().NotBeNull();
        vocab.ContextTagsJson!.Should().Contain("everyday");
        vocab.Subskill.Should().Be("vocabulary.receptive");
    }

    // ── Entity metadata contract ────────────────────────────────────────────────

    [Fact]
    public void SetSelectionMetadata_rejects_out_of_range_difficulty_band()
    {
        var entry = new CefrVocabularyEntry(Guid.NewGuid(), "word", "B1");
        var act = () => entry.SetSelectionMetadata("vocabulary.receptive", 6, "[\"general\"]", "[]");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── Backfill: repairs pre-E9 rows, idempotent, never guesses ────────────────

    [Fact]
    public async Task Backfill_restores_metadata_on_lean_rows_that_lost_it()
    {
        await RunE8SeederAsync();

        // Simulate pre-E9 rows: strip the selection metadata off every lean row.
        foreach (var v in await _db.CefrVocabularyEntries.ToListAsync()) v.SetSelectionMetadata(null, null, null, null);
        foreach (var g in await _db.CefrGrammarProfileEntries.ToListAsync()) g.SetSelectionMetadata(null, null, null, null);
        foreach (var r in await _db.CefrReadingReferences.ToListAsync()) r.SetSelectionMetadata(null, null, null, null);
        await _db.SaveChangesAsync();

        var backfilled = await PublishedBankMetadataBackfillSeeder.RunAsync(_db, NullLogger.Instance);

        backfilled.Should().BeGreaterThan(0);
        (await _db.CefrVocabularyEntries.FirstAsync(v => v.Word == "responsible")).ContextTagsJson.Should().Contain("workplace");
        (await _db.CefrGrammarProfileEntries.FirstAsync(g => g.GrammarPoint == "Second conditional")).Subskill.Should().Be("grammar.tense_aspect");
        (await _db.CefrReadingReferences.FirstAsync(r => r.TextType == "announcement" && r.CefrLevel == "B1")).ContextTagsJson.Should().Contain("social");
    }

    [Fact]
    public async Task Backfill_is_idempotent_and_does_not_overwrite_existing_metadata()
    {
        await RunE8SeederAsync();

        // Rows already carry metadata from publish → first backfill is a no-op.
        var firstRun = await PublishedBankMetadataBackfillSeeder.RunAsync(_db, NullLogger.Instance);
        firstRun.Should().Be(0);

        // A second run after stripping-then-restoring must also be a stable no-op the third time.
        foreach (var v in await _db.CefrVocabularyEntries.ToListAsync()) v.SetSelectionMetadata(null, null, null, null);
        await _db.SaveChangesAsync();
        (await PublishedBankMetadataBackfillSeeder.RunAsync(_db, NullLogger.Instance)).Should().BeGreaterThan(0);
        (await PublishedBankMetadataBackfillSeeder.RunAsync(_db, NullLogger.Instance)).Should().Be(0);
    }

    [Fact]
    public async Task Backfill_does_not_touch_a_row_with_no_traceable_candidate()
    {
        var source = new CefrResourceSource("Untraceable", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        _db.CefrResourceSources.Add(source);
        await _db.SaveChangesAsync();

        // A bank row with no ResourceCandidate pointing at it (e.g. legacy/other origin).
        var orphan = new CefrVocabularyEntry(source.Id, "orphanword", "B1");
        _db.CefrVocabularyEntries.Add(orphan);
        await _db.SaveChangesAsync();

        var backfilled = await PublishedBankMetadataBackfillSeeder.RunAsync(_db, NullLogger.Instance);

        backfilled.Should().Be(0);
        (await _db.CefrVocabularyEntries.FirstAsync(v => v.Word == "orphanword")).ContextTagsJson.Should().BeNull();
    }

    [Fact]
    public async Task Backfill_skips_a_row_with_an_ambiguous_multi_candidate_match()
    {
        var source = new CefrResourceSource("Ambiguous", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        _db.CefrResourceSources.Add(source);
        await _db.SaveChangesAsync();

        var row = new CefrVocabularyEntry(source.Id, "ambiguousword", "B1");
        _db.CefrVocabularyEntries.Add(row);

        // Minimal real staging chain so the candidates satisfy their FK to a raw record.
        var run = new ResourceImportRun(source.Id, ResourceImportMode.Json, "f.json", "hash", DateTimeOffset.UtcNow);
        _db.ResourceImportRuns.Add(run);
        await _db.SaveChangesAsync();
        var raw = new ResourceRawRecord(run.Id, "rawhash", "en", "row", rawJson: "{}");
        _db.ResourceRawRecords.Add(raw);
        await _db.SaveChangesAsync();

        // Two published candidates both claim the same bank row → ambiguous, must not be guessed.
        for (var i = 0; i < 2; i++)
        {
            var c = new ResourceCandidate(
                raw.Id, ResourceCandidateType.VocabularyEntry, "ambiguousword", "{}", "en",
                "ambiguousword", $"fp-{i}", ResourceCandidateValidationStatus.Passed);
            c.ApplyAnalysis("{}", "B1", 1.0, "vocabulary", "vocabulary.receptive", null,
                "[\"workplace\"]", "[]", null, null, null, null, null, null, null);
            c.MarkPublished(nameof(CefrVocabularyEntry), row.Id, DateTimeOffset.UtcNow, null);
            _db.ResourceCandidates.Add(c);
        }
        await _db.SaveChangesAsync();

        var backfilled = await PublishedBankMetadataBackfillSeeder.RunAsync(_db, NullLogger.Instance);

        backfilled.Should().Be(0);
        (await _db.CefrVocabularyEntries.FirstAsync(v => v.Word == "ambiguousword")).ContextTagsJson.Should().BeNull();
    }

    // ── Pipeline integrity: publish still traces back through the workflow ───────

    [Fact]
    public async Task Published_rows_still_trace_back_to_a_published_candidate_no_direct_seeding()
    {
        await RunE8SeederAsync();

        var vocabId = await _db.CefrVocabularyEntries.Select(v => v.Id).FirstAsync();
        var candidate = await _db.ResourceCandidates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublishedEntityType == nameof(CefrVocabularyEntry) && c.PublishedEntityId == vocabId);

        candidate.Should().NotBeNull();
        candidate!.IsPublished.Should().BeTrue();
    }
}
