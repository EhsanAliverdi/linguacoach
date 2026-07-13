using System.Text;
using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase E1 — English resource import staging pipeline. Uses SQLite in-memory (matches
/// ActivityFeedbackHandlerTests convention) so gate/parsing logic is tested against real EF
/// writes, not a full web stack. All fixture content here is synthetic/fake — never a real
/// external dataset.
/// </summary>
public sealed class ResourceImportServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ResourceImportService _sut;

    public ResourceImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ResourceImportService(_db, new ActivityContentFingerprintService());
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private CefrResourceSource SeedApprovedSource()
    {
        var source = new CefrResourceSource(
            "Test English Source", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport("cleared for test");
        _db.CefrResourceSources.Add(source);
        _db.SaveChanges();
        return source;
    }

    private static Stream ToStream(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    // ── Gate 2 — license/source approval ───────────────────────────────────────

    [Fact]
    public async Task Import_blocked_when_source_not_approved()
    {
        var source = new CefrResourceSource("Unapproved Source", "CC-BY-4.0");
        _db.CefrResourceSources.Add(source);
        await _db.SaveChangesAsync();

        var act = async () => await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream("word,cefr\nhello,A1\n"), "test.csv", ResourceImportMode.Csv));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
        (await _db.ResourceImportRuns.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Import_blocked_when_source_language_is_not_english()
    {
        var source = SeedApprovedSource();
        // Entity invariants never allow constructing a non-English source via its public API —
        // simulate legacy/malformed data reaching the service anyway (defense-in-depth check).
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE cefr_resource_sources SET language_code = 'fa' WHERE id = {0}", source.Id);
        await _db.Entry(source).ReloadAsync();

        var act = async () => await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream("word,cefr\nhello,A1\n"), "test.csv", ResourceImportMode.Csv));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
        (await _db.ResourceImportRuns.CountAsync()).Should().Be(0);
    }

    // ── Gate 1 — English-only ──────────────────────────────────────────────────

    [Fact]
    public async Task English_only_row_passes_gate_and_persian_script_row_is_rejected()
    {
        var source = SeedApprovedSource();
        var csv = "word,cefr\nhello,A1\nسلام,A1\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "mixed.csv", ResourceImportMode.Csv));

        result.SucceededCount.Should().Be(1);
        result.RejectedCount.Should().Be(1);

        var candidates = await _db.ResourceCandidates.ToListAsync();
        candidates.Should().ContainSingle();
        candidates[0].CanonicalText.Should().Be("hello");
        candidates.Should().NotContain(c => c.CanonicalText.Contains("سلام"));
    }

    [Fact]
    public async Task Row_with_explicit_non_english_languageCode_is_rejected()
    {
        var source = SeedApprovedSource();
        var csv = "word,languageCode\nbonjour,fr\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "explicit-lang.csv", ResourceImportMode.Csv));

        result.SucceededCount.Should().Be(0);
        result.RejectedCount.Should().Be(1);
        (await _db.ResourceCandidates.CountAsync()).Should().Be(0);
    }

    // ── Format parsing ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Csv_import_creates_expected_run_raw_record_and_candidate_rows()
    {
        var source = SeedApprovedSource();
        var csv = "word,cefr\nhello,A1\nworld,A1\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "vocab.csv", ResourceImportMode.Csv));

        result.TotalRecordCount.Should().Be(2);
        result.SucceededCount.Should().Be(2);
        result.Status.Should().Be("Completed");

        (await _db.ResourceImportRuns.CountAsync()).Should().Be(1);
        (await _db.ResourceRawRecords.CountAsync()).Should().Be(2);
        var candidates = await _db.ResourceCandidates.ToListAsync();
        candidates.Should().HaveCount(2);
        candidates.Should().OnlyContain(c => c.CandidateType == ResourceCandidateType.VocabularyEntry);
    }

    [Fact]
    public async Task Json_array_import_creates_expected_rows()
    {
        var source = SeedApprovedSource();
        var json = """[{"word":"hello"},{"word":"world"}]""";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(json), "vocab.json", ResourceImportMode.Json));

        result.SucceededCount.Should().Be(2);
        (await _db.ResourceCandidates.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Jsonl_import_creates_expected_rows()
    {
        var source = SeedApprovedSource();
        var jsonl = "{\"word\":\"hello\"}\n{\"word\":\"world\"}\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(jsonl), "vocab.jsonl", ResourceImportMode.Jsonl));

        result.SucceededCount.Should().Be(2);
        (await _db.ResourceCandidates.CountAsync()).Should().Be(2);
    }

    // ── Gate 3 — recognizable content field ─────────────────────────────────────

    [Fact]
    public async Task Malformed_row_missing_content_fields_is_rejected_without_aborting_rest_of_file()
    {
        var source = SeedApprovedSource();
        var csv = "word,notes\nhello,ok\n,just some notes with nothing recognizable\nworld,ok\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "partial-bad.csv", ResourceImportMode.Csv));

        result.TotalRecordCount.Should().Be(3);
        result.SucceededCount.Should().Be(2);
        result.RejectedCount.Should().Be(1);
        (await _db.ResourceCandidates.CountAsync()).Should().Be(2);

        var rejected = await _db.ResourceRawRecords
            .Where(r => r.ExtractionStatus == ResourceRawRecordStatus.Rejected).ToListAsync();
        rejected.Should().ContainSingle();
        rejected[0].ExtractionWarningsJson.Should().NotBeNullOrWhiteSpace();
    }

    // ── Duplicate detection ─────────────────────────────────────────────────────

    [Fact]
    public async Task Duplicate_raw_row_within_one_run_is_flagged_not_duplicated_as_two_candidates()
    {
        var source = SeedApprovedSource();
        var csv = "word,cefr\nhello,A1\nhello,A1\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "dupes.csv", ResourceImportMode.Csv));

        result.TotalRecordCount.Should().Be(2);
        result.SucceededCount.Should().Be(1);
        result.RejectedCount.Should().Be(1);
        (await _db.ResourceCandidates.CountAsync()).Should().Be(1);
    }

    // ── Fingerprint stability ────────────────────────────────────────────────────

    [Fact]
    public async Task Content_fingerprint_is_stable_across_two_separate_imports()
    {
        var source = SeedApprovedSource();
        var csv = "word,cefr\nhello,A1\n";

        await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "run1.csv", ResourceImportMode.Csv));
        await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "run2.csv", ResourceImportMode.Csv));

        var fingerprints = await _db.ResourceCandidates.Select(c => c.ContentFingerprint).ToListAsync();
        fingerprints.Should().HaveCount(2);
        fingerprints[0].Should().Be(fingerprints[1]);
    }

    // ── No writes to any published Cefr* bank table ─────────────────────────────

    [Fact]
    public async Task No_rows_are_ever_written_to_any_published_cefr_bank_table()
    {
        var source = SeedApprovedSource();
        var csv = "word,cefr\nhello,A1\nworld,A1\n";

        await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "vocab.csv", ResourceImportMode.Csv));

        (await _db.CefrDescriptors.CountAsync()).Should().Be(0);
        (await _db.ResourceBankItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Malformed_file_fails_cleanly_with_no_candidates()
    {
        var source = SeedApprovedSource();
        var badJson = "{ this is not valid json ";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(badJson), "bad.json", ResourceImportMode.Json));

        result.Status.Should().Be("Failed");
        result.ErrorSummary.Should().NotBeNullOrWhiteSpace();
        (await _db.ResourceCandidates.CountAsync()).Should().Be(0);
    }

    // ── Phase H2 — admin-forced candidate type + default metadata ──────────────

    [Fact]
    public async Task DefaultCandidateType_overrides_row_field_name_inference()
    {
        var source = SeedApprovedSource();
        // "word" would normally infer VocabularyEntry — force GrammarProfileEntry instead.
        var csv = "word\nhello\n";

        await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "forced-type.csv", ResourceImportMode.Csv,
            DefaultCandidateType: ResourceCandidateType.GrammarProfileEntry));

        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CandidateType.Should().Be(ResourceCandidateType.GrammarProfileEntry);
        candidate.CanonicalText.Should().Be("hello");
    }

    [Fact]
    public async Task Default_cefr_level_applies_when_row_has_no_cefrLevel_column()
    {
        var source = SeedApprovedSource();
        var csv = "word\nhello\n";

        await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "default-cefr.csv", ResourceImportMode.Csv,
            DefaultCandidateType: ResourceCandidateType.VocabularyEntry, DefaultCefrLevel: "B1"));

        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CefrLevel.Should().Be("B1");
        candidate.CefrConfidence.Should().Be(1.0);
    }

    [Fact]
    public async Task Row_cefrLevel_overrides_default_when_valid()
    {
        var source = SeedApprovedSource();
        var csv = "word,cefrLevel\nhello,C1\n";

        await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "row-cefr.csv", ResourceImportMode.Csv,
            DefaultCandidateType: ResourceCandidateType.VocabularyEntry, DefaultCefrLevel: "B1"));

        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CefrLevel.Should().Be("C1");
    }

    [Fact]
    public async Task Invalid_row_cefrLevel_falls_back_to_default_and_produces_a_warning()
    {
        var source = SeedApprovedSource();
        var csv = "word,cefrLevel\nhello,Z9\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "invalid-row-cefr.csv", ResourceImportMode.Csv,
            DefaultCandidateType: ResourceCandidateType.VocabularyEntry, DefaultCefrLevel: "B1"));

        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CefrLevel.Should().Be("B1");
        result.WarningCount.Should().Be(1);
        result.SucceededCount.Should().Be(1);

        var rawRecord = await _db.ResourceRawRecords.SingleAsync();
        rawRecord.ExtractionStatus.Should().Be(ResourceRawRecordStatus.Parsed);
        rawRecord.ExtractionWarningsJson.Should().Contain("Z9");
    }

    [Fact]
    public async Task Invalid_default_cefrLevel_applies_no_cefr_and_produces_a_warning()
    {
        var source = SeedApprovedSource();
        var csv = "word\nhello\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "invalid-default-cefr.csv", ResourceImportMode.Csv,
            DefaultCandidateType: ResourceCandidateType.VocabularyEntry, DefaultCefrLevel: "not-a-level"));

        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CefrLevel.Should().BeNull();
        result.WarningCount.Should().Be(1);
    }

    [Fact]
    public async Task Default_skill_subskill_context_focus_and_difficulty_apply_when_row_has_none()
    {
        var source = SeedApprovedSource();
        var csv = "word\nhello\n";

        await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "defaults.csv", ResourceImportMode.Csv,
            DefaultCandidateType: ResourceCandidateType.VocabularyEntry,
            DefaultSkill: "Vocabulary", DefaultSubskill: "CoreWords",
            DefaultContextTags: new[] { "travel" }, DefaultFocusTags: new[] { "greetings" },
            DefaultDifficultyBand: 2));

        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.PrimarySkill.Should().Be("Vocabulary");
        candidate.Subskill.Should().Be("CoreWords");
        candidate.ContextTagsJson.Should().Contain("travel");
        candidate.FocusTagsJson.Should().Contain("greetings");
        candidate.DifficultyBand.Should().Be(2);
    }

    [Fact]
    public async Task Row_skill_overrides_default_skill()
    {
        var source = SeedApprovedSource();
        var csv = "word,skill\nhello,Reading\n";

        await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "row-skill.csv", ResourceImportMode.Csv,
            DefaultCandidateType: ResourceCandidateType.VocabularyEntry, DefaultSkill: "Vocabulary"));

        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.PrimarySkill.Should().Be("Reading");
    }

    // ── Phase J5a — WritingPrompt candidate type ────────────────────────────────

    [Fact]
    public async Task Row_with_prompt_field_infers_as_WritingPrompt()
    {
        var source = SeedApprovedSource();
        var csv = "title,prompt\nEmail reply,Write a reply to your manager about a scheduling conflict.\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "writing.csv", ResourceImportMode.Csv));

        result.SucceededCount.Should().Be(1);
        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CandidateType.Should().Be(ResourceCandidateType.WritingPrompt);
        candidate.CanonicalText.Should().Be("Email reply");
    }

    [Fact]
    public async Task DefaultCandidateType_WritingPrompt_extracts_canonical_text_from_prompt_when_no_title()
    {
        var source = SeedApprovedSource();
        var csv = "prompt\nDescribe your typical morning routine.\n";

        await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "writing-no-title.csv", ResourceImportMode.Csv,
            DefaultCandidateType: ResourceCandidateType.WritingPrompt));

        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CandidateType.Should().Be(ResourceCandidateType.WritingPrompt);
        candidate.CanonicalText.Should().Be("Describe your typical morning routine.");
    }

    // ── Phase J5c — ListeningPassage candidate type ─────────────────────────────

    [Fact]
    public async Task Row_with_transcript_field_infers_as_ListeningPassage()
    {
        var source = SeedApprovedSource();
        var csv = "title,transcript\nMorning News,Good morning and welcome to the daily news.\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "listening.csv", ResourceImportMode.Csv));

        result.SucceededCount.Should().Be(1);
        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CandidateType.Should().Be(ResourceCandidateType.ListeningPassage);
        candidate.CanonicalText.Should().Be("Morning News");
        candidate.AudioStorageKey.Should().BeNull();
    }

    [Fact]
    public async Task DefaultCandidateType_ListeningPassage_extracts_canonical_text_from_transcript_when_no_title()
    {
        var source = SeedApprovedSource();
        var csv = "transcript\nAn interview about remote work habits.\n";

        await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "listening-no-title.csv", ResourceImportMode.Csv,
            DefaultCandidateType: ResourceCandidateType.ListeningPassage));

        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CandidateType.Should().Be(ResourceCandidateType.ListeningPassage);
        candidate.CanonicalText.Should().Be("An interview about remote work habits.");
    }

    // ── Real-world CSV header compatibility (found importing the CEFR-J Vocabulary Profile) ────

    [Fact]
    public async Task Row_with_headword_and_CEFR_columns_is_recognized_as_vocabulary()
    {
        // Matches the CEFR-J Vocabulary Profile's actual header shape: headword,pos,CEFR,...
        var source = SeedApprovedSource();
        var csv = "headword,pos,CEFR\nabandon,verb,B1\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "cefrj.csv", ResourceImportMode.Csv));

        result.SucceededCount.Should().Be(1);
        result.RejectedCount.Should().Be(0);
        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CandidateType.Should().Be(ResourceCandidateType.VocabularyEntry);
        candidate.CanonicalText.Should().Be("abandon");
        candidate.CefrLevel.Should().Be("B1");
    }

    [Fact]
    public async Task Row_with_only_unrecognized_columns_produces_an_actionable_rejection_message()
    {
        var source = SeedApprovedSource();
        var csv = "foo,bar\nsomething,else\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "unrecognized.csv", ResourceImportMode.Csv));

        result.RejectedCount.Should().Be(1);
        var rawRecord = await _db.ResourceRawRecords.SingleAsync();
        rawRecord.ExtractionWarningsJson.Should().Contain("foo").And.Contain("bar");
        rawRecord.ExtractionWarningsJson.Should().Contain("headword");
    }

    // ── Phase K1 — admin-confirmed column renames applied before any gate runs ──────────────────

    [Fact]
    public async Task ColumnRenames_maps_an_unrecognized_column_onto_a_recognized_field()
    {
        var source = SeedApprovedSource();
        // "term" isn't a recognized field on its own — without the rename this row would be
        // rejected by Gate 3 entirely.
        var csv = "term,level\nabandon,B1\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "renamed.csv", ResourceImportMode.Csv,
            ColumnRenames: new Dictionary<string, string> { ["term"] = "word", ["level"] = "cefrLevel" }));

        result.SucceededCount.Should().Be(1);
        result.RejectedCount.Should().Be(0);
        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CandidateType.Should().Be(ResourceCandidateType.VocabularyEntry);
        candidate.CanonicalText.Should().Be("abandon");
        candidate.CefrLevel.Should().Be("B1");
    }

    [Fact]
    public async Task ColumnRenames_is_case_insensitive_on_the_source_column_name()
    {
        var source = SeedApprovedSource();
        var csv = "Term\nabandon\n";

        await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "renamed-case.csv", ResourceImportMode.Csv,
            ColumnRenames: new Dictionary<string, string> { ["term"] = "word" }));

        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CandidateType.Should().Be(ResourceCandidateType.VocabularyEntry);
    }

    // ── Phase J5d — SpeakingPrompt candidate type ───────────────────────────────

    [Fact]
    public async Task Row_with_scenario_field_infers_as_SpeakingPrompt()
    {
        var source = SeedApprovedSource();
        var csv = "title,scenario\nDeadline negotiation,Role-play: negotiate a deadline extension with your manager.\n";

        var result = await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "speaking.csv", ResourceImportMode.Csv));

        result.SucceededCount.Should().Be(1);
        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CandidateType.Should().Be(ResourceCandidateType.SpeakingPrompt);
        candidate.CanonicalText.Should().Be("Deadline negotiation");
    }

    [Fact]
    public async Task DefaultCandidateType_SpeakingPrompt_extracts_canonical_text_from_scenario_when_no_title()
    {
        var source = SeedApprovedSource();
        var csv = "scenario\nOrder food at a restaurant and ask about allergens.\n";

        await _sut.ImportAsync(new ResourceImportRequest(
            source.Id, ToStream(csv), "speaking-no-title.csv", ResourceImportMode.Csv,
            DefaultCandidateType: ResourceCandidateType.SpeakingPrompt));

        var candidate = await _db.ResourceCandidates.SingleAsync();
        candidate.CandidateType.Should().Be(ResourceCandidateType.SpeakingPrompt);
        candidate.CanonicalText.Should().Be("Order food at a restaurant and ask about allergens.");
    }
}
