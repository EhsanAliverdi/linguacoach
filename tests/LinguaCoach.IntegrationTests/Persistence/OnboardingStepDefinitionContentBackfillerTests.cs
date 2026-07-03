using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.IntegrationTests.Persistence;

/// <summary>Unified Question-Schema Phase 5 — backfilling ContentJson onto historical
/// OnboardingStepDefinition rows created before this field existed (the live active flow's
/// existing steps, which the seeder's "flows are immutable" rule never touches).</summary>
public sealed class OnboardingStepDefinitionContentBackfillerTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public OnboardingStepDefinitionContentBackfillerTests()
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
    public async Task BackfillAsync_PopulatesContentForGenericStepType()
    {
        var flow = new OnboardingFlowDefinition("Legacy Flow", version: 1);
        flow.Activate();
        _db.OnboardingFlowDefinitions.Add(flow);

        var step = new OnboardingStepDefinition(
            flow.Id, "quick_check", "Quick check", OnboardingStepTypeV2.SingleChoice,
            OnboardingStepRequirementType.SystemRequired, 1, true,
            optionsJson: "[{\"key\":\"a\",\"label\":\"Option A\"},{\"key\":\"b\",\"label\":\"Option B\"}]");
        _db.OnboardingStepDefinitions.Add(step);
        await _db.SaveChangesAsync();

        await OnboardingStepDefinitionContentBackfiller.BackfillAsync(_db);

        var reloaded = await _db.OnboardingStepDefinitions.FirstAsync(s => s.Id == step.Id);
        var content = Assert.IsType<Domain.Questions.SingleChoiceQuestion>(reloaded.Content);
        Assert.Equal(2, content.Choices.Count);
    }

    [Fact]
    public async Task BackfillAsync_LeavesSemanticStepTypeContentNull()
    {
        var flow = new OnboardingFlowDefinition("Legacy Flow", version: 1);
        flow.Activate();
        _db.OnboardingFlowDefinitions.Add(flow);

        var step = new OnboardingStepDefinition(
            flow.Id, "support_language", "Support language", OnboardingStepTypeV2.SupportLanguage,
            OnboardingStepRequirementType.SystemRequired, 1, true,
            optionsJson: "[{\"key\":\"fa\",\"label\":\"Farsi\"}]");
        _db.OnboardingStepDefinitions.Add(step);
        await _db.SaveChangesAsync();

        await OnboardingStepDefinitionContentBackfiller.BackfillAsync(_db);

        var reloaded = await _db.OnboardingStepDefinitions.FirstAsync(s => s.Id == step.Id);
        Assert.Null(reloaded.Content);
    }

    [Fact]
    public async Task BackfillAsync_IsIdempotent()
    {
        var flow = new OnboardingFlowDefinition("Legacy Flow", version: 1);
        flow.Activate();
        _db.OnboardingFlowDefinitions.Add(flow);

        var step = new OnboardingStepDefinition(
            flow.Id, "quick_check", "Quick check", OnboardingStepTypeV2.SingleChoice,
            OnboardingStepRequirementType.SystemRequired, 1, true,
            optionsJson: "[{\"key\":\"a\",\"label\":\"Option A\"}]");
        _db.OnboardingStepDefinitions.Add(step);
        await _db.SaveChangesAsync();

        await OnboardingStepDefinitionContentBackfiller.BackfillAsync(_db);
        await OnboardingStepDefinitionContentBackfiller.BackfillAsync(_db);

        var reloaded = await _db.OnboardingStepDefinitions.FirstAsync(s => s.Id == step.Id);
        Assert.NotNull(reloaded.Content);
    }
}
