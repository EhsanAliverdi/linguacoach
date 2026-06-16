using System.Text.Json;
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

        Assert.Equal(36, keys.Count);
        Assert.Contains("listening_comprehension", keys);
        Assert.Contains("writing_scenario", keys);
        Assert.Contains("write_essay", keys);
        Assert.Contains("write_from_dictation", keys);
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
    public async Task FutureTypes_AreNotGenerationEligibleUntilReady()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Contains(eligible, e => e.Key == "email_reply");
    }

    [Fact]
    public async Task ReadingMultipleChoiceSingle_IsReadyAndEligible()
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "reading_multiple_choice_single");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.True(type.SupportsPracticeGym);
        Assert.False(type.SupportsTodayLesson);
        Assert.Equal("reading", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "reading_multiple_choice_single");
    }

    [Fact]
    public async Task ReadingMultipleChoiceMulti_IsReadyAndEligible()
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "reading_multiple_choice_multi");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.True(type.SupportsPracticeGym);
        Assert.False(type.SupportsTodayLesson);
        Assert.Equal("reading", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "reading_multiple_choice_multi");
    }

    [Fact]
    public async Task ReadingFillInBlanks_IsReadyAndEligible()
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "reading_fill_in_blanks");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.True(type.SupportsPracticeGym);
        Assert.False(type.SupportsTodayLesson);
        Assert.Equal("reading", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "reading_fill_in_blanks");
    }

    [Fact]
    public async Task ReorderParagraphs_IsReadyAndEligible()
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "reorder_paragraphs");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.True(type.SupportsPracticeGym);
        Assert.False(type.SupportsTodayLesson);
        Assert.Equal("reading", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "reorder_paragraphs");
    }

    [Fact]
    public async Task ReadingWritingFillInBlanks_IsReadyAndEligible()
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "reading_writing_fill_in_blanks");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.True(type.SupportsPracticeGym);
        Assert.False(type.SupportsTodayLesson);
        Assert.Equal("reading", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "reading_writing_fill_in_blanks");
    }

    [Fact]
    public async Task SummarizeWrittenText_IsReadyAndEligible()
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "summarize_written_text");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.True(type.SupportsPracticeGym);
        Assert.False(type.SupportsTodayLesson);
        Assert.Equal("writing", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "summarize_written_text");
    }

    [Fact]
    public async Task WriteEssay_IsReadyAndEligible()
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "write_essay");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.True(type.SupportsPracticeGym);
        Assert.False(type.SupportsTodayLesson);
        Assert.Equal("writing", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "write_essay");
    }

    [Fact]
    public async Task ListeningMultipleChoiceSingle_IsReadyAndEligible()
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "listening_multiple_choice_single");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.True(type.SupportsPracticeGym);
        Assert.False(type.SupportsTodayLesson);
        Assert.Equal("listening", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "listening_multiple_choice_single");
    }

    [Fact]
    public async Task ListeningMultipleChoiceMulti_IsReadyAndEligible()
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "listening_multiple_choice_multi");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.True(type.SupportsPracticeGym);
        Assert.False(type.SupportsTodayLesson);
        Assert.Equal("listening", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "listening_multiple_choice_multi");
    }

    [Fact]
    public async Task ListeningFillInBlanks_IsReadyAndEligible()
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "listening_fill_in_blanks");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.True(type.SupportsPracticeGym);
        Assert.False(type.SupportsTodayLesson);
        Assert.Equal("listening", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "listening_fill_in_blanks");
    }

    [Fact]
    public async Task SelectMissingWord_IsReadyAndEligible()
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "select_missing_word");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.True(type.SupportsPracticeGym);
        Assert.False(type.SupportsTodayLesson);
        Assert.Equal("listening", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "select_missing_word");
    }

    [Fact]
    public async Task HighlightCorrectSummary_IsReadyAndEligible()
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "highlight_correct_summary");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.True(type.SupportsPracticeGym);
        Assert.False(type.SupportsTodayLesson);
        Assert.Equal("listening", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "highlight_correct_summary");
    }

    [Fact]
    public async Task HighlightIncorrectWords_IsReadyAndEligible()
    {
        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "highlight_incorrect_words");
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.True(type.SupportsPracticeGym);
        Assert.False(type.SupportsTodayLesson);
        Assert.Equal("listening", type.PrimarySkill);
        Assert.Contains(eligible, e => e.Key == "highlight_incorrect_words");
    }

    [Fact]
    public async Task WriteFromDictation_IsNowRunnable()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "write_from_dictation");
        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Contains(eligible, e => e.Key == "write_from_dictation");
    }

    [Fact]
    public async Task SummarizeSpokenText_IsNowRunnable()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "summarize_spoken_text");
        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Contains(eligible, e => e.Key == "summarize_spoken_text");
    }

    [Fact]
    public async Task AnswerShortQuestion_IsNowRunnable()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "answer_short_question");
        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Contains(eligible, e => e.Key == "answer_short_question");
    }

    [Fact]
    public async Task ReadAloud_IsNowRunnable()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "read_aloud");
        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Contains(eligible, e => e.Key == "read_aloud");
    }

    [Fact]
    public async Task RepeatSentence_IsNowRunnable()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "repeat_sentence");
        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Contains(eligible, e => e.Key == "repeat_sentence");
    }

    [Fact]
    public async Task RespondToSituation_IsNowRunnable()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "respond_to_situation");
        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Contains(eligible, e => e.Key == "respond_to_situation");
    }

    [Fact]
    public async Task DescribeImage_IsNowRunnable()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "describe_image");
        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Contains(eligible, e => e.Key == "describe_image");
    }

    [Fact]
    public async Task RetellLecture_IsNowRunnable()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "retell_lecture");
        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Contains(eligible, e => e.Key == "retell_lecture");
    }

    [Fact]
    public async Task SummarizeGroupDiscussion_IsNowRunnable()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "summarize_group_discussion");
        Assert.Equal("ready", type.ImplementationStatus);
        Assert.True(type.IsAvailableForGeneration);
        Assert.Contains(eligible, e => e.Key == "summarize_group_discussion");
    }

    [Fact]
    public async Task AllPlannedSpeakingListeningFormats_AreNowReady()
    {
        // Phases 9A–9I promoted all planned speaking/listening formats to Ready.
        var speakingTypes = await _db.ExerciseTypeDefinitions
            .Where(e => e.PrimarySkill == "listening" || e.PrimarySkill == "speaking")
            .Where(e => e.ImplementationStatus == "planned")
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
        // All exercise types in the catalog are now Ready.
        var planned = await _db.ExerciseTypeDefinitions
            .Where(e => e.ImplementationStatus == "planned")
            .ToListAsync();

        Assert.Empty(planned);
    }

    [Fact]
    public async Task AdminToggle_DisablesFutureGenerationWithoutDeletingActivities()
    {
        var service = new ExerciseTypeCatalogService(_db);
        await service.UpdateAsync(new("email_reply", false, null, null));

        var type = await service.GetByKeyAsync("email_reply");
        var eligible = await service.GetGenerationEligibleAsync();

        Assert.NotNull(type);
        Assert.False(type!.IsEnabled);
        Assert.False(type.IsAvailableForGeneration);
        Assert.DoesNotContain(eligible, e => e.Key == "email_reply");
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

        var updated = await service.UpdateAsync(new("reading_fill_in_blanks", null, null, null,
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
            new("reading_fill_in_blanks", null, null, null,
                MinItemsPerPractice: 6, DefaultItemsPerPractice: 6, MaxItemsPerPractice: 3)));
    }

    [Fact]
    public async Task UpdateAsync_NegativeValue_Throws()
    {
        var service = new ExerciseTypeCatalogService(_db);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.UpdateAsync(
            new("reading_fill_in_blanks", null, null, null,
                MinItemsPerPractice: -1, DefaultItemsPerPractice: 0, MaxItemsPerPractice: 1)));
    }

    [Fact]
    public async Task UpdateAsync_CountUpdate_DoesNotChangeImplementationStatus()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var before = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "write_from_dictation");
        var statusBefore = before.ImplementationStatus;

        var updated = await service.UpdateAsync(new("write_from_dictation", null, null, null,
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
    [InlineData("phrase_match", "vocabulary", "phrase_match", "VocabularyPractice")]
    [InlineData("email_reply", "writing", "email_reply", "WritingScenario")]
    public async Task Registry_Resolves_ReadyTypes(string key, string skill, string? pattern, string legacy)
    {
        var registry = new LinguaCoach.Infrastructure.Activity.ExerciseTypeRegistry(_db);

        var resolved = await registry.GetByKeyAsync(key);

        Assert.NotNull(resolved);
        Assert.Equal(skill, resolved!.PrimarySkill);
        Assert.Equal(pattern, resolved.ExercisePatternKey);
        Assert.Equal(legacy, resolved.LegacyActivityType?.ToString());
        Assert.True(resolved.IsAvailableForGeneration);
    }

    [Fact]
    public async Task Registry_SummarizeGroupDiscussion_IsNowEligible()
    {
        var registry = new LinguaCoach.Infrastructure.Activity.ExerciseTypeRegistry(_db);

        var def = await registry.GetByKeyAsync("summarize_group_discussion");
        var eligible = await registry.GetGenerationEligibleAsync();

        Assert.NotNull(def);
        Assert.Equal("ready", def!.ImplementationStatus);
        Assert.Contains(eligible, e => e.Key == "summarize_group_discussion");
    }

    [Fact]
    public async Task Registry_FiltersDisabledAndContextSpecificTypes()
    {
        var catalog = new ExerciseTypeCatalogService(_db);
        await catalog.UpdateAsync(new("email_reply", false, null, null));
        var registry = new LinguaCoach.Infrastructure.Activity.ExerciseTypeRegistry(_db);

        var practice = await registry.GetForPracticeGymAsync();
        var today = await registry.GetForTodayAsync();

        Assert.DoesNotContain(practice, e => e.Key == "email_reply");
        Assert.Contains(today, e => e.Key == "lesson_reflection");
        Assert.DoesNotContain(practice, e => e.Key == "lesson_reflection");
    }

    [Theory]
    [InlineData("listening")]
    [InlineData("writing")]
    public async Task Registry_SelectsPracticeGymSkill_FromReadyEligibleRows(string skill)
    {
        var registry = new LinguaCoach.Infrastructure.Activity.ExerciseTypeRegistry(_db);

        var selected = await registry.SelectForPracticeGymSkillAsync(skill);

        Assert.NotNull(selected);
        Assert.Equal(skill, selected!.PrimarySkill);
        Assert.True(selected.IsEnabled);
        Assert.Equal("ready", selected.ImplementationStatus);
        Assert.True(selected.IsAvailableForGeneration);
        Assert.True(selected.SupportsPracticeGym);
    }

    [Fact]
    public async Task Registry_SelectPracticeGymSkill_ExcludesDisabledPlannedAndUnsupportedRows()
    {
        var catalog = new ExerciseTypeCatalogService(_db);
        await catalog.UpdateAsync(new("listen_and_answer", false, null, null));
        await catalog.UpdateAsync(new("listen_and_gap_fill", false, null, null));
        await catalog.UpdateAsync(new("listening_multiple_choice_single", false, null, null));
        await catalog.UpdateAsync(new("listening_multiple_choice_multi", false, null, null));
        await catalog.UpdateAsync(new("listening_fill_in_blanks", false, null, null));
        await catalog.UpdateAsync(new("select_missing_word", false, null, null));
        await catalog.UpdateAsync(new("highlight_correct_summary", false, null, null));
        await catalog.UpdateAsync(new("highlight_incorrect_words", false, null, null));
        await catalog.UpdateAsync(new("write_from_dictation", false, null, null));
        await catalog.UpdateAsync(new("summarize_spoken_text", false, null, null));
        await catalog.UpdateAsync(new("retell_lecture", false, null, null));
        await catalog.UpdateAsync(new("summarize_group_discussion", false, null, null));
        var registry = new LinguaCoach.Infrastructure.Activity.ExerciseTypeRegistry(_db);

        var selected = await registry.SelectForPracticeGymSkillAsync("listening");

        Assert.Null(selected);
    }

    [Fact]
    public async Task Registry_SelectPracticeGymSkill_ReturnsNullForUnknownSkill()
    {
        var registry = new LinguaCoach.Infrastructure.Activity.ExerciseTypeRegistry(_db);

        var selected = await registry.SelectForPracticeGymSkillAsync("unknown");

        Assert.Null(selected);
    }

    [Fact]
    public async Task Registry_ReturnsEligibleExerciseTypesForSkillOnly()
    {
        var catalog = new ExerciseTypeCatalogService(_db);
        await catalog.UpdateAsync(new("listen_and_answer", false, null, null));
        var registry = new LinguaCoach.Infrastructure.Activity.ExerciseTypeRegistry(_db);

        var listening = await registry.GetEligibleExerciseTypesForSkillAsync(
            "Listening",
            LinguaCoach.Application.Activity.ExerciseTypeSupportContext.PracticeGym);
        var writing = await registry.GetEligibleExerciseTypesForSkillAsync("writing");

        Assert.DoesNotContain(listening, e => e.Key == "listen_and_answer");
        Assert.Contains(listening, e => e.Key == "listen_and_gap_fill");
        Assert.Contains(listening, e => e.Key == "summarize_spoken_text");
        Assert.All(writing, e => Assert.Equal("writing", e.PrimarySkill));
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
