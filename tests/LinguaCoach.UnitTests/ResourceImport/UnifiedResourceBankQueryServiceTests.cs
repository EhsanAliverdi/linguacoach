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

    private ResourceBankItem SeedVocab(
        string word, string cefr = "B1", string? subskill = null, int? difficulty = null,
        string? contextTagsJson = null, string? focusTagsJson = null)
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Vocabulary, _sourceId, cefr,
            ResourceBankItemContent.Serialize(new VocabularyContent(word, null, null)),
            subskill, difficulty, contextTagsJson, focusTagsJson);
        _db.ResourceBankItems.Add(e);
        return e;
    }

    private ResourceBankItem SeedGrammar(
        string point, string cefr = "B1", string? subskill = null, int? difficulty = null,
        string? contextTagsJson = null, string? focusTagsJson = null)
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Grammar, _sourceId, cefr,
            ResourceBankItemContent.Serialize(new GrammarContent(point, null)),
            subskill, difficulty, contextTagsJson, focusTagsJson);
        _db.ResourceBankItems.Add(e);
        return e;
    }

    private ResourceBankItem SeedReference(
        string textType, string cefr = "B1", string? subskill = null, int? difficulty = null,
        string? contextTagsJson = null, string? focusTagsJson = null)
    {
        var e = new ResourceBankItem(
            PublishedResourceType.ReadingReference, _sourceId, cefr,
            ResourceBankItemContent.Serialize(new ReadingReferenceContent(textType, null, "a short excerpt")),
            subskill, difficulty, contextTagsJson, focusTagsJson);
        _db.ResourceBankItems.Add(e);
        return e;
    }

    private ResourceBankItem SeedPassage(
        string title, string cefr = "B1", string? subskill = null, int? difficulty = null,
        string? contextTagsJson = null, string? focusTagsJson = null)
    {
        const string passageText = "Passage text with enough words to be realistic for a reading exercise.";
        var wordCount = passageText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var e = new ResourceBankItem(
            PublishedResourceType.ReadingPassage, _sourceId, cefr,
            ResourceBankItemContent.Serialize(new ReadingPassageContent(
                title, passageText, null, "Reading", null, wordCount, 1, null, null)),
            subskill, difficulty, contextTagsJson, focusTagsJson);
        _db.ResourceBankItems.Add(e);
        return e;
    }

    private ResourceBankItem SeedWriting(
        string title, string cefr = "B1", string? subskill = null, int? difficulty = null,
        string? contextTagsJson = null, string? focusTagsJson = null)
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Writing, _sourceId, cefr,
            ResourceBankItemContent.Serialize(new WritingPromptContent(title, "Write about something.", null, null)),
            subskill, difficulty, contextTagsJson, focusTagsJson);
        _db.ResourceBankItems.Add(e);
        return e;
    }

    private ResourceBankItem SeedListening(
        string title, string cefr = "B1", string? subskill = null, int? difficulty = null,
        string? contextTagsJson = null, string? focusTagsJson = null, decimal? audioDurationSeconds = null)
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Listening, _sourceId, cefr,
            ResourceBankItemContent.Serialize(new ListeningPassageContent(
                title, "A transcript.", "resource-import-audio/test.mp3", "audio/mpeg", null, audioDurationSeconds)),
            subskill, difficulty, contextTagsJson, focusTagsJson);
        _db.ResourceBankItems.Add(e);
        return e;
    }

    private ResourceBankItem SeedSpeaking(
        string title, string cefr = "B1", string? subskill = null, int? difficulty = null,
        string? contextTagsJson = null, string? focusTagsJson = null)
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Speaking, _sourceId, cefr,
            ResourceBankItemContent.Serialize(new SpeakingPromptContent(title, "Role-play about something.", null)),
            subskill, difficulty, contextTagsJson, focusTagsJson);
        _db.ResourceBankItems.Add(e);
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
    public async Task Unified_returns_writing_prompt_rows()
    {
        SeedWriting("Email reply");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter());

        result.Items.Should().ContainSingle(i => i.Type == UnifiedResourceBankItemType.Writing && i.Title == "Email reply");
    }

    [Fact]
    public async Task Unified_returns_listening_passage_rows()
    {
        SeedListening("Morning News");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter());

        result.Items.Should().ContainSingle(i => i.Type == UnifiedResourceBankItemType.Listening && i.Title == "Morning News");
    }

    /// <summary>Phase 4.6 — the unified list/detail DTO must surface HasAudio/AudioContentType/
    /// AudioDurationSeconds for a published Listening row (previously silently dropped), while
    /// never exposing the raw AudioStorageKey.</summary>
    [Fact]
    public async Task Unified_listening_row_surfaces_audio_metadata_but_not_the_raw_storage_key()
    {
        var entry = SeedListening("Morning News", audioDurationSeconds: 87.5m);
        _db.SaveChanges();

        var byId = await _query.GetUnifiedByIdAsync(entry.Id);

        byId.Should().NotBeNull();
        byId!.HasAudio.Should().BeTrue();
        byId.AudioContentType.Should().Be("audio/mpeg");
        byId.AudioDurationSeconds.Should().Be(87.5m);
    }

    /// <summary>Phase 4.6 — the edit DTO (admin-only, informational) does carry AudioStorageKey,
    /// for parity with Speaking's ImageUrl — but it is never accepted back by
    /// UpdateResourceBankItemCommand (replacing published audio is out of scope for this phase).</summary>
    [Fact]
    public async Task Edit_dto_for_listening_surfaces_audio_storage_key_content_type_and_duration()
    {
        var entry = SeedListening("Morning News", audioDurationSeconds: 42m);
        _db.SaveChanges();

        var edit = await _query.GetEditDtoAsync(entry.Id);

        edit.Should().NotBeNull();
        edit!.AudioStorageKey.Should().Be("resource-import-audio/test.mp3");
        edit.AudioContentType.Should().Be("audio/mpeg");
        edit.AudioDurationSeconds.Should().Be(42m);
    }

    [Fact]
    public async Task Unified_returns_speaking_prompt_rows()
    {
        SeedSpeaking("Deadline negotiation");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter());

        result.Items.Should().ContainSingle(i => i.Type == UnifiedResourceBankItemType.Speaking && i.Title == "Deadline negotiation");
    }

    [Fact]
    public async Task Unified_aggregates_all_seven_types_in_one_call()
    {
        SeedVocab("garden");
        SeedGrammar("present perfect");
        SeedReference("narrative");
        SeedPassage("A Trip to the Market");
        SeedWriting("Email reply");
        SeedListening("Morning News");
        SeedSpeaking("Deadline negotiation");
        _db.SaveChanges();

        var result = await _query.ListUnifiedAsync(new UnifiedResourceBankListFilter(PageSize: 200));

        result.TotalCount.Should().Be(7);
        result.Items.Select(i => i.Type).Should().BeEquivalentTo(new[]
        {
            UnifiedResourceBankItemType.Vocabulary, UnifiedResourceBankItemType.Grammar,
            UnifiedResourceBankItemType.ReadingReference, UnifiedResourceBankItemType.ReadingPassage,
            UnifiedResourceBankItemType.Writing, UnifiedResourceBankItemType.Listening, UnifiedResourceBankItemType.Speaking
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
        _db.ResourceBankItems.Add(new ResourceBankItem(
            PublishedResourceType.Vocabulary, _sourceId, "B1",
            ResourceBankItemContent.Serialize(new VocabularyContent("plainword", null, null))));
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
