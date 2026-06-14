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
