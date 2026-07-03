using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.IntegrationTests.Persistence;

public sealed class PlacementItemBankSeederTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public PlacementItemBankSeederTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task SeedAsync_WhenEmpty_Backfills72Items()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var count = await _db.PlacementItemDefinitions.CountAsync();
        Assert.Equal(72, count);
    }

    [Fact]
    public async Task SeedAsync_CoversSixSkillsAndFourCefrLevels()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var skills = await _db.PlacementItemDefinitions.Select(i => i.Skill).Distinct().ToListAsync();
        var levels = await _db.PlacementItemDefinitions.Select(i => i.CefrLevel).Distinct().ToListAsync();

        Assert.Equal(6, skills.Count);
        Assert.Equal(4, levels.Count);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_DoesNotDuplicateOnRerun()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);
        await PlacementItemBankSeeder.SeedAsync(_db);

        var count = await _db.PlacementItemDefinitions.CountAsync();
        Assert.Equal(72, count);
    }

    [Fact]
    public async Task SeedAsync_DoesNotOverwriteAdminEditedItem()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var item = await _db.PlacementItemDefinitions.FirstAsync(i => i.Skill == "grammar");
        item.Update(item.Skill, item.CefrLevel, item.ItemType, item.Prompt, "AdminEditedAnswer",
            item.ItemOrder, item.IsEnabled, item.ReadingPassage, item.ListeningAudioScript);
        await _db.SaveChangesAsync();

        await PlacementItemBankSeeder.SeedAsync(_db);

        var reloaded = await _db.PlacementItemDefinitions.FirstAsync(i => i.Id == item.Id);
        Assert.Equal("AdminEditedAnswer", reloaded.CorrectAnswer);
        Assert.Equal(72, await _db.PlacementItemDefinitions.CountAsync());
    }
}
