using LinguaCoach.Application.LearningPlan;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.LearningPlan;

/// <summary>
/// Integration tests for Phase 12D — Learning Plan Orchestrator Foundation.
/// Verifies DI registration and service resolution only.
/// Full plan generation requires a student profile with CEFR level and curriculum seed data.
/// </summary>
public sealed class LearningPlanIntegrationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public LearningPlanIntegrationTests(ApiTestFactory factory)
        => _factory = factory;

    // 1. ILearningPlanService is registered and resolves.
    [Fact]
    public void LearningPlanService_IsRegisteredInDI()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<ILearningPlanService>();
        Assert.NotNull(svc);
    }

    // 2. LearningPlanOptions is registered and resolves with safe defaults.
    [Fact]
    public void LearningPlanOptions_IsRegisteredInDI()
    {
        using var scope = _factory.Services.CreateScope();
        var opts = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<LearningPlanOptions>>();
        Assert.True(opts.Value.PlannedLessonCount > 0);
        Assert.InRange(opts.Value.MasteryCompletionThreshold, 1, 100);
    }

    // 3. DbSets for StudentLearningPlans and StudentLearningPlanObjectives are accessible.
    [Fact]
    public void DbContext_HasLearningPlanDbSets()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // Should not throw — tables must exist after migration T61.
        var planCount = db.StudentLearningPlans.Count();
        var objCount  = db.StudentLearningPlanObjectives.Count();

        Assert.True(planCount >= 0);
        Assert.True(objCount >= 0);
    }

    // 4. GetOrCreatePlanAsync throws for an unknown student.
    [Fact]
    public async Task GetOrCreatePlan_UnknownStudent_ThrowsInvalidOperation()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ILearningPlanService>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.GetOrCreatePlanAsync(Guid.NewGuid()));
    }

    // 5. GetProgressAsync returns zero-state summary (not throws) for unknown student.
    [Fact]
    public async Task GetProgress_UnknownStudent_ReturnsZeroSummary()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ILearningPlanService>();

        var result = await svc.GetProgressAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal(0, result.ObjectivesCompleted);
        Assert.Equal(0, result.ObjectivesRemaining);
    }

    // 6. GetNextPlannedObjectiveAsync returns null (not throws) for unknown student.
    [Fact]
    public async Task GetNextPlannedObjective_UnknownStudent_ReturnsNull()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ILearningPlanService>();

        var result = await svc.GetNextPlannedObjectiveAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // 7. GetPracticeGymObjectivesAsync returns empty list (not throws) for unknown student.
    [Fact]
    public async Task GetPracticeGymObjectives_UnknownStudent_ReturnsEmpty()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ILearningPlanService>();

        var result = await svc.GetPracticeGymObjectivesAsync(Guid.NewGuid(), maxCount: 5);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // 8. Migration created the learning plan tables (verifiable via EF query).
    [Fact]
    public void Migration_CreatedLearningPlansTables()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        // If the table doesn't exist EF throws during query.
        var ex1 = Record.Exception(() => db.StudentLearningPlans.Any());
        Assert.Null(ex1);

        var ex2 = Record.Exception(() => db.StudentLearningPlanObjectives.Any());
        Assert.Null(ex2);
    }
}
