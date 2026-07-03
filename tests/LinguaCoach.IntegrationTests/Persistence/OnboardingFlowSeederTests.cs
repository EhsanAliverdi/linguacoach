using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.IntegrationTests.Persistence;

public sealed class OnboardingFlowSeederTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public OnboardingFlowSeederTests()
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

    // ── Seed creates default flow when none exists ────────────────────────────

    [Fact]
    public async Task SeedAsync_WhenNoFlowExists_CreatesActiveFlow()
    {
        await OnboardingFlowSeeder.SeedAsync(_db);

        var flow = await _db.OnboardingFlowDefinitions
            .Include(f => f.Steps)
            .FirstOrDefaultAsync(f => f.IsActive);

        Assert.NotNull(flow);
        Assert.Equal("Default Flow", flow.Name);
        Assert.True(flow.IsActive);
    }

    [Fact]
    public async Task SeedAsync_WhenNoFlowExists_CreatesAtLeastSixEnabledSteps()
    {
        await OnboardingFlowSeeder.SeedAsync(_db);

        var flow = await _db.OnboardingFlowDefinitions
            .Include(f => f.Steps)
            .FirstAsync(f => f.IsActive);

        var enabled = flow.Steps.Count(s => s.IsEnabled);
        Assert.True(enabled >= 6, $"Expected at least 6 enabled steps, got {enabled}");
    }

    [Fact]
    public async Task SeedAsync_WhenNoFlowExists_StepsHaveUniqueKeys()
    {
        await OnboardingFlowSeeder.SeedAsync(_db);

        var flow = await _db.OnboardingFlowDefinitions
            .Include(f => f.Steps)
            .FirstAsync(f => f.IsActive);

        var keys = flow.Steps.Select(s => s.StepKey).ToList();
        var distinct = keys.Distinct().ToList();
        Assert.Equal(keys.Count, distinct.Count);
    }

    [Fact]
    public async Task SeedAsync_WhenNoFlowExists_ContainsCoreStepKeys()
    {
        await OnboardingFlowSeeder.SeedAsync(_db);

        var flow = await _db.OnboardingFlowDefinitions
            .Include(f => f.Steps)
            .FirstAsync(f => f.IsActive);

        var keys = flow.Steps.Select(s => s.StepKey).ToHashSet();
        Assert.Contains("welcome", keys);
        Assert.Contains("support_language", keys);
        Assert.Contains("learning_goals", keys);
        Assert.Contains("focus_areas", keys);
    }

    // ── Seed is idempotent ────────────────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicateFlow()
    {
        await OnboardingFlowSeeder.SeedAsync(_db);
        await OnboardingFlowSeeder.SeedAsync(_db);

        var count = await _db.OnboardingFlowDefinitions.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SeedAsync_RunTwice_StillExactlyOneActiveFlow()
    {
        await OnboardingFlowSeeder.SeedAsync(_db);
        await OnboardingFlowSeeder.SeedAsync(_db);

        var activeCount = await _db.OnboardingFlowDefinitions.CountAsync(f => f.IsActive);
        Assert.Equal(1, activeCount);
    }

    // ── Seed does not override existing custom flow ───────────────────────────

    [Fact]
    public async Task SeedAsync_WhenActiveFlowExists_DoesNotModifyIt()
    {
        // Pre-seed a custom active flow.
        var customFlow = new OnboardingFlowDefinition("Custom Admin Flow", version: 42);
        customFlow.Activate();
        _db.OnboardingFlowDefinitions.Add(customFlow);
        await _db.SaveChangesAsync();

        await OnboardingFlowSeeder.SeedAsync(_db);

        var flows = await _db.OnboardingFlowDefinitions.ToListAsync();
        Assert.Single(flows);
        Assert.Equal("Custom Admin Flow", flows[0].Name);
        Assert.Equal(42, flows[0].Version);
    }

    // ── Seeded flow is usable by onboarding logic ─────────────────────────────

    [Fact]
    public async Task SeedAsync_SeededFlow_CanBeQueriedAsActiveFlow()
    {
        await OnboardingFlowSeeder.SeedAsync(_db);

        var active = await _db.OnboardingFlowDefinitions
            .Include(f => f.Steps)
            .Where(f => f.IsActive)
            .FirstOrDefaultAsync();

        Assert.NotNull(active);
        Assert.True(active.Steps.Any(s => s.IsEnabled && s.StepOrder == 1));
    }

    [Fact]
    public async Task SeedAsync_SeededFlow_StepsHaveAscendingOrders()
    {
        await OnboardingFlowSeeder.SeedAsync(_db);

        var flow = await _db.OnboardingFlowDefinitions
            .Include(f => f.Steps)
            .FirstAsync(f => f.IsActive);

        var orders = flow.Steps.OrderBy(s => s.StepOrder).Select(s => s.StepOrder).ToList();
        for (var i = 1; i < orders.Count; i++)
            Assert.True(orders[i] > orders[i - 1], "Step orders must be strictly ascending");
    }

    // ── Regression: production had an older, already-seeded flow missing the
    // Phase 20I steps (session_duration, career_context, learning_goal_description,
    // work_experience) because the pre-fix seeder bailed out entirely once any
    // active flow existed. SeedAsync must now publish a new version instead.

    [Fact]
    public async Task SeedAsync_ActiveFlowMissingNewerSteps_PublishesNewVersionWithAllSteps()
    {
        // Simulate a pre-Phase-20I production flow: only the original steps.
        var oldFlow = new OnboardingFlowDefinition("Default Flow", version: 1);
        oldFlow.Activate();
        oldFlow.AddStep(new OnboardingStepDefinition(
            oldFlow.Id, "welcome", "Welcome to SpeakPath", OnboardingStepTypeV2.Welcome,
            OnboardingStepRequirementType.SystemRequired, stepOrder: 1, isEnabled: true));
        oldFlow.AddStep(new OnboardingStepDefinition(
            oldFlow.Id, "summary", "You're all set!", OnboardingStepTypeV2.Summary,
            OnboardingStepRequirementType.SystemRequired, stepOrder: 2, isEnabled: true));
        _db.OnboardingFlowDefinitions.Add(oldFlow);
        await _db.SaveChangesAsync();

        await OnboardingFlowSeeder.SeedAsync(_db);

        var flows = await _db.OnboardingFlowDefinitions.Include(f => f.Steps).ToListAsync();
        Assert.Equal(2, flows.Count);

        var reloadedOld = flows.Single(f => f.Id == oldFlow.Id);
        Assert.False(reloadedOld.IsActive);

        var newFlow = flows.Single(f => f.Id != oldFlow.Id);
        Assert.True(newFlow.IsActive);
        Assert.Equal(2, newFlow.Version);
        Assert.Contains(newFlow.Steps, s => s.StepKey == "session_duration");
        Assert.Contains(newFlow.Steps, s => s.StepKey == "career_context");
        Assert.Contains(newFlow.Steps, s => s.StepKey == "work_experience");
        Assert.Contains(newFlow.Steps, s => s.StepKey == "learning_goal_description");
    }

    [Fact]
    public async Task SeedAsync_ActiveFlowAlreadyUpToDate_DoesNotCreateNewVersion()
    {
        await OnboardingFlowSeeder.SeedAsync(_db);
        var countAfterFirst = await _db.OnboardingFlowDefinitions.CountAsync();

        await OnboardingFlowSeeder.SeedAsync(_db);
        var countAfterSecond = await _db.OnboardingFlowDefinitions.CountAsync();

        Assert.Equal(countAfterFirst, countAfterSecond);
    }
}
