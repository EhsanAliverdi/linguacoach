using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase H1 — proves the unified Resource Bank read model (ResourceBankQueryService.ListUnifiedAsync)
/// correctly aggregates all four typed published bank tables (CefrVocabularyEntry/
/// CefrGrammarProfileEntry/CefrReadingReference/CefrReadingPassage), applies the shared type/CEFR/
/// subskill/context/focus/difficulty/search filters, never leaks staging-only ResourceCandidate rows,
/// and never crashes on missing metadata. Uses directly-constructed bank fixtures — same convention
/// as ResourceBankMetadataFilterTests — against SQLite in-memory.
/// </summary>
public sealed class UnifiedResourceBankQueryServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ResourceBankQueryService _query;
    private readonly Guid _sourceId;

    public UnifiedResourceBankQueryServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var source = new CefrResourceSource("Unified Filter Test Source", "Internal/Original",
            allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        _db.CefrResourceSources.Add(source);
        _sourceId = source.Id;
        _db.SaveChanges();

        _query = new ResourceBankQueryService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private CefrVocabularyEntry SeedVocab(
        string word, string cefr = "B1", string? subskill = null, int? difficulty = null,
        string? contextTagsJson = null, string? focusTagsJson = null)
    {
        var e = new CefrVocabularyEntry(_sourceId, word, cefr);
        e.SetSelectionMetadata(subskill, difficulty, contextTagsJson, focusTagsJson);
        _db.CefrVocabularyEntries.Add(e);
        return e;
    }

    private CefrGrammarProfileEntry SeedGrammar(
        string point, string cefr = "B1", string? subskill = null, int? difficulty = null,
        string? contextTagsJson = null, string? focusTagsJson = null)
    {
        var e = new CefrGrammarProfileEntry(_sourceId, cefr, point);
        e.SetSelectionMetadata(subskill, difficulty, contextTagsJson, focusTagsJson);
        _db.CefrGrammarProfileEntries.Add(e);
        return e;
    }

    private CefrReadingReference SeedReference(
        string textType, string cefr = "B1", string? subskill = null, int? difficulty = null,
        string? contextTagsJson = null, string? focusTagsJson = null)
    {
        var e = new CefrReadingReference(_sourceId, cefr, textType: textType, referenceExcerpt: "a short excerpt");
        e.SetSelectionMetadata(subskill, difficulty, contextTagsJson, focusTagsJson);
        _db.CefrReadingReferences.Add(e);
        return e;
    }

    private CefrReadingPassage SeedPassage(
        string title, string cefr = "B1", string? subskill = null, int? difficulty = null,
        string? contextTagsJson = null, string? focusTagsJson = null)
    {
        var e = new CefrReadingPassage(
            _sourceId, title, "Passage text with enough words to be realistic for a reading exercise.",
            cefr, difficultyBand: difficulty, subskill: subskill, contextTagsJson: contextTagsJson, focusTagsJson: focusTagsJson);
        _db.CefrReadingPassages.Add(e);
        return e;
    }

    // ── Aggregation across all four tables ────────────────────────────────────────

    [Fact]
    public async Task Unified_returns_vocabulary_rows()
    {
        SeedVocab("garden");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter());

        result.Items.Should().ContainSingle(i => i.Type == UnifiedResourceBankItemType.Vocabulary && i.Title == "garden");
    }

    [Fact]
    public async Task Unified_returns_grammar_rows()
    {
        SeedGrammar("present perfect");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter());

        result.Items.Should().ContainSingle(i => i.Type == UnifiedResourceBankItemType.Grammar && i.Title == "present perfect");
    }

    [Fact]
    public async Task Unified_returns_reading_reference_rows()
    {
        SeedReference("narrative");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter());

        result.Items.Should().ContainSingle(i => i.Type == UnifiedResourceBankItemType.ReadingReference && i.Title == "narrative");
    }

    [Fact]
    public async Task Unified_returns_reading_passage_rows()
    {
        SeedPassage("A Trip to the Market");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter());

        result.Items.Should().ContainSingle(i =>
            i.Type == UnifiedResourceBankItemType.ReadingPassage && i.Title == "A Trip to the Market");
    }

    [Fact]
    public async Task Unified_aggregates_all_four_types_in_one_call()
    {
        SeedVocab("garden");
        SeedGrammar("present perfect");
        SeedReference("narrative");
        SeedPassage("A Trip to the Market");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter(PageSize: 200));

        result.TotalCount.Should().Be(4);
        result.Items.Select(i => i.Type).Should().BeEquivalentTo(new[]
        {
            UnifiedResourceBankItemType.Vocabulary, UnifiedResourceBankItemType.Grammar,
            UnifiedResourceBankItemType.ReadingReference, UnifiedResourceBankItemType.ReadingPassage
        });
    }

    // ── Filters ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Type_filter_returns_only_the_requested_type()
    {
        SeedVocab("garden");
        SeedGrammar("present perfect");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter(Type: UnifiedResourceBankItemType.Grammar));

        result.Items.Should().ContainSingle().Which.Type.Should().Be(UnifiedResourceBankItemType.Grammar);
    }

    [Fact]
    public async Task Cefr_filter_returns_only_matching_level()
    {
        SeedVocab("garden", cefr: "A1");
        SeedVocab("negotiate", cefr: "C1");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter(CefrLevel: "C1"));

        result.Items.Should().ContainSingle().Which.Title.Should().Be("negotiate");
    }

    [Fact]
    public async Task Skill_filter_returns_only_matching_skill()
    {
        SeedVocab("garden");
        SeedGrammar("present perfect");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter(Skill: "Grammar"));

        result.Items.Should().ContainSingle().Which.Type.Should().Be(UnifiedResourceBankItemType.Grammar);
    }

    [Fact]
    public async Task Context_tag_filter_works_across_types()
    {
        SeedVocab("meeting", contextTagsJson: "[\"workplace\"]");
        SeedVocab("garden", contextTagsJson: "[\"general\"]");
        SeedGrammar("phrasal verbs", contextTagsJson: "[\"general\"]");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter(ContextTag: "general", PageSize: 200));

        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => i.Title != "meeting");
    }

    [Fact]
    public async Task Focus_tag_filter_works()
    {
        SeedVocab("garden", focusTagsJson: "[\"collocation\"]");
        SeedVocab("negotiate", focusTagsJson: "[\"idiom\"]");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter(FocusTag: "idiom"));

        result.Items.Should().ContainSingle().Which.Title.Should().Be("negotiate");
    }

    [Fact]
    public async Task Subskill_filter_works()
    {
        SeedVocab("garden", subskill: "vocabulary.receptive");
        SeedVocab("negotiate", subskill: "vocabulary.productive");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter(Subskill: "vocabulary.productive"));

        result.Items.Should().ContainSingle().Which.Title.Should().Be("negotiate");
    }

    [Fact]
    public async Task Difficulty_band_filter_works()
    {
        SeedVocab("garden", difficulty: 1);
        SeedVocab("negotiate", difficulty: 4);
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter(DifficultyBand: 4));

        result.Items.Should().ContainSingle().Which.Title.Should().Be("negotiate");
    }

    [Fact]
    public async Task Search_matches_across_reasonable_display_fields()
    {
        SeedVocab("negotiate");
        SeedGrammar("present perfect");
        SeedPassage("A Trip to the Market");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter(SearchText: "negot"));

        result.Items.Should().ContainSingle().Which.Title.Should().Be("negotiate");
    }

    // ── Robustness ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Missing_metadata_does_not_crash_and_row_still_appears()
    {
        // No subskill/difficulty/context/focus set at all (pre-E9-style row).
        _db.CefrVocabularyEntries.Add(new CefrVocabularyEntry(_sourceId, "plainword", "B1"));
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter());

        result.Items.Should().ContainSingle().Which.Title.Should().Be("plainword");
        result.Items[0].Subskill.Should().BeNull();
        result.Items[0].DifficultyBand.Should().BeNull();
        result.Items[0].ContextTags.Should().BeEmpty();
        result.Items[0].FocusTags.Should().BeEmpty();
    }

    [Fact]
    public async Task Staging_only_candidates_never_appear_in_the_unified_bank()
    {
        // A raw ResourceCandidate with no corresponding published bank row must never surface here —
        // ListUnifiedAsync only ever reads the four typed published tables, never ResourceCandidate.
        var run = new ResourceImportRun(
            _sourceId, ResourceImportMode.Csv, "test.csv", "filehash-unified-1", DateTimeOffset.UtcNow);
        _db.ResourceImportRuns.Add(run);
        _db.SaveChanges();

        var raw = new ResourceRawRecord(run.Id, "rawhash-unified-1", "en", "row", rawJson: "{\"word\":\"unpublished\"}");
        raw.MarkParsed();
        _db.ResourceRawRecords.Add(raw);
        _db.SaveChanges();

        var candidate = new ResourceCandidate(
            raw.Id, ResourceCandidateType.VocabularyEntry, "unpublished", "{\"word\":\"unpublished\"}",
            "en", "unpublished", "fingerprint-unified-1", ResourceCandidateValidationStatus.NeedsReview);
        _db.ResourceCandidates.Add(candidate);
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter());

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Default_ordering_is_stable_across_repeated_calls()
    {
        SeedVocab("banana");
        SeedVocab("apple");
        SeedGrammar("present perfect");
        _db.SaveChanges();

        var first = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter(PageSize: 200));
        var second = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter(PageSize: 200));

        first.Items.Select(i => i.Id).Should().Equal(second.Items.Select(i => i.Id));
    }
}
