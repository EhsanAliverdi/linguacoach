using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase E9 — proves ResourceBankQueryService can filter the lean bank tables by the new published
/// selection metadata (context tag, focus tag, subskill, difficulty band), that unfiltered browse
/// stays backward-compatible, and that a general-context filter does not return workplace rows.
/// Uses directly-constructed bank fixtures (the same test convention as
/// TodayBankResourceSelectorTests) against SQLite in-memory.
/// </summary>
public sealed class ResourceBankMetadataFilterTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ResourceBankQueryService _query;
    private readonly Guid _sourceId;

    public ResourceBankMetadataFilterTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var source = new CefrResourceSource("Filter Test Source", "Internal/Original",
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

    private void SeedVocab(string word, string subskill, int? difficulty, string contextTagsJson, string focusTagsJson = "[]")
    {
        var e = new CefrVocabularyEntry(_sourceId, word, "B1");
        e.SetSelectionMetadata(subskill, difficulty, contextTagsJson, focusTagsJson);
        _db.CefrVocabularyEntries.Add(e);
    }

    private void SeedGrammar(string point, string subskill, string contextTagsJson)
    {
        var e = new CefrGrammarProfileEntry(_sourceId, "B1", point);
        e.SetSelectionMetadata(subskill, null, contextTagsJson, "[]");
        _db.CefrGrammarProfileEntries.Add(e);
    }

    private void SeedReference(string textType, string subskill, string contextTagsJson)
    {
        var e = new CefrReadingReference(_sourceId, "B1", textType: textType, referenceExcerpt: "excerpt");
        e.SetSelectionMetadata(subskill, null, contextTagsJson, "[]");
        _db.CefrReadingReferences.Add(e);
    }

    // ── Vocabulary filters ──────────────────────────────────────────────────────

    [Fact]
    public async Task Vocabulary_can_be_filtered_by_context_tag()
    {
        SeedVocab("meeting", "vocabulary.receptive", 3, "[\"workplace\"]");
        SeedVocab("garden", "vocabulary.receptive", 2, "[\"general\",\"daily\"]");
        _db.SaveChanges();

        var result = await _query.ListVocabularyAsync(new ResourceBankListFilter(ContextTag: "general"));

        result.Items.Should().ContainSingle().Which.Word.Should().Be("garden");
    }

    [Fact]
    public async Task Vocabulary_context_filter_does_not_return_workplace_for_a_general_request()
    {
        SeedVocab("meeting", "vocabulary.receptive", 3, "[\"workplace\"]");
        SeedVocab("garden", "vocabulary.receptive", 2, "[\"general\"]");
        _db.SaveChanges();

        var result = await _query.ListVocabularyAsync(new ResourceBankListFilter(ContextTag: "general"));

        result.Items.Should().OnlyContain(i => i.Word != "meeting");
    }

    [Fact]
    public async Task Vocabulary_context_filter_uses_quoted_token_to_avoid_substring_false_positive()
    {
        SeedVocab("meeting", "vocabulary.receptive", 3, "[\"workplace\"]");
        _db.SaveChanges();

        // "work" must NOT match "workplace" (the filter matches the quoted token "work").
        var result = await _query.ListVocabularyAsync(new ResourceBankListFilter(ContextTag: "work"));

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Vocabulary_can_be_filtered_by_focus_tag()
    {
        SeedVocab("word1", "vocabulary.receptive", 3, "[\"general\"]", "[\"collocation\"]");
        SeedVocab("word2", "vocabulary.receptive", 3, "[\"general\"]", "[\"word_form\"]");
        _db.SaveChanges();

        var result = await _query.ListVocabularyAsync(new ResourceBankListFilter(FocusTag: "collocation"));

        result.Items.Should().ContainSingle().Which.Word.Should().Be("word1");
    }

    [Fact]
    public async Task Vocabulary_can_be_filtered_by_subskill_and_difficulty_band()
    {
        SeedVocab("easy", "vocabulary.receptive", 1, "[\"general\"]");
        SeedVocab("hard", "vocabulary.productive", 4, "[\"general\"]");
        _db.SaveChanges();

        (await _query.ListVocabularyAsync(new ResourceBankListFilter(Subskill: "vocabulary.productive")))
            .Items.Should().ContainSingle().Which.Word.Should().Be("hard");

        (await _query.ListVocabularyAsync(new ResourceBankListFilter(DifficultyBand: 1)))
            .Items.Should().ContainSingle().Which.Word.Should().Be("easy");
    }

    // ── Grammar + reading-reference filters ─────────────────────────────────────

    [Fact]
    public async Task Grammar_can_be_filtered_by_context_tag()
    {
        SeedGrammar("Passive voice", "grammar.tense_aspect", "[\"workplace\"]");
        SeedGrammar("First conditional", "grammar.tense_aspect", "[\"general\"]");
        _db.SaveChanges();

        var result = await _query.ListGrammarAsync(new ResourceBankListFilter(ContextTag: "general"));

        result.Items.Should().ContainSingle().Which.GrammarPoint.Should().Be("First conditional");
    }

    [Fact]
    public async Task Reading_reference_can_be_filtered_by_context_tag()
    {
        SeedReference("memo", "reading.detail", "[\"workplace\"]");
        SeedReference("note", "reading.gist", "[\"social\",\"daily\"]");
        _db.SaveChanges();

        var result = await _query.ListReadingReferencesAsync(new ResourceBankListFilter(ContextTag: "social"));

        result.Items.Should().ContainSingle().Which.TextType.Should().Be("note");
    }

    // ── Detail DTO + backward compatibility ─────────────────────────────────────

    [Fact]
    public async Task Detail_dto_exposes_the_new_metadata_read_only()
    {
        SeedVocab("meeting", "vocabulary.collocation", 3, "[\"workplace\"]", "[\"collocation\"]");
        _db.SaveChanges();
        var id = _db.CefrVocabularyEntries.Single().Id;

        var detail = await _query.GetVocabularyDetailAsync(id);

        detail.Should().NotBeNull();
        detail!.Subskill.Should().Be("vocabulary.collocation");
        detail.DifficultyBand.Should().Be(3);
        detail.ContextTags.Should().Contain("workplace");
        detail.FocusTags.Should().Contain("collocation");
    }

    [Fact]
    public async Task Unfiltered_browse_still_returns_all_rows_including_those_without_metadata()
    {
        SeedVocab("withmeta", "vocabulary.receptive", 3, "[\"general\"]");
        _db.CefrVocabularyEntries.Add(new CefrVocabularyEntry(_sourceId, "nometa", "B1")); // pre-E9-style, no metadata
        _db.SaveChanges();

        var result = await _query.ListVocabularyAsync(new ResourceBankListFilter());

        result.TotalCount.Should().Be(2);
        result.Items.Select(i => i.Word).Should().Contain(new[] { "withmeta", "nometa" });
    }

    [Fact]
    public async Task A_metadata_filter_never_matches_a_row_that_has_no_metadata()
    {
        _db.CefrVocabularyEntries.Add(new CefrVocabularyEntry(_sourceId, "nometa", "B1"));
        _db.SaveChanges();

        (await _query.ListVocabularyAsync(new ResourceBankListFilter(ContextTag: "general"))).Items.Should().BeEmpty();
        (await _query.ListVocabularyAsync(new ResourceBankListFilter(Subskill: "vocabulary.receptive"))).Items.Should().BeEmpty();
    }
}
