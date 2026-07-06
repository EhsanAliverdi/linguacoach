using System.Text.Json;
using LinguaCoach.Infrastructure.Placement;
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
        item.Update(item.Skill, "B2", item.ItemType, item.Prompt, item.ItemOrder, item.IsEnabled);
        await _db.SaveChangesAsync();

        await PlacementItemBankSeeder.SeedAsync(_db);

        var reloaded = await _db.PlacementItemDefinitions.FirstAsync(i => i.Id == item.Id);
        Assert.Equal("B2", reloaded.CefrLevel);
        Assert.Equal(72, await _db.PlacementItemDefinitions.CountAsync());
    }

    // ── Form.io-native authoring ──────────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_EveryItemHasFormIoSchemaAndScoringRules()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var items = await _db.PlacementItemDefinitions.ToListAsync();
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.FormIoSchemaJson)));
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.ScoringRulesJson)));
        Assert.All(items, i => Assert.True(i.ScoringRulesVersion >= 1));
    }

    [Fact]
    public async Task SeedAsync_MultipleChoiceItem_ProducesRadioComponentWithChoices()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var item = await _db.PlacementItemDefinitions.FirstAsync(
            i => i.Skill == "grammar" && i.ItemType == "multiple_choice");

        using var doc = JsonDocument.Parse(item.FormIoSchemaJson!);
        var component = doc.RootElement.GetProperty("components")[0];
        Assert.Equal("radio", component.GetProperty("type").GetString());
        Assert.Equal("answer", component.GetProperty("key").GetString());
        Assert.True(component.GetProperty("values").GetArrayLength() >= 2);
    }

    [Fact]
    public async Task SeedAsync_GapFillItem_ProducesTextfieldWithTextNormalizedScoring()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var item = await _db.PlacementItemDefinitions.FirstAsync(
            i => i.Skill == "grammar" && i.ItemType == "gap_fill");

        using var schemaDoc = JsonDocument.Parse(item.FormIoSchemaJson!);
        var component = schemaDoc.RootElement.GetProperty("components")[0];
        Assert.Equal("textfield", component.GetProperty("type").GetString());

        using var rulesDoc = JsonDocument.Parse(item.ScoringRulesJson!);
        var rule = rulesDoc.RootElement.GetProperty("components").GetProperty("answer");
        Assert.Equal("text_normalized", rule.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task SeedAsync_ListeningItem_EmbedsAudioScriptInScoringRulesOnly()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var item = await _db.PlacementItemDefinitions.FirstAsync(
            i => i.Prompt == "You hear: 'Turn left at the traffic lights.' Where do you turn? (A) right (B) left (C) straight");

        using var rulesDoc = JsonDocument.Parse(item.ScoringRulesJson!);
        Assert.Equal("Turn left at the traffic lights.",
            rulesDoc.RootElement.GetProperty("listeningAudioScript").GetString());

        // The script must never appear in the student-safe Form.io schema.
        Assert.DoesNotContain("listeningAudioScript", item.FormIoSchemaJson);
    }

    [Fact]
    public async Task SeedAsync_NonListeningItems_HaveNoAudioScript()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var grammarItems = await _db.PlacementItemDefinitions.Where(i => i.Skill == "grammar").ToListAsync();
        foreach (var item in grammarItems)
        {
            using var rulesDoc = JsonDocument.Parse(item.ScoringRulesJson!);
            var scriptEl = rulesDoc.RootElement.GetProperty("listeningAudioScript");
            Assert.Equal(JsonValueKind.Null, scriptEl.ValueKind);
        }
    }

    [Fact]
    public async Task Content_ScoredWithSharedScorer_MatchesCorrectAnswerForEverySeededItem()
    {
        // Proves the new scoring service correctly scores every seeded item's own correct answer —
        // de-risking the seed conversion from legacy flat CorrectAnswer to native scoring rules.
        await PlacementItemBankSeeder.SeedAsync(_db);
        var scorer = new PlacementScoringService();

        var items = await _db.PlacementItemDefinitions.ToListAsync();
        foreach (var item in items)
        {
            using var rulesDoc = JsonDocument.Parse(item.ScoringRulesJson!);
            var correctAnswer = rulesDoc.RootElement.GetProperty("components").GetProperty("answer").GetProperty("correctAnswer").GetString()!;

            var submissionJson = JsonSerializer.Serialize(new { answer = correctAnswer });
            var submission = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(submissionJson)!;

            var result = scorer.ScoreSubmission(item.ScoringRulesJson, submission);

            Assert.True(result.IsCorrect, $"Shared scorer marked correct answer wrong for item {item.Id} ({item.Prompt}).");
        }
    }
}
