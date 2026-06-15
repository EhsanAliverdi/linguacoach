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

        Assert.DoesNotContain(eligible, e => e.Key == "write_essay");
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
    public async Task OtherPlannedReadingTypes_RemainUnchanged()
    {
        var readyReadingKeys = new[] { "reading_multiple_choice_single", "reading_multiple_choice_multi", "reading_fill_in_blanks" };
        var planned = await _db.ExerciseTypeDefinitions
            .Where(e => e.PrimarySkill == "reading" && !readyReadingKeys.Contains(e.Key))
            .ToListAsync();

        Assert.NotEmpty(planned);
        Assert.All(planned, e => Assert.Equal("planned", e.ImplementationStatus));
        Assert.All(planned, e => Assert.False(e.IsAvailableForGeneration));
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
    public async Task Registry_ReturnsPlannedDefinitions_ButExcludesThemFromGeneration()
    {
        var registry = new LinguaCoach.Infrastructure.Activity.ExerciseTypeRegistry(_db);

        var planned = await registry.GetByKeyAsync("summarize_spoken_text");
        var eligible = await registry.GetGenerationEligibleAsync();

        Assert.NotNull(planned);
        Assert.Equal("planned", planned!.ImplementationStatus);
        Assert.DoesNotContain(eligible, e => e.Key == "summarize_spoken_text");
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
        Assert.DoesNotContain(listening, e => e.Key == "summarize_spoken_text");
        Assert.All(writing, e => Assert.Equal("writing", e.PrimarySkill));
    }
}
