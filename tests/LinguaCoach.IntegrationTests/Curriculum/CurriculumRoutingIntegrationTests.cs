using LinguaCoach.Application.Curriculum;
using LinguaCoach.Application.Learning;
using LinguaCoach.Domain.Constants;
using LinguaCoach.IntegrationTests.Api;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Curriculum;

/// <summary>
/// Integration tests for Phase 10L CEFR-aware curriculum routing.
/// Verifies that CurriculumRoutingService works against real seeded objectives in the test DB.
/// </summary>
public sealed class CurriculumRoutingIntegrationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public CurriculumRoutingIntegrationTests(ApiTestFactory factory)
        => _factory = factory;

    // ── Service is registered and resolves ───────────────────────────────────

    [Fact]
    public void RoutingService_IsRegisteredInDI()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICurriculumRoutingService>();
        Assert.NotNull(svc);
    }

    // ── CEFR normalization via DI service ────────────────────────────────────

    [Theory]
    [InlineData("B2+", "B2")]
    [InlineData("A1+", "A1")]
    [InlineData("C1-", "C1")]
    public void RoutingService_NormalizeCefrLevel_StripsPlus(string input, string expected)
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICurriculumRoutingService>();
        Assert.Equal(expected, svc.NormalizeCefrLevel(input));
    }

    // ── Seeded objectives are queryable via routing ───────────────────────────

    [Fact]
    public async Task RoutingService_SeededObjectivesPresent_ReturnsNormalMatch()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICurriculumRoutingService>();

        var req = new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = CefrLevelConstants.B1,
            Source = "integration_test",
            ResolvedLearningGoalContext = new ResolvedLearningGoalContext
            {
                WorkplaceSpecific = false,
                ContextSummary = "general English communication",
                Source = "Fallback"
            },
            AllowReviewOrScaffold = false
        };

        var rec = await svc.RecommendAsync(req);

        // The seeder creates objectives for B1 — we should get a match or at least a safe fallback.
        Assert.NotNull(rec);
        Assert.False(string.IsNullOrWhiteSpace(rec.TargetCefrLevel));
        Assert.Equal(CefrLevelConstants.B1, rec.TargetCefrLevel);
    }

    // ── B2 student does not receive B1 content silently ─────────────────────

    [Fact]
    public async Task RoutingService_B2Student_NeverSilentlyGetB1Content()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICurriculumRoutingService>();

        var req = new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = CefrLevelConstants.B2,
            Source = "integration_test",
            ResolvedLearningGoalContext = new ResolvedLearningGoalContext
            {
                WorkplaceSpecific = false,
                ContextSummary = "general English communication",
                Source = "Fallback"
            },
            AllowReviewOrScaffold = false
        };

        var rec = await svc.RecommendAsync(req);

        // When AllowReviewOrScaffold=false, TargetCefrLevel must never be below B2.
        // Either it matches B2 (Normal) or it falls back (Fallback at B2).
        Assert.NotEqual(RoutingReason.Review, rec.RoutingReason);
        Assert.NotEqual(RoutingReason.Scaffold, rec.RoutingReason);
        Assert.NotEqual(RoutingReason.Remediation, rec.RoutingReason);

        if (rec.RoutingReason == RoutingReason.Normal)
            Assert.Equal(CefrLevelConstants.B2, rec.TargetCefrLevel);
        else
            Assert.Equal(CefrLevelConstants.B2, rec.TargetCefrLevel); // Fallback also stays at B2
    }

    // ── Non-workplace student never gets workplace context ────────────────────

    [Fact]
    public async Task RoutingService_DayToDayEnglish_ContextTagsDoNotContainWorkplace()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICurriculumRoutingService>();

        var req = new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = CefrLevelConstants.B1,
            Source = "integration_test",
            LearningGoals = ["day_to_day_english", "social_conversation"],
            ResolvedLearningGoalContext = new ResolvedLearningGoalContext
            {
                WorkplaceSpecific = false,
                PrimaryGoalKey = "day_to_day_english",
                ContextSummary = "day to day English",
                Source = "Structured"
            },
            AllowReviewOrScaffold = false
        };

        var rec = await svc.RecommendAsync(req);

        Assert.DoesNotContain(CurriculumContextTagConstants.Workplace, rec.ContextTags);
    }

    // ── Workplace goal routes to workplace context ────────────────────────────

    [Fact]
    public async Task RoutingService_WorkplaceGoal_ContextTagsContainWorkplace()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICurriculumRoutingService>();

        var req = new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = CefrLevelConstants.B1,
            Source = "integration_test",
            LearningGoals = ["workplace_english"],
            ResolvedLearningGoalContext = new ResolvedLearningGoalContext
            {
                WorkplaceSpecific = true,
                PrimaryGoalKey = "workplace_english",
                ContextSummary = "workplace English",
                Source = "Structured"
            },
            AllowReviewOrScaffold = false
        };

        var rec = await svc.RecommendAsync(req);

        Assert.Contains(CurriculumContextTagConstants.Workplace, rec.ContextTags);
    }

    // ── CurriculumSyllabusQuery is seeded (regression) ────────────────────────

    [Fact]
    public async Task SeededObjectives_AreQueryableViaICurriculumSyllabusQuery()
    {
        using var scope = _factory.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<ICurriculumSyllabusQuery>();

        var objectives = await query.GetActiveObjectivesAsync();
        Assert.NotEmpty(objectives);
    }

    // ── Routing recommendation is stable (idempotent for same input) ──────────

    [Fact]
    public async Task RoutingService_SameInput_ReturnsSameLevel()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICurriculumRoutingService>();

        var req = new CurriculumRoutingRequest
        {
            StudentId = Guid.NewGuid(),
            CurrentCefrLevel = CefrLevelConstants.A2,
            Source = "integration_test",
            ResolvedLearningGoalContext = new ResolvedLearningGoalContext
            {
                WorkplaceSpecific = false,
                ContextSummary = "general English",
                Source = "Fallback"
            },
            AllowReviewOrScaffold = false
        };

        var rec1 = await svc.RecommendAsync(req);
        var rec2 = await svc.RecommendAsync(req);

        Assert.Equal(rec1.TargetCefrLevel, rec2.TargetCefrLevel);
        Assert.Equal(rec1.RoutingReason, rec2.RoutingReason);
    }
}
