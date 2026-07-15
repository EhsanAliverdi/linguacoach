using System.Text.Json;
using LinguaCoach.Domain;
using LinguaCoach.Infrastructure.Admin;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Sessions;

public sealed class ExerciseTypeCatalogTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LinguaCoachDbContext _db;

    public ExerciseTypeCatalogTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.EnsureCreated();
        ExerciseTypeDefinitionSeeder.SeedAsync(_db, NullLogger.Instance).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Seeder_Seeds_AllRequiredExerciseTypes()
    {
        var keys = await _db.ExerciseTypeDefinitions.Select(e => e.Key).ToListAsync();

        // 36 legacy/pattern exercise types + 1 Form.io Practice Gym pilot (ImplementationStatus="planned",
        // inert by default — see docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md) +
        // 3 Phase K15 "BankFirst" types (gap_fill, multiple_choice_single, short_answer) that are
        // actually wired to Lesson "Generate Exercises" today.
        Assert.Equal(40, keys.Count);
        Assert.Contains("listening_comprehension", keys);
        Assert.Contains("writing_scenario", keys);
        Assert.Contains("write_essay", keys);
        Assert.Contains("write_from_dictation", keys);
        Assert.Contains("gap_fill", keys);
        Assert.Contains("multiple_choice_single", keys);
        Assert.Contains("short_answer", keys);
    }

    [Theory]
    [InlineData("read_aloud", "speaking", "reading")]
    [InlineData("repeat_sentence", "speaking", "listening")]
    [InlineData("summarize_written_text", "writing", "reading")]
    [InlineData("reading_writing_fill_in_blanks", "reading", "writing")]
    [InlineData("summarize_spoken_text", "listening", "writing")]
    [InlineData("highlight_correct_summary", "listening", "reading")]
    [InlineData("write_from_dictation", "listening", "writing")]
    public async Task PteTypes_HaveExpectedSkillMapping(string key, string primary, string secondary)
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == key);
        var secondaries = JsonSerializer.Deserialize<List<string>>(type.SecondarySkillsJson) ?? [];

        Assert.Equal(primary, type.PrimarySkill);
        Assert.Contains(secondary, secondaries);
    }

    [Fact]
    public async Task BankFirstTypes_AreGenerationEligibleByDefault()
    {
        // Phase K15 — gap_fill/multiple_choice_single/short_answer are the only catalog entries
        // with a real Lesson-generation composer today, so they're the only ones enabled by
        // default. Every legacy/pattern entry below is seeded disabled until its own composer
        // ships (see docs/sprints/exercise-type-catalog-lesson-generation-buildout-sprint.md).
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Contains(eligible, e => e.Key == "gap_fill");
        Assert.Contains(eligible, e => e.Key == "multiple_choice_single");
        Assert.Contains(eligible, e => e.Key == "short_answer");
    }

    [Fact]
    public async Task ReadingFillInBlanks_IsBankFirstAndGenerationEligible_PhaseK16()
    {
        // Phase K16 — reading_fill_in_blanks got a real composer
        // (ActivityGenerationService.ComposeReadingFillInBlanksAsync) and moved from the
        // disabled-by-default Pattern bucket into BankFirst/enabled.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "reading_fill_in_blanks");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("reading", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "reading_fill_in_blanks");
    }

    [Fact]
    public async Task ReadingMultipleChoiceSingle_IsBankFirstAndGenerationEligible_PhaseK17()
    {
        // Phase K17 — reading_multiple_choice_single got a real (AI-assisted) composer
        // (AiExerciseGenerationService.ComposeReadingMultipleChoiceSingle) and moved from the
        // disabled-by-default Pattern bucket into BankFirst/enabled.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "reading_multiple_choice_single");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("reading", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "reading_multiple_choice_single");
    }

    [Fact]
    public async Task ReadingMultipleChoiceMulti_IsBankFirstAndGenerationEligible_PhaseK17()
    {
        // Phase K17 — reading_multiple_choice_multi got a real (AI-assisted) composer
        // (AiExerciseGenerationService.ComposeReadingMultipleChoiceMulti) and moved from the
        // disabled-by-default Pattern bucket into BankFirst/enabled.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "reading_multiple_choice_multi");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("reading", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "reading_multiple_choice_multi");
    }

    [Theory]
    [InlineData("email_reply")]
    [InlineData("open_writing_task")]
    [InlineData("write_essay")]
    public async Task WritingComposerTypes_AreBankFirstAndGenerationEligible_PhaseK17(string key)
    {
        // Phase K17 — email_reply/open_writing_task/write_essay got real (deterministic) composers
        // (ActivityGenerationService.ComposeWritingPrompt) and moved from the disabled-by-default
        // Pattern bucket into BankFirst/enabled.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == key);
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("writing", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == key);
    }

    [Fact]
    public async Task SummarizeWrittenText_IsBankFirstAndGenerationEligible_PhaseK17()
    {
        // Phase K17 — summarize_written_text got a real composer
        // (ActivityGenerationService.ComposeSummarizeWrittenText) and moved from the
        // disabled-by-default Pattern bucket into BankFirst/enabled. Writing-skill but
        // Reading-resource-sourced, so secondarySkills still carries "reading".
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "summarize_written_text");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("writing", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "summarize_written_text");
    }

    [Theory]
    [InlineData("listening_fill_in_blanks")]
    [InlineData("listening_multiple_choice_single")]
    [InlineData("listening_multiple_choice_multi")]
    public async Task ListeningComposerTypes_AreBankFirstAndGenerationEligible_PhaseK17(string key)
    {
        // Phase K17 — the 3 Listening comprehension types with real composers
        // (ActivityGenerationService.ComposeListeningFillInBlanks, deterministic; and the two
        // AI-assisted MC types reusing the reading composers) moved from disabled-Pattern to
        // BankFirst/enabled.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == key);
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("listening", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == key);
    }

    [Theory]
    [InlineData("highlight_correct_summary")]
    [InlineData("select_missing_word")]
    public async Task ListeningAiAssistedTypes_AreBankFirstAndGenerationEligible_PhaseK17(string key)
    {
        // Phase K17 — highlight_correct_summary reuses ComposeReadingMultipleChoiceSingle (same
        // AI-supplies-the-answer exception); select_missing_word has a deterministic correct
        // answer (PickBlankWord) with AI only supplying wrong-word distractors, same safe shape
        // as multiple_choice_single.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == key);
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("listening", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == key);
    }

    [Theory]
    [InlineData("spoken_response_from_prompt")]
    [InlineData("respond_to_situation")]
    [InlineData("answer_short_question")]
    [InlineData("speaking_roleplay_turn")]
    [InlineData("read_aloud")]
    public async Task SpeakingComposerTypes_AreBankFirstAndGenerationEligible_PhaseK18(string key)
    {
        // Phase K18 — deterministic, Speaking resources only
        // (ActivityGenerationService.ComposeSpeakingPrompt) — shows the resource's own prompt
        // text verbatim, honestly unscored (RequiresManualOrAiEvaluation) since real audio
        // scoring isn't wired into the bank-first pipeline yet.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == key);
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("speaking", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == key);
    }

    [Theory]
    [InlineData("summarize_spoken_text")]
    [InlineData("retell_lecture")]
    [InlineData("summarize_group_discussion")]
    public async Task OpenEndedListeningTypes_AreBankFirstAndGenerationEligible_PhaseK18(string key)
    {
        // Phase K18 — deterministic, Listening resources only, reusing
        // ComposeWritingPrompt/ComposeSpeakingPrompt unchanged against the resource's own
        // transcript — no audio playback involved.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == key);
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("listening", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == key);
    }

    [Fact]
    public async Task ReadingWritingFillInBlanks_IsBankFirstAndGenerationEligible_PhaseK18()
    {
        // Phase K18 — reading_writing_fill_in_blanks got a real composer
        // (ActivityGenerationService.ComposeReadingWritingFillInBlanksAsync — "choose" not "type")
        // and moved from disabled-Pattern to BankFirst/enabled.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "reading_writing_fill_in_blanks");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("reading", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "reading_writing_fill_in_blanks");
    }

    [Fact]
    public async Task PhraseMatch_IsBankFirstAndGenerationEligible_PhaseK16()
    {
        // Phase K16 — phrase_match got a real composer (decomposed into N single_choice
        // sub-questions, ActivityGenerationService.ComposePhraseMatchAsync) and moved from
        // disabled-Pattern to BankFirst/enabled.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "phrase_match");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("vocabulary", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "phrase_match");
    }

    [Fact]
    public async Task ReorderParagraphs_IsBankFirstAndGenerationEligible_PhaseK16()
    {
        // Phase K16 — reorder_paragraphs got a real composer (stock Form.io datagrid+reorder
        // pattern, ActivityGenerationService.ComposeReorderParagraphsAsync) and moved from
        // disabled-Pattern to BankFirst/enabled. Frontend rendering behavior flagged for manual
        // verification — not verified live this session.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "reorder_paragraphs");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("reading", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "reorder_paragraphs");
    }

    [Fact]
    public async Task LessonReflection_IsBankFirstAndGenerationEligible_PhaseK19()
    {
        // Phase K19 — lesson_reflection got a real composer sourced from the Lesson's own
        // Body/Title, not a Resource Bank row (ActivityGenerationService.
        // ComposeAndSaveLessonReflectionAsync) and moved from disabled-Pattern to
        // BankFirst/enabled.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "lesson_reflection");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("reflection", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "lesson_reflection");
    }

    [Fact]
    public async Task DescribeImage_IsBankFirstAndGenerationEligible_PhaseK20()
    {
        // Phase K20 — describe_image got a real composer sourced from the Speaking resource's
        // new optional ImageUrl field and moved from disabled-Pattern to BankFirst/enabled.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "describe_image");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("speaking", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "describe_image");
    }

    [Fact]
    public async Task TeamsChatSimulation_IsBankFirstAndGenerationEligible_PhaseK20()
    {
        // Phase K20 — teams_chat_simulation got a real composer (reuses ComposeWritingPrompt,
        // simplified to single-turn) and moved from disabled-Pattern to BankFirst/enabled.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "teams_chat_simulation");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("writing", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "teams_chat_simulation");
    }

    [Fact]
    public async Task HighlightIncorrectWords_IsBankFirstAndGenerationEligible_PhaseK21()
    {
        // Phase K21 — highlight_incorrect_words got a real composer (rotates real transcript
        // words into altered positions, streams the resource's own audio via the new Phase K21
        // audio-serving bridge) and moved from disabled-Pattern to BankFirst/enabled.
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "highlight_incorrect_words");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("BankFirst", type.Category);
        Assert.True(type.IsEnabled);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Equal("listening", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "highlight_incorrect_words");
    }

    [Fact]
    public async Task LegacyAndPatternTypes_AreDisabledByDefault_DespiteBeingReady()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "repeat_sentence");
        Assert.Equal("ready", type.ImplementationStatus);
        Assert.False(type.IsEnabled);
        Assert.False(type.IsAvailableForGeneration);
        Assert.DoesNotContain(eligible, e => e.Key == "repeat_sentence");
    }

    [Theory]
    [InlineData("write_from_dictation", "listening")]
    [InlineData("repeat_sentence", "speaking")]
    public async Task PatternTypes_AreReadyButNotEligibleUntilAdminEnables(string key, string expectedSkill)
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == key);
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.Equal(expectedSkill, type.PrimarySkill);
        Assert.False(type.IsAvailableForGeneration);
        Assert.DoesNotContain(eligible, e => e.Key == key);
    }

    [Fact]
    public async Task AllPlannedSpeakingListeningFormats_AreNowReady()
    {
        // Phases 9A–9I promoted all planned speaking/listening formats to Ready.
        // Exception: formio_practice_gym_pilot is intentionally kept "planned" — an inert,
        // admin-gated pilot pattern (see docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md).
        var speakingTypes = await _db.ExerciseTypeDefinitions
            .Where(e => e.PrimarySkill == "listening" || e.PrimarySkill == "speaking")
            .Where(e => e.ImplementationStatus == "planned")
            .Where(e => e.Key != ExercisePatternKey.FormIoPracticeGymPilot)
            .ToListAsync();

        Assert.Empty(speakingTypes);
    }

    [Fact]
    public async Task AllReadingPrimaryTypes_AreNowReady()
    {
        // All reading-primary exercise types have been promoted to Ready in Phases 8A-8E.
        // This test verifies no reading-primary type is left in planned status.
        var readingTypes = await _db.ExerciseTypeDefinitions
            .Where(e => e.PrimarySkill == "reading")
            .ToListAsync();

        Assert.NotEmpty(readingTypes);
        Assert.All(readingTypes, e => Assert.Equal("ready", e.ImplementationStatus));
    }

    [Fact]
    public async Task AllSpeakingAndListeningTypes_AreNowReady_NoPlannedRemain()
    {
        // Phase 9I: summarize_group_discussion was the last planned speaking/listening format.
        // Exception: formio_practice_gym_pilot is intentionally kept "planned" — an inert,
        // admin-gated pilot pattern (see docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md).
        var planned = await _db.ExerciseTypeDefinitions
            .Where(e => e.ImplementationStatus == "planned")
            .Where(e => e.Key != ExercisePatternKey.FormIoPracticeGymPilot)
            .ToListAsync();

        Assert.Empty(planned);
    }

    [Fact]
    public async Task AdminToggle_EnablesAndDisablesGenerationWithoutDeletingActivities()
    {
        var service = new ExerciseTypeCatalogService(_db);
        await service.UpdateAsync(new("gap_fill", false));

        var type = await service.GetByKeyAsync("gap_fill");
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.NotNull(type);
        Assert.False(type!.IsEnabled);
        Assert.False(type.IsAvailableForGeneration);
        Assert.DoesNotContain(eligible, e => e.Key == "gap_fill");
    }

    // ── Phase 8N: configurable practice item counts ──────────────────────────

    [Theory]
    [InlineData("reading_multiple_choice_single", 1, 1, 1, 3, 4, 5)]
    [InlineData("reading_fill_in_blanks", 3, 4, 6, 3, 4, 5)]
    [InlineData("reorder_paragraphs", 4, 4, 5, 0, 0, 0)]
    [InlineData("highlight_incorrect_words", 2, 3, 4, 0, 0, 0)]
    [InlineData("write_from_dictation", 2, 3, 5, 0, 0, 0)]
    [InlineData("answer_short_question", 3, 5, 8, 0, 0, 0)]
    [InlineData("repeat_sentence", 3, 5, 6, 0, 0, 0)]
    [InlineData("respond_to_situation", 1, 1, 2, 0, 0, 0)]
    [InlineData("describe_image", 1, 1, 1, 0, 0, 0)]
    [InlineData("retell_lecture", 1, 1, 1, 0, 0, 0)]
    [InlineData("summarize_group_discussion", 1, 1, 1, 0, 0, 0)]
    public async Task Seeder_SeedsCountFields(string key, int minI, int defI, int maxI, int minO, int defO, int maxO)
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == key);

        Assert.Equal(minI, type.MinItemsPerPractice);
        Assert.Equal(defI, type.DefaultItemsPerPractice);
        Assert.Equal(maxI, type.MaxItemsPerPractice);
        Assert.Equal(minO, type.MinOptionsPerItem);
        Assert.Equal(defO, type.DefaultOptionsPerItem);
        Assert.Equal(maxO, type.MaxOptionsPerItem);
    }

    [Fact]
    public async Task AllTypes_HaveValidCountRanges()
    {
        var all = await _db.ExerciseTypeDefinitions.ToListAsync();

        Assert.All(all, e =>
        {
            Assert.True(e.MinItemsPerPractice >= 0);
            Assert.True(e.MinItemsPerPractice <= e.DefaultItemsPerPractice);
            Assert.True(e.DefaultItemsPerPractice <= e.MaxItemsPerPractice);
            Assert.True(e.MinOptionsPerItem >= 0);
            Assert.True(e.MinOptionsPerItem <= e.DefaultOptionsPerItem);
            Assert.True(e.DefaultOptionsPerItem <= e.MaxOptionsPerItem);
        });
    }

    [Fact]
    public async Task UpdateAsync_ValidCountRange_Succeeds()
    {
        var service = new ExerciseTypeCatalogService(_db);

        var updated = await service.UpdateAsync(new("reading_fill_in_blanks", null,
            MinItemsPerPractice: 2, DefaultItemsPerPractice: 3, MaxItemsPerPractice: 5,
            MinOptionsPerItem: 2, DefaultOptionsPerItem: 3, MaxOptionsPerItem: 4));

        Assert.Equal(2, updated.MinItemsPerPractice);
        Assert.Equal(5, updated.MaxItemsPerPractice);
        Assert.Equal(4, updated.MaxOptionsPerItem);
    }

    [Fact]
    public async Task UpdateAsync_InvalidRange_MinAboveMax_Throws()
    {
        var service = new ExerciseTypeCatalogService(_db);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.UpdateAsync(
            new("reading_fill_in_blanks", null,
                MinItemsPerPractice: 6, DefaultItemsPerPractice: 6, MaxItemsPerPractice: 3)));
    }

    [Fact]
    public async Task UpdateAsync_NegativeValue_Throws()
    {
        var service = new ExerciseTypeCatalogService(_db);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.UpdateAsync(
            new("reading_fill_in_blanks", null,
                MinItemsPerPractice: -1, DefaultItemsPerPractice: 0, MaxItemsPerPractice: 1)));
    }

    [Fact]
    public async Task UpdateAsync_CountUpdate_DoesNotChangeImplementationStatus()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var before = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "write_from_dictation");
        var statusBefore = before.ImplementationStatus;

        var updated = await service.UpdateAsync(new("write_from_dictation", null,
            MinItemsPerPractice: 1, DefaultItemsPerPractice: 2, MaxItemsPerPractice: 4));

        Assert.Equal(statusBefore, updated.ImplementationStatus);
        Assert.Equal("ready", updated.ImplementationStatus);
    }
}

public sealed class ExerciseTypeRegistryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LinguaCoachDbContext _db;

    public ExerciseTypeRegistryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.EnsureCreated();
        ExerciseTypeDefinitionSeeder.SeedAsync(_db, NullLogger.Instance).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Theory]
    [InlineData("writing_scenario", "writing", null, "WritingScenario")]
    [InlineData("LISTENING COMPREHENSION", "listening", null, "ListeningComprehension")]
    public async Task Registry_Resolves_ReadyTypes(string key, string skill, string? pattern, string legacy)
    {
        var registry = new LinguaCoach.Infrastructure.Activity.ExerciseTypeRegistry(_db);

        var resolved = await registry.GetByKeyAsync(key);

        Assert.NotNull(resolved);
        Assert.Equal(skill, resolved!.PrimarySkill);
        Assert.Equal(pattern, resolved.ExercisePatternKey);
        Assert.Equal(legacy, resolved.LegacyActivityType?.ToString());
        // Phase K15 — these legacy/pattern types resolve fine but are disabled by default
        // (no Lesson-generation composer yet); IsAvailableForGeneration is deliberately not
        // asserted true here anymore.
    }

    [Fact]
    public async Task Registry_SummarizeGroupDiscussion_IsReadyAndEligible_PhaseK18()
    {
        // Phase K18 — summarize_group_discussion got a real composer (reuses
        // ComposeSpeakingPrompt against the transcript) and moved from disabled-Pattern to
        // BankFirst/enabled.
        var registry = new LinguaCoach.Infrastructure.Activity.ExerciseTypeRegistry(_db);

        var def = await registry.GetByKeyAsync("summarize_group_discussion");
        var eligible = await registry.GetGenerationEligibleAsync();

        Assert.NotNull(def);
        Assert.Equal("ready", def!.ImplementationStatus);
        Assert.Contains(eligible, e => e.Key == "summarize_group_discussion");
    }

    [Fact]
    public async Task Registry_ReturnsEligibleExerciseTypesForSkillOnly()
    {
        // Phase K15 — only the 3 BankFirst types are enabled by default; skill filtering is
        // exercised against those instead of the disabled-by-default legacy/pattern catalog.
        var registry = new LinguaCoach.Infrastructure.Activity.ExerciseTypeRegistry(_db);

        var vocabulary = await registry.GetEligibleExerciseTypesForSkillAsync("vocabulary");
        var reading = await registry.GetEligibleExerciseTypesForSkillAsync("reading");

        Assert.Contains(vocabulary, e => e.Key == "gap_fill");
        Assert.Contains(vocabulary, e => e.Key == "multiple_choice_single");
        Assert.DoesNotContain(reading, e => e.Key == "gap_fill");
        Assert.Contains(reading, e => e.Key == "short_answer");
    }

    [Fact]
    public async Task Registry_Entry_IncludesCountFields()
    {
        var registry = new LinguaCoach.Infrastructure.Activity.ExerciseTypeRegistry(_db);

        var entry = await registry.GetByKeyAsync("reading_fill_in_blanks");

        Assert.NotNull(entry);
        Assert.Equal(3, entry!.MinItemsPerPractice);
        Assert.Equal(4, entry.DefaultItemsPerPractice);
        Assert.Equal(6, entry.MaxItemsPerPractice);
        Assert.Equal(3, entry.MinOptionsPerItem);
        Assert.Equal(4, entry.DefaultOptionsPerItem);
        Assert.Equal(5, entry.MaxOptionsPerItem);
    }
}
