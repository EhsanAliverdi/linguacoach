using FluentAssertions;
using LinguaCoach.Application.Activity;
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

    private Guid SeedVocabulary(string word, string cefrLevel = "B1",
        string? contextTagsJson = null, string? focusTagsJson = null, string? subskill = null, int? difficultyBand = null)
    {
        var entry = new ResourceBankItem(
            PublishedResourceType.Vocabulary, _sourceId, cefrLevel,
            ResourceBankItemContent.Serialize(new VocabularyContent(word, null, null)),
            subskill, difficultyBand, contextTagsJson, focusTagsJson);
        _db.ResourceBankItems.Add(entry);
        return entry.Id;
    }

    private Guid SeedGrammar(string grammarPoint, string cefrLevel = "B1", string? contextTagsJson = null, string? subskill = null)
    {
        var entry = new ResourceBankItem(
            PublishedResourceType.Grammar, _sourceId, cefrLevel,
            ResourceBankItemContent.Serialize(new GrammarContent(grammarPoint, null)),
            subskill, null, contextTagsJson, "[]");
        _db.ResourceBankItems.Add(entry);
        return entry.Id;
    }

    private Guid SeedReading(string excerpt, string cefrLevel = "B1", string? contextTagsJson = null, string? subskill = null)
    {
        var entry = new ResourceBankItem(
            PublishedResourceType.ReadingReference, _sourceId, cefrLevel,
            ResourceBankItemContent.Serialize(new ReadingReferenceContent(null, null, excerpt)),
            subskill, null, contextTagsJson, "[]");
        _db.ResourceBankItems.Add(entry);
        return entry.Id;
    }

    private Guid SeedReadingPassage(string title, string passageText, string cefrLevel = "B1", string? contextTagsJson = null)
    {
        var wordCount = passageText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var entry = new ResourceBankItem(
            PublishedResourceType.ReadingPassage, _sourceId, cefrLevel,
            ResourceBankItemContent.Serialize(new ReadingPassageContent(
                title, passageText, null, "Reading", null, wordCount,
                Math.Max(1, (int)Math.Round(wordCount / 200.0, MidpointRounding.AwayFromZero)), null, null)),
            contextTagsJson: contextTagsJson);
        _db.ResourceBankItems.Add(entry);
        return entry.Id;
    }

    private static TodayBankSelectionRequest Request(
        string cefrLevel = "B1",
        string primarySkill = "Vocabulary",
        IReadOnlyList<string>? secondarySkills = null,
        Guid? studentId = null,
        bool allowLowerLevelReview = false,
        string? patternKey = null,
        bool prefersWorkplaceContext = false,
        IReadOnlyList<string>? preferredFocusTags = null,
        string? preferredSubskill = null,
        int? preferredDifficultyBand = null) =>
        new(studentId ?? Guid.NewGuid(), cefrLevel, primarySkill, secondarySkills ?? Array.Empty<string>(),
            AllowLowerLevelReview: allowLowerLevelReview, PatternKey: patternKey,
            PrefersWorkplaceContext: prefersWorkplaceContext,
            PreferredFocusTags: preferredFocusTags, PreferredSubskill: preferredSubskill,
            PreferredDifficultyBand: preferredDifficultyBand);

    // A representative full-passage-suitable reading pattern (comprehension over a whole text).
    private const string FullPassagePattern = LinguaCoach.Domain.ExercisePatternKey.ReadingMultipleChoiceSingle;
    private const string FillInBlanksPattern = LinguaCoach.Domain.ExercisePatternKey.ReadingFillInBlanks;
    private const string PhraseMatchPattern = LinguaCoach.Domain.ExercisePatternKey.PhraseMatch;
    private const string LongPassage =
        "The team met on Monday to review the quarterly results. Sales had grown steadily, "
        + "but customer support wanted more staff. After a long discussion, the manager agreed "
        + "to hire two new people and to revisit the plan in three months.";

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
        var entryId = SeedVocabulary("deadline", "B1");
        _db.SaveChanges();

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

        var anyVocabLevel = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.Vocabulary).Select(v => v.CefrLevel).FirstAsync();

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

    // ── Phase D3 — full reading passage bank ────────────────────────────────────

    [Fact]
    public async Task Selects_full_reading_passage_for_a_full_passage_suitable_reading_pattern()
    {
        SeedReadingPassage("Quarterly Review", LongPassage, "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        var passage = result.Resources.Should().ContainSingle().Subject;
        passage.ResourceType.Should().Be("ReadingPassage");
        passage.Title.Should().Be("Quarterly Review");
        passage.PassageText.Should().Be(LongPassage);
        passage.CefrLevel.Should().Be("B1");
        passage.WordCount.Should().BeGreaterThan(0);
        passage.EstimatedReadingMinutes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Prefers_full_passage_over_short_reference_when_both_exist_for_a_suitable_pattern()
    {
        SeedReadingPassage("Full", LongPassage, "B1");
        SeedReading("A short excerpt.", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.Resources.Should().OnlyContain(r => r.ResourceType == "ReadingPassage");
    }

    [Fact]
    public async Task Falls_back_to_short_reference_when_no_full_passage_exists()
    {
        SeedReading("A short workplace excerpt about a meeting.", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        result.Resources.Should().OnlyContain(r => r.ResourceType == "Reading");
    }

    [Fact]
    public async Task Uses_short_reference_not_full_passage_for_a_cloze_pattern_even_when_a_passage_exists()
    {
        SeedReadingPassage("Full", LongPassage, "B1");
        SeedReading("A short excerpt.", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FillInBlanksPattern));

        result.Resources.Should().OnlyContain(r => r.ResourceType == "Reading");
    }

    [Fact]
    public async Task Falls_back_to_no_resources_when_neither_full_passage_nor_reference_exists()
    {
        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.NoSuitableResources);
        result.Resources.Should().BeEmpty();
    }

    [Fact]
    public async Task Prefers_exact_cefr_full_passage_and_never_widens_upward()
    {
        SeedReadingPassage("Harder", LongPassage, "C1"); // above routed level — must never be selected
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId,
            allowLowerLevelReview: true, patternKey: FullPassagePattern));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.NoSuitableResources);
    }

    [Fact]
    public async Task Widens_full_passage_one_level_down_only_for_review()
    {
        SeedReadingPassage("Lower", LongPassage, "A2");
        _db.SaveChanges();

        var withoutReview = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId,
            allowLowerLevelReview: false, patternKey: FullPassagePattern));
        withoutReview.Outcome.Should().Be(TodayBankSelectionOutcome.NoSuitableResources);

        var withReview = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: Guid.NewGuid(),
            allowLowerLevelReview: true, patternKey: FullPassagePattern));
        withReview.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        withReview.Resources.Should().OnlyContain(r => r.ResourceType == "ReadingPassage");
        withReview.Resources.Should().OnlyContain(r => r.SelectionReason.Contains("review/lower-level match"));
    }

    [Fact]
    public async Task Avoids_a_recently_used_full_passage_and_falls_back_to_reference()
    {
        var passageId = SeedReadingPassage("Full", LongPassage, "B1");
        SeedReading("A short excerpt.", "B1");
        _db.SaveChanges();

        _db.StudentActivityUsageLogs.Add(new StudentActivityUsageLog(
            studentProfileId: _studentId,
            contentFingerprint: $"bank-reading-passage-precheck:{passageId}",
            consumedAtUtc: DateTime.UtcNow.AddDays(-1)));
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        // Passage excluded by novelty → short reference used instead.
        result.Resources.Should().OnlyContain(r => r.ResourceType == "Reading");
    }

    [Fact]
    public async Task Excludes_a_full_passage_the_student_marked_not_useful()
    {
        var passageId = SeedReadingPassage("Full", LongPassage, "B1");
        _db.SaveChanges();

        var activity = new LearningActivity(
            ActivityType.ReadingTask, ActivitySource.AiGenerated, "t", "B1", "{}");
        activity.SetBankResourceProvenance($"[{{\"type\":\"ReadingPassage\",\"id\":\"{passageId}\"}}]");
        _db.LearningActivities.Add(activity);
        _db.SaveChanges();

        _db.ActivityFeedbackSignals.Add(new ActivityFeedbackSignal(
            _studentId, activity.Id,
            ActivityFeedbackDifficultyRating.RightLevel,
            ActivityFeedbackClarityRating.Clear,
            ActivityFeedbackUsefulnessRating.NotUseful,
            ActivityFeedbackRepeatPreference.Neutral));
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.Resources.Should().NotContain(r => r.ResourceType == "ReadingPassage");
    }

    [Fact]
    public async Task Structured_prompt_block_includes_full_passage_title_text_cefr_and_constraints()
    {
        SeedReadingPassage("Quarterly Review", LongPassage, "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.PromptSupplementText.Should().Contain("ReadingPassage");
        result.PromptSupplementText.Should().Contain("Quarterly Review");
        result.PromptSupplementText.Should().Contain(LongPassage);
        result.PromptSupplementText.Should().Contain("B1");
        result.PromptSupplementText.Should().Contain("Base every comprehension question");
        result.PromptSupplementText.Should().Contain("English-only");
    }

    [Fact]
    public async Task Discovers_full_passages_seeded_by_the_phase_e7_internal_seed_pack()
    {
        var importService = new ResourceImportService(_db, new ActivityContentFingerprintService());
        var validationService = new ResourceCandidateValidationService(_db, new FormIoSchemaValidationService());
        var publishService = new ResourceCandidatePublishService(_db);
        await InternalResourceSeedPackSeeder.SeedAsync(
            _db, importService, validationService, publishService, NullLogger.Instance);

        var anyPassageLevel = await _db.ResourceBankItems.Where(x => x.Type == PublishedResourceType.ReadingPassage).Select(p => p.CefrLevel).FirstAsync();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: anyPassageLevel, primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        result.Resources.Should().Contain(r => r.ResourceType == "ReadingPassage");
    }

    // ── Phase D4 — richer, pattern-shaped bundles ───────────────────────────────

    [Fact]
    public async Task Vocabulary_primary_pattern_returns_a_richer_vocabulary_and_grammar_bundle()
    {
        SeedVocabulary("deadline", "B1");
        SeedVocabulary("invoice", "B1");
        SeedVocabulary("budget", "B1");
        SeedGrammar("present perfect", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", secondarySkills: ["Grammar"],
            studentId: _studentId, patternKey: PhraseMatchPattern));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        // Richer than D2's 2 — up to 3 vocabulary targets now.
        result.Resources.Count(r => r.ResourceType == "Vocabulary").Should().Be(3);
        result.Resources.Should().Contain(r => r.ResourceType == "Grammar");
        result.Resources.Where(r => r.ResourceType == "Vocabulary").Should().OnlyContain(r => r.Role == "primary");
        result.Resources.Where(r => r.ResourceType == "Grammar").Should().OnlyContain(r => r.Role == "supporting");
    }

    [Fact]
    public async Task Reading_comprehension_pattern_uses_full_passage_as_primary_with_supporting_vocabulary()
    {
        SeedReadingPassage("Quarterly Review", LongPassage, "B1");
        SeedVocabulary("deadline", "B1");
        SeedVocabulary("invoice", "B1");
        SeedVocabulary("budget", "B1"); // 3 available, but supporting vocab is capped at 2
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        var passage = result.Resources.Should().ContainSingle(r => r.ResourceType == "ReadingPassage").Subject;
        passage.Role.Should().Be("primary");
        // Supporting vocabulary is capped at 2 so the passage stays the anchor.
        result.Resources.Count(r => r.ResourceType == "Vocabulary").Should().Be(2);
        result.Resources.Where(r => r.ResourceType == "Vocabulary").Should().OnlyContain(r => r.Role == "supporting");
    }

    [Fact]
    public async Task Reading_cloze_pattern_uses_short_reference_not_full_passage_with_supporting_vocabulary()
    {
        SeedReadingPassage("Full", LongPassage, "B1");
        SeedReading("A short excerpt about a meeting.", "B1");
        SeedVocabulary("deadline", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FillInBlanksPattern));

        result.Resources.Should().NotContain(r => r.ResourceType == "ReadingPassage");
        result.Resources.Should().Contain(r => r.ResourceType == "Reading" && r.Role == "primary");
        result.Resources.Should().Contain(r => r.ResourceType == "Vocabulary" && r.Role == "supporting");
    }

    [Fact]
    public async Task Comprehension_pattern_prompt_includes_pattern_specific_passage_instruction()
    {
        SeedReadingPassage("Quarterly Review", LongPassage, "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.PromptSupplementText.Should().Contain("ONLY the following full reading passage");
        result.PromptSupplementText.Should().Contain("every question must be answerable from that passage");
    }

    [Fact]
    public async Task Vocabulary_pattern_prompt_includes_pattern_specific_target_instruction()
    {
        SeedVocabulary("deadline", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: _studentId, patternKey: PhraseMatchPattern));

        result.PromptSupplementText.Should().Contain("Use the selected vocabulary/usage targets naturally");
        result.PromptSupplementText.Should().Contain("do not default to workplace");
    }

    [Fact]
    public async Task Cloze_pattern_prompt_instructs_not_to_copy_a_full_passage()
    {
        SeedReading("A short excerpt about a meeting.", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FillInBlanksPattern));

        result.PromptSupplementText.Should().Contain("do NOT copy a full reading passage");
    }

    [Fact]
    public async Task General_learner_does_not_receive_a_workplace_tagged_full_passage()
    {
        SeedReadingPassage("Office Restructure", LongPassage, "B1", contextTagsJson: "[\"workplace\"]");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId,
            patternKey: FullPassagePattern, prefersWorkplaceContext: false));

        // No non-workplace passage and no short reference exists → no bank resources at all.
        result.Resources.Should().NotContain(r => r.ResourceType == "ReadingPassage");
    }

    [Fact]
    public async Task Workplace_routed_learner_may_receive_a_workplace_tagged_full_passage()
    {
        SeedReadingPassage("Office Restructure", LongPassage, "B1", contextTagsJson: "[\"workplace\"]");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId,
            patternKey: FullPassagePattern, prefersWorkplaceContext: true));

        result.Resources.Should().Contain(r => r.ResourceType == "ReadingPassage");
    }

    [Fact]
    public async Task General_learner_prefers_a_non_workplace_passage_when_both_exist()
    {
        SeedReadingPassage("Office Restructure", LongPassage, "B1", contextTagsJson: "[\"workplace\"]");
        SeedReadingPassage("A Walk in the Park",
            "On Saturday morning, Maya walked to the park near her home. The air was cool and the "
            + "paths were quiet. She sat on a bench, read a few pages of her book, and watched the "
            + "ducks on the pond before heading back for breakfast with her family.",
            "B1", contextTagsJson: "[\"general\",\"daily\"]");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId,
            patternKey: FullPassagePattern, prefersWorkplaceContext: false));

        var passage = result.Resources.Should().ContainSingle(r => r.ResourceType == "ReadingPassage").Subject;
        passage.Title.Should().Be("A Walk in the Park");
    }

    [Fact]
    public async Task Reading_comprehension_falls_back_safely_when_supporting_vocabulary_is_absent()
    {
        SeedReadingPassage("Quarterly Review", LongPassage, "B1");
        _db.SaveChanges(); // no vocabulary seeded

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        result.Resources.Should().ContainSingle().Which.ResourceType.Should().Be("ReadingPassage");
    }

    [Fact]
    public async Task Reading_cloze_falls_back_safely_when_reading_reference_is_absent()
    {
        // Only vocabulary exists; a cloze pattern still returns a safe (vocabulary-supported) bundle
        // without a reading reference, rather than throwing or returning nothing.
        SeedVocabulary("deadline", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FillInBlanksPattern));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        result.Resources.Should().NotContain(r => r.ResourceType == "ReadingPassage");
        result.Resources.Should().Contain(r => r.ResourceType == "Vocabulary");
    }

    [Fact]
    public async Task Provenance_role_marks_primary_and_supporting_resources_in_a_multi_resource_bundle()
    {
        SeedReadingPassage("Quarterly Review", LongPassage, "B1");
        SeedVocabulary("deadline", "B1");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.Resources.Should().Contain(r => r.ResourceType == "ReadingPassage" && r.Role == "primary");
        result.Resources.Should().Contain(r => r.ResourceType == "Vocabulary" && r.Role == "supporting");
    }

    [Fact]
    public async Task Supporting_vocabulary_still_respects_exact_cefr_and_never_widens_upward()
    {
        SeedReadingPassage("Quarterly Review", LongPassage, "B1");
        SeedVocabulary("harder-word", "C1"); // above routed level — must never be selected as support
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.Resources.Should().NotContain(r => r.DisplayText == "harder-word");
    }

    // ── Phase D5 — context-aware selection across lean bank types (E9 metadata) ──

    [Fact]
    public async Task General_learner_does_not_select_workplace_tagged_vocabulary_when_general_exists()
    {
        SeedVocabulary("meeting", "B1", contextTagsJson: "[\"workplace\"]", subskill: "vocabulary.receptive");
        SeedVocabulary("garden", "B1", contextTagsJson: "[\"general\",\"daily\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: _studentId,
            patternKey: PhraseMatchPattern, prefersWorkplaceContext: false));

        result.Resources.Should().Contain(r => r.DisplayText == "garden");
        result.Resources.Should().NotContain(r => r.DisplayText == "meeting");
    }

    [Fact]
    public async Task Workplace_learner_may_select_workplace_tagged_vocabulary()
    {
        SeedVocabulary("meeting", "B1", contextTagsJson: "[\"workplace\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: _studentId,
            patternKey: PhraseMatchPattern, prefersWorkplaceContext: true));

        result.Resources.Should().Contain(r => r.DisplayText == "meeting");
        result.Resources.Should().OnlyContain(r => r.MatchedContextTags != null && r.MatchedContextTags.Contains("workplace"));
    }

    [Fact]
    public async Task General_learner_does_not_select_workplace_tagged_grammar_when_general_exists()
    {
        SeedVocabulary("garden", "B1", contextTagsJson: "[\"general\"]");
        SeedGrammar("Passive voice", "B1", contextTagsJson: "[\"workplace\"]", subskill: "grammar.tense_aspect");
        SeedGrammar("First conditional", "B1", contextTagsJson: "[\"general\"]", subskill: "grammar.tense_aspect");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", secondarySkills: ["Grammar"], studentId: _studentId,
            patternKey: PhraseMatchPattern, prefersWorkplaceContext: false));

        result.Resources.Should().Contain(r => r.ResourceType == "Grammar" && r.DisplayText == "First conditional");
        result.Resources.Should().NotContain(r => r.DisplayText == "Passive voice");
    }

    [Fact]
    public async Task General_learner_does_not_select_workplace_tagged_reading_reference_when_general_exists()
    {
        SeedReading("A workplace memo about a deadline.", "B1", contextTagsJson: "[\"workplace\"]");
        SeedReading("A note about a weekend walk.", "B1", contextTagsJson: "[\"general\",\"daily\"]");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId,
            patternKey: FillInBlanksPattern, prefersWorkplaceContext: false));

        result.Resources.Should().Contain(r => r.ResourceType == "Reading" && r.DisplayText.Contains("weekend walk"));
        result.Resources.Should().NotContain(r => r.DisplayText.Contains("workplace memo"));
    }

    [Fact]
    public async Task Cloze_pattern_context_filters_short_reference_and_never_selects_full_passage()
    {
        SeedReadingPassage("Full", LongPassage, "B1", contextTagsJson: "[\"general\"]");
        SeedReading("A note about a weekend walk.", "B1", contextTagsJson: "[\"general\"]");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId,
            patternKey: FillInBlanksPattern, prefersWorkplaceContext: false));

        result.Resources.Should().NotContain(r => r.ResourceType == "ReadingPassage");
        result.Resources.Should().Contain(r => r.ResourceType == "Reading");
    }

    [Fact]
    public async Task Focus_tag_preference_prefers_a_matching_vocabulary_resource()
    {
        SeedVocabulary("collocate", "B1", contextTagsJson: "[\"general\"]", focusTagsJson: "[\"collocation\"]");
        SeedVocabulary("plainword", "B1", contextTagsJson: "[\"general\"]", focusTagsJson: "[\"word_form\"]");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: _studentId,
            patternKey: PhraseMatchPattern, preferredFocusTags: ["collocation"]));

        result.Resources.Should().Contain(r => r.DisplayText == "collocate");
        result.Resources.Should().NotContain(r => r.DisplayText == "plainword");
    }

    [Fact]
    public async Task Subskill_preference_prefers_a_matching_vocabulary_resource()
    {
        SeedVocabulary("productiveword", "B1", contextTagsJson: "[\"general\"]", subskill: "vocabulary.productive");
        SeedVocabulary("receptiveword", "B1", contextTagsJson: "[\"general\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: _studentId,
            patternKey: PhraseMatchPattern, preferredSubskill: "vocabulary.productive"));

        result.Resources.Should().Contain(r => r.DisplayText == "productiveword");
        result.Resources.Should().NotContain(r => r.DisplayText == "receptiveword");
    }

    [Fact]
    public async Task Difficulty_band_preference_relaxes_when_no_lean_row_carries_it()
    {
        // Lean rows carry no difficulty band → the difficulty filter finds nothing and must relax
        // (drop difficulty first) rather than returning an empty bundle.
        SeedVocabulary("plainword", "B1", contextTagsJson: "[\"general\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: _studentId,
            patternKey: PhraseMatchPattern, preferredDifficultyBand: 3));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        result.Resources.Should().Contain(r => r.DisplayText == "plainword");
    }

    [Fact]
    public async Task Focus_filter_relaxes_to_general_when_no_focus_match_but_context_matches()
    {
        // No resource has the requested focus tag, but a general-context resource exists → relax
        // focus and still return the general resource rather than nothing.
        SeedVocabulary("plainword", "B1", contextTagsJson: "[\"general\"]", focusTagsJson: "[\"word_form\"]");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: _studentId,
            patternKey: PhraseMatchPattern, preferredFocusTags: ["collocation"]));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        result.Resources.Should().Contain(r => r.DisplayText == "plainword");
    }

    [Fact]
    public async Task Metadata_filtering_still_respects_exact_cefr_and_never_widens_upward()
    {
        SeedVocabulary("hardword", "C1", contextTagsJson: "[\"general\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: _studentId,
            patternKey: PhraseMatchPattern, preferredSubskill: "vocabulary.receptive"));

        result.Resources.Should().NotContain(r => r.DisplayText == "hardword");
    }

    [Fact]
    public async Task Metadata_widening_down_only_for_review_scaffold()
    {
        SeedVocabulary("lowerword", "A2", contextTagsJson: "[\"general\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var withoutReview = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: _studentId,
            patternKey: PhraseMatchPattern, preferredSubskill: "vocabulary.receptive", allowLowerLevelReview: false));
        withoutReview.Outcome.Should().Be(TodayBankSelectionOutcome.NoSuitableResources);

        var withReview = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: Guid.NewGuid(),
            patternKey: PhraseMatchPattern, preferredSubskill: "vocabulary.receptive", allowLowerLevelReview: true));
        withReview.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        withReview.Resources.Should().Contain(r => r.DisplayText == "lowerword");
    }

    [Fact]
    public async Task Feedback_exclusion_still_applies_after_metadata_filtering()
    {
        var vocabId = SeedVocabulary("blocked", "B1", contextTagsJson: "[\"general\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var activity = new LearningActivity(ActivityType.VocabularyPractice, ActivitySource.AiGenerated, "t", "B1", "{}");
        activity.SetBankResourceProvenance($"[{{\"type\":\"Vocabulary\",\"id\":\"{vocabId}\"}}]");
        _db.LearningActivities.Add(activity);
        _db.SaveChanges();
        _db.ActivityFeedbackSignals.Add(new ActivityFeedbackSignal(
            _studentId, activity.Id, ActivityFeedbackDifficultyRating.RightLevel, ActivityFeedbackClarityRating.Clear,
            ActivityFeedbackUsefulnessRating.NotUseful, ActivityFeedbackRepeatPreference.Neutral));
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: _studentId, patternKey: PhraseMatchPattern));

        result.Resources.Should().NotContain(r => r.DisplayText == "blocked");
    }

    [Fact]
    public async Task Provenance_records_applied_filters_and_matched_context_tags()
    {
        SeedVocabulary("meeting", "B1", contextTagsJson: "[\"workplace\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: _studentId,
            patternKey: PhraseMatchPattern, prefersWorkplaceContext: true));

        var res = result.Resources.Should().ContainSingle().Subject;
        res.AppliedFilters.Should().Contain("context=workplace");
        res.MatchedContextTags.Should().Contain("workplace");
    }

    [Fact]
    public async Task Pattern_instruction_and_roles_preserved_with_metadata_filtering()
    {
        SeedReadingPassage("General Passage", LongPassage, "B1", contextTagsJson: "[\"general\"]");
        SeedVocabulary("supportword", "B1", contextTagsJson: "[\"general\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.PromptSupplementText.Should().Contain("ONLY the following full reading passage");
        result.Resources.Should().Contain(r => r.ResourceType == "ReadingPassage" && r.Role == "primary");
        result.Resources.Should().Contain(r => r.ResourceType == "Vocabulary" && r.Role == "supporting");
    }

    // ── Phase D6 — topic-aware supporting-resource selection ─────────────────────

    [Fact]
    public async Task Passage_topic_anchors_supporting_vocabulary_to_the_passage_context()
    {
        // A travel passage should pull travel-context supporting vocabulary, not the generic word,
        // even though both are level-appropriate — deterministic context-tag topic matching.
        SeedReadingPassage("Trip Abroad", LongPassage, "B1", contextTagsJson: "[\"travel\"]");
        SeedVocabulary("itinerary", "B1", contextTagsJson: "[\"travel\"]", subskill: "vocabulary.receptive");
        SeedVocabulary("generic", "B1", contextTagsJson: "[\"general\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.Resources.Should().Contain(r => r.ResourceType == "Vocabulary" && r.DisplayText == "itinerary");
        result.Resources.Should().NotContain(r => r.ResourceType == "Vocabulary" && r.DisplayText == "generic");
    }

    [Fact]
    public async Task Passage_topic_anchor_provenance_records_topic_anchor_context()
    {
        SeedReadingPassage("Trip Abroad", LongPassage, "B1", contextTagsJson: "[\"travel\"]");
        SeedVocabulary("itinerary", "B1", contextTagsJson: "[\"travel\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        var vocab = result.Resources.Should().ContainSingle(r => r.ResourceType == "Vocabulary").Subject;
        vocab.AppliedFilters.Should().Contain("context=travel(topic-anchor)");
        vocab.MatchedContextTags.Should().Contain("travel");
    }

    [Fact]
    public async Task Passage_topic_anchor_relaxes_to_general_when_no_context_match_exists()
    {
        // Travel passage but only a general supporting word exists → the anchor rung finds nothing and
        // must relax to the general ladder rather than dropping the supporting vocabulary entirely.
        SeedReadingPassage("Trip Abroad", LongPassage, "B1", contextTagsJson: "[\"travel\"]");
        SeedVocabulary("generic", "B1", contextTagsJson: "[\"general\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FullPassagePattern));

        result.Outcome.Should().Be(TodayBankSelectionOutcome.BankResourcesFound);
        result.Resources.Should().Contain(r => r.ResourceType == "Vocabulary" && r.DisplayText == "generic");
    }

    [Fact]
    public async Task Reference_bundle_topic_anchors_supporting_vocabulary_to_the_reference_context()
    {
        // Cloze pattern → short reference primary. The reference's travel context should anchor the
        // supporting vocabulary the same way a full passage does.
        SeedReading("Booking a flight and a hotel for the summer.", "B1", contextTagsJson: "[\"travel\"]");
        SeedVocabulary("itinerary", "B1", contextTagsJson: "[\"travel\"]", subskill: "vocabulary.receptive");
        SeedVocabulary("generic", "B1", contextTagsJson: "[\"general\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId, patternKey: FillInBlanksPattern));

        result.Resources.Should().Contain(r => r.ResourceType == "Vocabulary" && r.DisplayText == "itinerary");
        result.Resources.Should().NotContain(r => r.ResourceType == "Vocabulary" && r.DisplayText == "generic");
    }

    [Fact]
    public async Task Passage_topic_anchor_still_excludes_workplace_vocabulary_for_general_learner()
    {
        // Even when anchoring on a topical context, the D5 general-English default still excludes
        // workplace-tagged supporting rows for a non-workplace learner.
        SeedReadingPassage("Trip Abroad", LongPassage, "B1", contextTagsJson: "[\"travel\"]");
        SeedVocabulary("meeting", "B1", contextTagsJson: "[\"workplace\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Reading", studentId: _studentId,
            patternKey: FullPassagePattern, prefersWorkplaceContext: false));

        result.Resources.Should().NotContain(r => r.ResourceType == "Vocabulary" && r.DisplayText == "meeting");
    }

    [Fact]
    public async Task Difficulty_band_preference_prefers_matching_band_when_rows_carry_mixed_bands()
    {
        // With mixed difficulty bands at the same CEFR (contrast with the E10-uniform limitation), the
        // difficulty filter selects the requested band rather than relaxing.
        SeedVocabulary("harderword", "B1", contextTagsJson: "[\"general\"]", subskill: "vocabulary.receptive", difficultyBand: 4);
        SeedVocabulary("easierword", "B1", contextTagsJson: "[\"general\"]", subskill: "vocabulary.receptive", difficultyBand: 3);
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: _studentId,
            patternKey: PhraseMatchPattern, preferredDifficultyBand: 4));

        result.Resources.Should().Contain(r => r.DisplayText == "harderword");
        result.Resources.Should().NotContain(r => r.DisplayText == "easierword");
    }

    [Fact]
    public async Task Vocabulary_primary_bundle_is_not_topic_anchored_to_its_opportunistic_reference()
    {
        // Vocabulary is primary here; the opportunistic reference is supporting. Vocabulary selection
        // must not be constrained by any reference context (no anchoring on a vocabulary-primary bundle).
        SeedVocabulary("primaryword", "B1", contextTagsJson: "[\"general\"]", subskill: "vocabulary.receptive");
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(
            cefrLevel: "B1", primarySkill: "Vocabulary", studentId: _studentId, patternKey: PhraseMatchPattern));

        var vocab = result.Resources.Should().ContainSingle(r => r.ResourceType == "Vocabulary").Subject;
        vocab.AppliedFilters.Should().NotContain("topic-anchor");
    }
}
