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
    public async Task LegacyAndPatternTypes_AreDisabledByDefault_DespiteBeingReady()
    {
        var service = new ExerciseTypeCatalogService(_db);
        var eligible = await service.GetGenerationEligibleAsync();

        var type = await _db.ExerciseTypeDefinitions.SingleAsync(e => e.Key == "email_reply");
        Assert.Equal("ready", type.ImplementationStatus);
        Assert.False(type.IsEnabled);
        Assert.False(type.IsAvailableForGeneration);
        Assert.DoesNotContain(eligible, e => e.Key == "email_reply");
    }

    [Theory]
    [InlineData("reading_multiple_choice_single", "reading")]
    [InlineData("reading_multiple_choice_multi", "reading")]
    [InlineData("reorder_paragraphs", "reading")]
    [InlineData("reading_writing_fill_in_blanks", "reading")]
    [InlineData("summarize_written_text", "writing")]
    [InlineData("write_essay", "writing")]
    [InlineData("listening_multiple_choice_single", "listening")]
    [InlineData("listening_multiple_choice_multi", "listening")]
    [InlineData("listening_fill_in_blanks", "listening")]
    [InlineData("select_missing_word", "listening")]
    [InlineData("highlight_correct_summary", "listening")]
    [InlineData("highlight_incorrect_words", "listening")]
    [InlineData("write_from_dictation", "listening")]
    [InlineData("summarize_spoken_text", "listening")]
    [InlineData("answer_short_question", "speaking")]
    [InlineData("read_aloud", "speaking")]
    [InlineData("repeat_sentence", "speaking")]
    [InlineData("respond_to_situation", "speaking")]
    [InlineData("describe_image", "speaking")]
    [InlineData("retell_lecture", "listening")]
    [InlineData("summarize_group_discussion", "listening")]
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
        // Phase K15 — these legacy/pattern types resolve fine but are disabled by default
        // (no Lesson-generation composer yet); IsAvailableForGeneration is deliberately not
        // asserted true here anymore.
    }

    [Fact]
    public async Task Registry_SummarizeGroupDiscussion_IsReadyButNotEligibleByDefault()
    {
        var registry = new LinguaCoach.Infrastructure.Activity.ExerciseTypeRegistry(_db);

        var def = await registry.GetByKeyAsync("summarize_group_discussion");
        var eligible = await registry.GetGenerationEligibleAsync();

        Assert.NotNull(def);
        Assert.Equal("ready", def!.ImplementationStatus);
        Assert.DoesNotContain(eligible, e => e.Key == "summarize_group_discussion");
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
