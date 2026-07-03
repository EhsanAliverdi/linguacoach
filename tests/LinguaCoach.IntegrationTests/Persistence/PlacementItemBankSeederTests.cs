using LinguaCoach.Domain.Questions;
using LinguaCoach.Infrastructure.Placement;
using LinguaCoach.Infrastructure.Questions;
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

    // ── Phase 20I-5: listening audio script derivation ──────────────────────

    [Fact]
    public async Task SeedAsync_DerivesListeningAudioScriptFromQuotedPromptText()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var item = await _db.PlacementItemDefinitions.FirstAsync(
            i => i.Prompt == "You hear: 'Turn left at the traffic lights.' Where do you turn? (A) right (B) left (C) straight");

        Assert.Equal("Turn left at the traffic lights.", item.ListeningAudioScript);
    }

    [Fact]
    public async Task SeedAsync_SubstitutesGapFillBlankWithCorrectAnswerInAudioScript()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var item = await _db.PlacementItemDefinitions.FirstAsync(
            i => i.Prompt == "You hear: 'My name is ___.' (Maria/Monday/Morning)");

        Assert.Equal("My name is Maria.", item.ListeningAudioScript);
    }

    [Fact]
    public async Task SeedAsync_LeavesAudioScriptNullWhenPromptHasNoQuotedLine()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var item = await _db.PlacementItemDefinitions.FirstAsync(
            i => i.Prompt == "You hear a complaint about slow service. What is the caller's main concern? (A) price (B) quality (C) speed");

        Assert.Null(item.ListeningAudioScript);
    }

    [Fact]
    public async Task SeedAsync_NonListeningItemsHaveNoAudioScript()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var grammarItems = await _db.PlacementItemDefinitions.Where(i => i.Skill == "grammar").ToListAsync();
        Assert.All(grammarItems, i => Assert.Null(i.ListeningAudioScript));
    }

    [Fact]
    public async Task SeedAsync_BackfillsAudioScriptOntoRowsSeededBeforeThisFieldExisted()
    {
        // Simulates the real production state after the Phase 20I-4 deploy: rows already
        // exist with ListeningAudioScript null, since that deploy predates this field.
        await PlacementItemBankSeeder.SeedAsync(_db);
        var item = await _db.PlacementItemDefinitions.FirstAsync(
            i => i.Prompt == "You hear: 'Turn left at the traffic lights.' Where do you turn? (A) right (B) left (C) straight");
        item.Update(item.Skill, item.CefrLevel, item.ItemType, item.Prompt, item.CorrectAnswer,
            item.ItemOrder, item.IsEnabled, item.ReadingPassage, listeningAudioScript: null);
        await _db.SaveChangesAsync();

        await PlacementItemBankSeeder.SeedAsync(_db);

        var reloaded = await _db.PlacementItemDefinitions.FirstAsync(i => i.Id == item.Id);
        Assert.Equal("Turn left at the traffic lights.", reloaded.ListeningAudioScript);
    }

    // ── Unified Question-Schema Phase 2: ContentJson shadow ─────────────────

    [Fact]
    public async Task SeedAsync_PopulatesContentJsonForEveryItem()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var items = await _db.PlacementItemDefinitions.ToListAsync();
        Assert.All(items, i => Assert.NotNull(i.ContentJson));
        Assert.All(items, i => Assert.NotNull(i.Content));
    }

    [Fact]
    public async Task SeedAsync_ListeningItem_ProducesListeningGroupContent()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var item = await _db.PlacementItemDefinitions.FirstAsync(
            i => i.Prompt == "You hear: 'Turn left at the traffic lights.' Where do you turn? (A) right (B) left (C) straight");

        var group = Assert.IsType<ListeningGroupQuestion>(item.Content);
        Assert.Equal("Turn left at the traffic lights.", group.AudioScript);
        var leaf = Assert.IsType<SingleChoiceQuestion>(Assert.Single(group.Questions));
        Assert.Equal("B", leaf.CorrectAnswerKey);
    }

    [Fact]
    public async Task SeedAsync_GapFillItem_ProducesGapFillContentWithSameCorrectAnswer()
    {
        await PlacementItemBankSeeder.SeedAsync(_db);

        var item = await _db.PlacementItemDefinitions.FirstAsync(
            i => i.Skill == "grammar" && i.ItemType == "gap_fill");

        var leaf = Assert.IsType<GapFillQuestion>(item.Content);
        Assert.Equal(item.CorrectAnswer, leaf.CorrectAnswer);
    }

    [Fact]
    public async Task Content_ScoredWithSharedScorer_MatchesLegacyPlacementScoringService()
    {
        // Proves the new shared IQuestionScorer produces identical correctness to the legacy
        // PlacementScoringService for every seeded item, de-risking the future cutover where
        // PlacementAssessmentService switches from flat-field scoring to Content-based scoring.
        await PlacementItemBankSeeder.SeedAsync(_db);
        var legacyScorer = new PlacementScoringService();
        var sharedScorer = new QuestionScorer();

        var items = await _db.PlacementItemDefinitions.ToListAsync();
        foreach (var item in items)
        {
            var legacyResult = legacyScorer.Score(item.CorrectAnswer, item.CorrectAnswer, item.ItemType);

            var answer = new QuestionAnswer([new QuestionAnswerItem("q1", [item.CorrectAnswer])]);
            var sharedResult = sharedScorer.Score(item.Content!, answer);

            Assert.True(legacyResult.IsCorrect, $"Legacy scorer disagreed with itself for item {item.Id}.");
            Assert.True(sharedResult.IsCorrect, $"Shared scorer marked correct answer wrong for item {item.Id} ({item.Prompt}).");
        }
    }
}
