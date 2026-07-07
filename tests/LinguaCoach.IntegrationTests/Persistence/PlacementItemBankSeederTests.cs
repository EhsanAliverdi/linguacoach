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
        item.Update(item.Skill, "B2", item.ItemOrder, item.IsEnabled);
        await _db.SaveChangesAsync();

        await PlacementItemBankSeeder.SeedAsync(_db);

        var reloaded = await _db.PlacementItemDefinitions.FirstAsync(i => i.Id == item.Id);
        Assert.Equal("B2", reloaded.CefrLevel);
        Assert.Equal(72, await _db.PlacementItemDefinitions.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_BackfillsNullSchemaOnExistingRow_WithoutDuplicating()
    {
        // Simulates rows that exist in the DB from before the Form.io-native migration ran —
        // present, but FormIoSchemaJson/ScoringRulesJson still null. A prior regression made the
        // seeder's idempotency check skip these rows entirely once their (skill, level) pair
        // existed, leaving them permanently blank.
        await PlacementItemBankSeeder.SeedAsync(_db);

        var grammarA1Items = await _db.PlacementItemDefinitions
            .Where(i => i.Skill == "grammar" && i.CefrLevel == "A1")
            .ToListAsync();
        foreach (var item in grammarA1Items)
        {
            _db.Entry(item).Property("FormIoSchemaJson").CurrentValue = null;
            _db.Entry(item).Property("ScoringRulesJson").CurrentValue = null;
        }
        await _db.SaveChangesAsync();

        await PlacementItemBankSeeder.SeedAsync(_db);

        var reloaded = await _db.PlacementItemDefinitions
            .Where(i => i.Skill == "grammar" && i.CefrLevel == "A1")
            .ToListAsync();
        Assert.Equal(3, reloaded.Count);
        Assert.All(reloaded, i => Assert.False(string.IsNullOrWhiteSpace(i.FormIoSchemaJson)));
        Assert.All(reloaded, i => Assert.False(string.IsNullOrWhiteSpace(i.ScoringRulesJson)));
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
    public async Task SeedAsync_EveryItemHasAuthoringSchemaWithQuizAnnotation()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var items = await _db.PlacementItemDefinitions.ToListAsync();
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.AuthoringSchemaJson)));
        Assert.All(items, i =>
        {
            using var doc = JsonDocument.Parse(i.AuthoringSchemaJson!);
            // Reading items carry a leading read-only "content" passage component before the
            // scored "answer" component — find by key rather than assuming index 0.
            var answerComponent = doc.RootElement.GetProperty("components")
                .EnumerateArray()
                .First(c => c.GetProperty("key").GetString() == "answer");
            var quiz = answerComponent.GetProperty("quiz");
            Assert.True(quiz.GetProperty("enabled").GetBoolean());
        });
    }

    [Fact]
    public async Task SeedAsync_BackfillsMissingAuthoringSchemaOnExistingRow_WithoutTouchingScoringData()
    {
        // Simulates rows seeded before the Quiz tab existed: FormIoSchemaJson/ScoringRulesJson
        // present, AuthoringSchemaJson still null.
        await PlacementItemBankSeeder.SeedAsync(_db);

        var item = await _db.PlacementItemDefinitions.FirstAsync(i => i.Skill == "grammar" && i.CefrLevel == "A1");
        var originalFormIoSchema = item.FormIoSchemaJson;
        var originalScoringRules = item.ScoringRulesJson;
        _db.Entry(item).Property("AuthoringSchemaJson").CurrentValue = null;
        await _db.SaveChangesAsync();

        await PlacementItemBankSeeder.SeedAsync(_db);

        var reloaded = await _db.PlacementItemDefinitions.FirstAsync(i => i.Id == item.Id);
        Assert.False(string.IsNullOrWhiteSpace(reloaded.AuthoringSchemaJson));
        Assert.Equal(originalFormIoSchema, reloaded.FormIoSchemaJson);
        Assert.Equal(originalScoringRules, reloaded.ScoringRulesJson);

        using var doc = JsonDocument.Parse(reloaded.AuthoringSchemaJson!);
        Assert.True(doc.RootElement.GetProperty("components")[0].GetProperty("quiz").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task SeedAsync_NeverOverwritesAlreadyPresentAuthoringSchema()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var item = await _db.PlacementItemDefinitions.FirstAsync(i => i.Skill == "grammar" && i.CefrLevel == "A1");
        item.SetAuthoringSchema("""{"components":[{"type":"radio","key":"answer","admin":"customized"}]}""");
        await _db.SaveChangesAsync();

        await PlacementItemBankSeeder.SeedAsync(_db);

        var reloaded = await _db.PlacementItemDefinitions.FirstAsync(i => i.Id == item.Id);
        Assert.Contains("customized", reloaded.AuthoringSchemaJson);
    }

    [Fact]
    public async Task SeedAsync_MultipleChoiceItem_ProducesRadioComponentWithChoices()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var grammarItems = await _db.PlacementItemDefinitions.Where(i => i.Skill == "grammar").ToListAsync();
        var item = grammarItems.First(i =>
            JsonDocument.Parse(i.FormIoSchemaJson!).RootElement.GetProperty("components")[0]
                .GetProperty("type").GetString() == "radio");

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

        var grammarItems = await _db.PlacementItemDefinitions.Where(i => i.Skill == "grammar").ToListAsync();
        var item = grammarItems.First(i =>
            JsonDocument.Parse(i.FormIoSchemaJson!).RootElement.GetProperty("components")[0]
                .GetProperty("type").GetString() == "textfield");

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

        var listeningItems = await _db.PlacementItemDefinitions.Where(i => i.Skill == "listening").ToListAsync();
        var item = listeningItems.First(i =>
            JsonDocument.Parse(i.ScoringRulesJson!).RootElement.TryGetProperty("listeningAudioScript", out var script)
            && script.GetString() == "Turn left at the traffic lights.");

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
    public async Task SeedAsync_SpeakingItems_UseSpeakingResponseComponentAndSpeakingKind()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var speakingItems = await _db.PlacementItemDefinitions.Where(i => i.Skill == "speaking").ToListAsync();
        Assert.NotEmpty(speakingItems);

        foreach (var item in speakingItems)
        {
            using var schemaDoc = JsonDocument.Parse(item.FormIoSchemaJson!);
            var component = schemaDoc.RootElement.GetProperty("components")[0];
            Assert.Equal("speakingResponse", component.GetProperty("type").GetString());
            Assert.Equal("answer", component.GetProperty("key").GetString());

            using var rulesDoc = JsonDocument.Parse(item.ScoringRulesJson!);
            var rule = rulesDoc.RootElement.GetProperty("components").GetProperty("answer");
            Assert.Equal("speaking", rule.GetProperty("kind").GetString());
            Assert.True(rule.GetProperty("requiresManualOrAiEvaluation").GetBoolean());
            Assert.False(rule.TryGetProperty("correctAnswer", out _));
        }
    }

    [Fact]
    public async Task Content_ScoredWithSharedScorer_MatchesCorrectAnswerForEverySeededItem()
    {
        // Proves the new scoring service correctly scores every seeded item's own correct answer —
        // de-risking the seed conversion from legacy flat CorrectAnswer to native scoring rules.
        await PlacementItemBankSeeder.SeedAsync(_db);
        var scorer = new PlacementScoringService();

        // "speaking" items have no authored correctAnswer — they're scored by
        // IPlacementSpeakingScorer/ISpeakingEvaluationProvider against an uploaded recording, not
        // by deterministic comparison, so they're out of scope for this test.
        var items = await _db.PlacementItemDefinitions.Where(i => i.Skill != "speaking").ToListAsync();
        foreach (var item in items)
        {
            using var rulesDoc = JsonDocument.Parse(item.ScoringRulesJson!);
            var correctAnswer = rulesDoc.RootElement.GetProperty("components").GetProperty("answer").GetProperty("correctAnswer").GetString()!;

            var submissionJson = JsonSerializer.Serialize(new { answer = correctAnswer });
            var submission = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(submissionJson)!;

            var result = scorer.ScoreSubmission(item.ScoringRulesJson, submission);

            Assert.True(result.IsCorrect, $"Shared scorer marked correct answer wrong for item {item.Id}.");
        }
    }
}
