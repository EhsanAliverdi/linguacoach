using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinguaCoach.UnitTests.Activity;

/// <summary>
/// Phase D1/D2 — bank-first Today slice. Exercises <see cref="TodayBankResourceSelector"/>
/// against SQLite in-memory, using directly-constructed Cefr* bank rows (test fixtures only — the
/// "no direct final-table insert" guarantee from Phase E is about production code paths, proven
/// separately by InternalResourceSeedPackSeederTests). No AI provider is involved anywhere in this
/// suite; <see cref="ActivityNoveltyPolicy"/> is the real deterministic implementation, not a fake.
/// </summary>
public sealed class TodayBankResourceSelectorTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly TodayBankResourceSelector _sut;
    private readonly Guid _studentId;
    private readonly Guid _sourceId;

    public TodayBankResourceSelectorTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        _studentId = student.Id;

        var source = new CefrResourceSource(
            "Test Source", "Internal/Original", allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        _db.CefrResourceSources.Add(source);
        _sourceId = source.Id;

        _db.SaveChanges();

        var noveltyPolicy = new ActivityNoveltyPolicy(_db, Options.Create(new NoveltyPolicySettings
        {
            FingerprintCooldownDays = 60,
            TemplateCooldownDays = 3,
            TopicCooldownDays = 7,
            ScenarioCooldownDays = 7,
        }));
        var bankQuery = new ResourceBankQueryService(_db);
        _sut = new TodayBankResourceSelector(bankQuery, noveltyPolicy, _db, NullLogger<TodayBankResourceSelector>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private Guid SeedVocabulary(string word, string cefrLevel = "B1")
    {
        var entry = new CefrVocabularyEntry(_sourceId, word, cefrLevel);
        _db.CefrVocabularyEntries.Add(entry);
        return entry.Id;
    }

    private Guid SeedGrammar(string grammarPoint, string cefrLevel = "B1")
    {
        var entry = new CefrGrammarProfileEntry(_sourceId, cefrLevel, grammarPoint);
        _db.CefrGrammarProfileEntries.Add(entry);
        return entry.Id;
    }

    private Guid SeedReading(string excerpt, string cefrLevel = "B1")
    {
        var entry = new CefrReadingReference(_sourceId, cefrLevel, referenceExcerpt: excerpt);
        _db.CefrReadingReferences.Add(entry);
        return entry.Id;
    }

    private static TodayBankSelectionRequest Request(
        string cefrLevel = "B1",
        string primarySkill = "Vocabulary",
        IReadOnlyList<string>? secondarySkills = null,
        Guid? studentId = null,
        bool allowLowerLevelReview = false) =>
        new(studentId ?? Guid.NewGuid(), cefrLevel, primarySkill, secondarySkills ?? Array.Empty<string>(),
            AllowLowerLevelReview: allowLowerLevelReview);

    [Fact]
    public async Task Returns_matching_vocabulary_entries_for_vocabulary_pattern_and_cefr_level()
    {
        SeedVocabulary("deadline", "B1");
        SeedVocabulary("invoice", "B1");
        SeedVocabulary("advanced-term", "C1"); // different CEFR level — must not be selected
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(cefrLevel: "B1", studentId: _studentId));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        result.Resources.Should().NotBeEmpty();
        result.Resources.Should().OnlyContain(r => r.ResourceType == "Vocabulary");
        result.Resources.Select(r => r.DisplayText).Should().NotContain("advanced-term");
        result.PromptSupplementText.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Includes_opportunistic_grammar_entries_only_when_secondary_skills_list_grammar()
    {
        SeedVocabulary("deadline", "B1");
        SeedGrammar("present perfect", "B1");
        _db.SaveChanges();

        var withGrammar = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", secondarySkills: ["Grammar"], studentId: _studentId));
        withGrammar.Resources.Should().Contain(r => r.ResourceType == "Grammar");

        var withoutGrammar = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", secondarySkills: [], studentId: Guid.NewGuid()));
        withoutGrammar.Resources.Should().NotContain(r => r.ResourceType == "Grammar");
    }

    [Fact]
    public async Task Returns_matching_reading_references_for_reading_pattern()
    {
        SeedReading("A short workplace email excerpt about a meeting change.", "B2");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(cefrLevel: "B2", primarySkill: "Reading", studentId: _studentId));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        result.Resources.Should().ContainSingle().Which.ResourceType.Should().Be("Reading");
    }

    [Theory]
    [InlineData("Writing")]
    [InlineData("Speaking")]
    [InlineData("Listening")]
    [InlineData("Reflection")]
    public async Task Returns_skipped_unsupported_pattern_for_non_vocabulary_non_reading_skills(string skill)
    {
        SeedVocabulary("deadline", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(primarySkill: skill, studentId: _studentId));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.SkippedUnsupportedPattern);
        result.Resources.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_no_suitable_resources_gracefully_when_bank_is_empty()
    {
        var result = await _sut.SelectAsync(Request(studentId: _studentId));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.NoSuitableResources);
        result.Resources.Should().BeEmpty();
        result.PromptSupplementText.Should().BeNull();
    }

    [Fact]
    public async Task Excludes_a_recently_used_bank_entry_and_reports_blocked_by_novelty_when_it_was_the_only_candidate()
    {
        SeedVocabulary("deadline", "B1");
        _db.SaveChanges();
        var entryId = _db.CefrVocabularyEntries.Single().Id;

        _db.StudentActivityUsageLogs.Add(new StudentActivityUsageLog(
            studentProfileId: _studentId,
            contentFingerprint: $"bank-vocab-precheck:{entryId}",
            consumedAtUtc: DateTime.UtcNow.AddDays(-1)));
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(cefrLevel: "B1", studentId: _studentId));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BlockedByNovelty);
        result.Resources.Should().BeEmpty();
    }

    [Fact]
    public async Task Selected_resources_are_english_only_by_construction()
    {
        // The Resource Bank pipeline enforces English-only at import/publish time (Phase E1's
        // language heuristic, re-verified by Phase E's own tests) — there is no separate
        // language check to re-implement here. This test only guards that the selector doesn't
        // introduce a second, independent content path that could bypass that guarantee.
        SeedVocabulary("workflow", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(cefrLevel: "B1", studentId: _studentId));

        result.Resources.Should().OnlyContain(r => System.Text.RegularExpressions.Regex.IsMatch(
            r.DisplayText, "^[\\x00-\\x7F]*$"));
    }

    [Fact]
    public async Task Discovers_content_seeded_by_the_phase_e6_internal_seed_pack()
    {
        var importService = new ResourceImportService(_db, new ActivityContentFingerprintService());
        var validationService = new ResourceCandidateValidationService(_db, new FormIoSchemaValidationService());
        var publishService = new ResourceCandidatePublishService(_db);
        await InternalResourceSeedPackSeeder.SeedAsync(
            _db, importService, validationService, publishService, NullLogger.Instance);

        var anyVocabLevel = await _db.CefrVocabularyEntries.Select(v => v.CefrLevel).FirstAsync();

        var result = await _sut.SelectAsync(Request(cefrLevel: anyVocabLevel, studentId: _studentId));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
    }

    // ── Phase D2 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Returns_a_balanced_bundle_of_vocabulary_grammar_and_reading_for_a_vocabulary_pattern()
    {
        SeedVocabulary("deadline", "B1");
        SeedGrammar("present perfect", "B1");
        SeedReading("A short workplace email excerpt.", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", secondarySkills: ["Grammar"], studentId: _studentId));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        result.Resources.Should().Contain(r => r.ResourceType == "Vocabulary");
        result.Resources.Should().Contain(r => r.ResourceType == "Grammar");
        result.Resources.Should().Contain(r => r.ResourceType == "Reading");
    }

    [Fact]
    public async Task Prefers_exact_cefr_match_over_any_other_level()
    {
        SeedVocabulary("exact-match-word", "B1");
        SeedVocabulary("other-level-word", "A2");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(cefrLevel: "B1", studentId: _studentId));

        result.Resources.Select(r => r.DisplayText).Should().Contain("exact-match-word");
        result.Resources.Select(r => r.DisplayText).Should().NotContain("other-level-word");
        result.Resources.Should().OnlyContain(r => r.SelectionReason.Contains("exact CEFR match"));
    }

    [Fact]
    public async Task Widens_to_the_next_lower_cefr_level_only_when_review_is_allowed_and_exact_level_is_empty()
    {
        SeedVocabulary("lower-level-word", "A2");
        _db.SaveChanges();

        var withoutReview = await _sut.SelectAsync(Request(cefrLevel: "B1", studentId: _studentId, allowLowerLevelReview: false));
        withoutReview.Outcome.Should().Be(TodayBankSelectionOutcome.NoSuitableResources);

        var withReview = await _sut.SelectAsync(Request(cefrLevel: "B1", studentId: Guid.NewGuid(), allowLowerLevelReview: true));
        withReview.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        withReview.Resources.Should().Contain(r => r.DisplayText == "lower-level-word");
        withReview.Resources.Should().OnlyContain(r => r.SelectionReason.Contains("review/lower-level match"));
    }

    [Fact]
    public async Task Never_widens_upward_even_when_review_is_allowed()
    {
        SeedVocabulary("harder-word", "C1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(cefrLevel: "B1", studentId: _studentId, allowLowerLevelReview: true));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.NoSuitableResources);
    }

    [Fact]
    public async Task Excludes_a_resource_the_student_previously_marked_not_useful()
    {
        var vocabId = SeedVocabulary("deadline", "B1");
        _db.SaveChanges();

        var activity = new LearningActivity(
            ActivityType.VocabularyPractice, ActivitySource.AiGenerated, "t", "B1", "{}");
        activity.SetBankResourceProvenance($"[{{\"type\":\"Vocabulary\",\"id\":\"{vocabId}\"}}]");
        _db.LearningActivities.Add(activity);
        _db.SaveChanges();

        _db.ActivityFeedbackSignals.Add(new ActivityFeedbackSignal(
            _studentId, activity.Id,
            ActivityFeedbackDifficultyRating.RightLevel,
            ActivityFeedbackClarityRating.Clear,
            ActivityFeedbackUsefulnessRating.NotUseful,
            ActivityFeedbackRepeatPreference.Neutral));
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(cefrLevel: "B1", studentId: _studentId));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BlockedByNovelty);
        result.Resources.Should().BeEmpty();
    }

    [Fact]
    public async Task Excludes_a_resource_the_student_marked_do_not_show_similar_soon()
    {
        var vocabId = SeedVocabulary("invoice", "B1");
        _db.SaveChanges();

        var activity = new LearningActivity(
            ActivityType.VocabularyPractice, ActivitySource.AiGenerated, "t", "B1", "{}");
        activity.SetBankResourceProvenance($"[{{\"type\":\"Vocabulary\",\"id\":\"{vocabId}\"}}]");
        _db.LearningActivities.Add(activity);
        _db.SaveChanges();

        _db.ActivityFeedbackSignals.Add(new ActivityFeedbackSignal(
            _studentId, activity.Id,
            ActivityFeedbackDifficultyRating.RightLevel,
            ActivityFeedbackClarityRating.Clear,
            ActivityFeedbackUsefulnessRating.Useful,
            ActivityFeedbackRepeatPreference.DoNotShowSimilarSoon));
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(cefrLevel: "B1", studentId: _studentId));

        result.Resources.Should().BeEmpty();
    }

    [Fact]
    public async Task Selected_resources_carry_source_id_and_content_fingerprint_metadata()
    {
        SeedVocabulary("deadline", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(cefrLevel: "B1", studentId: _studentId));

        var resource = result.Resources.Should().ContainSingle().Subject;
        resource.SourceId.Should().Be(_sourceId);
        resource.ContentFingerprint.Should().Be($"bank-vocab-precheck:{resource.Id}");
        resource.SelectionReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Structured_prompt_block_names_the_cefr_level_and_english_only_constraint()
    {
        SeedVocabulary("deadline", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(cefrLevel: "B1", studentId: _studentId));

        result.PromptSupplementText.Should().Contain("B1");
        result.PromptSupplementText.Should().Contain("English-only");
        result.PromptSupplementText.Should().Contain("do not invent unrelated vocabulary");
    }
}
