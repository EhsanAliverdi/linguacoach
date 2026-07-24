using FluentAssertions;
using LinguaCoach.Application.Learning;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.LearningPlan;
using LinguaCoach.Infrastructure.SkillGraph;
using LinguaCoach.Persistence;
using LinguaCoach.UnitTests.TodayPlanModules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinguaCoach.UnitTests.LearningPlan;

/// <summary>
/// Skill Graph pipeline audit (2026-07-24, Bug #5) — <see cref="StudentLearningPlanObjective.IsBlocked"/>
/// was permanently inert (every construction site hardcoded false/null). These tests verify
/// <see cref="LearningPlanService"/> now computes a real value: blocked when an objective's
/// skill-graph node has a direct <c>SkillGraphPrerequisiteEdge</c> prerequisite the student is
/// currently AtRisk on — same threshold/bootstrap-safety reasoning as the Today/Gym composer's
/// <c>HasUnmetPrerequisite</c> signal (see <c>TodayPlanModuleSelectionServiceTests.cs</c>).
///
/// Uses a real <see cref="SkillGraphRoutingService"/> (small, deterministic, DB-only — no reason
/// to fake it) and the shared <see cref="FakeStudentMasteryEvaluationService"/> test double
/// (internal, same assembly, reused rather than duplicated).
/// </summary>
public sealed class LearningPlanBlockingTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly FakeStudentMasteryEvaluationService _mastery;
    private readonly LearningPlanService _sut;
    private readonly StudentProfile _profile;

    public LearningPlanBlockingTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _mastery = new FakeStudentMasteryEvaluationService();
        var routing = new SkillGraphRoutingService(_db, NullLogger<SkillGraphRoutingService>.Instance);
        var goalContextResolver = new FakeLearningGoalContextResolver();
        var planOptions = Options.Create(new LearningPlanOptions());

        _sut = new LearningPlanService(
            _db, routing, _mastery, goalContextResolver, planOptions, NullLogger<LearningPlanService>.Instance);

        _profile = new StudentProfile(Guid.NewGuid());
        _profile.SetCefrLevel("A1");
        _db.StudentProfiles.Add(_profile);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Database.CloseConnection();

    /// <summary>Seeds a prerequisite node + a dependent node linked by a
    /// <see cref="SkillGraphPrerequisiteEdge"/>, both A1/Speaking — the rotation's first slot, so
    /// the dependent node is deterministically the plan's first objective (mirrors
    /// <c>StudentLearningPlanJourneyTests.GetJourney_ResolvesActivePlan_ByUserIdNotProfileId</c>'s
    /// proven single-node seeding pattern).</summary>
    private (SkillGraphNode Prerequisite, SkillGraphNode Dependent) SeedPrerequisiteChain(string suffix)
    {
        var prerequisite = new SkillGraphNode(
            $"a1.speaking.prereq_{suffix}", "Prereq", "desc", CefrLevelConstants.A1, CurriculumSkillConstants.Speaking);
        prerequisite.Approve(null);
        var dependent = new SkillGraphNode(
            $"a1.speaking.dependent_{suffix}", "Dependent", "desc", CefrLevelConstants.A1, CurriculumSkillConstants.Speaking);
        dependent.Approve(null);
        _db.SkillGraphNodes.AddRange(prerequisite, dependent);
        _db.SaveChanges();
        _db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(dependent.Id, prerequisite.Id));
        _db.SaveChanges();
        return (prerequisite, dependent);
    }

    [Fact]
    public async Task Objective_with_atrisk_prerequisite_is_blocked()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var (prerequisite, dependent) = SeedPrerequisiteChain(suffix);
        _mastery.AtRiskObjectiveKeys = [prerequisite.Key];

        await _sut.RegeneratePlanAsync(_profile.Id, "test");

        var obj = _db.StudentLearningPlanObjectives.Single(o => o.ObjectiveKey == dependent.Key);
        obj.IsBlocked.Should().BeTrue();
        obj.BlockedByObjectiveKey.Should().Be(prerequisite.Key);
    }

    [Fact]
    public async Task Objective_with_never_attempted_prerequisite_is_not_blocked()
    {
        // Bootstrap safety: a never-attempted prerequisite must never block, or nothing could ever
        // be shown to a brand-new student (every prerequisite starts unattempted).
        var suffix = Guid.NewGuid().ToString("N");
        var (_, dependent) = SeedPrerequisiteChain(suffix);

        await _sut.RegeneratePlanAsync(_profile.Id, "test");

        var obj = _db.StudentLearningPlanObjectives.Single(o => o.ObjectiveKey == dependent.Key);
        obj.IsBlocked.Should().BeFalse();
        obj.BlockedByObjectiveKey.Should().BeNull();
    }

    [Fact]
    public async Task Objective_with_merely_weak_not_atrisk_prerequisite_is_not_blocked()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var (prerequisite, dependent) = SeedPrerequisiteChain(suffix);
        _mastery.WeakObjectiveKeys = [prerequisite.Key];

        await _sut.RegeneratePlanAsync(_profile.Id, "test");

        var obj = _db.StudentLearningPlanObjectives.Single(o => o.ObjectiveKey == dependent.Key);
        obj.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task Objective_with_no_prerequisite_edge_is_not_blocked()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var node = new SkillGraphNode(
            $"a1.speaking.solo_{suffix}", "Solo", "desc", CefrLevelConstants.A1, CurriculumSkillConstants.Speaking);
        node.Approve(null);
        _db.SkillGraphNodes.Add(node);
        _db.SaveChanges();
        _mastery.AtRiskObjectiveKeys = ["some.unrelated.node"];

        await _sut.RegeneratePlanAsync(_profile.Id, "test");

        var obj = _db.StudentLearningPlanObjectives.Single(o => o.ObjectiveKey == node.Key);
        obj.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task BlockedObjectives_count_reflects_a_real_blocked_objective()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var (prerequisite, _) = SeedPrerequisiteChain(suffix);
        _mastery.AtRiskObjectiveKeys = [prerequisite.Key];

        var summary = await _sut.RegeneratePlanAsync(_profile.Id, "test");

        summary.BlockedObjectives.Should().Be(1);
    }

    [Fact]
    public async Task GetNextPlannedObjective_skips_a_blocked_objective()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var (prerequisite, dependent) = SeedPrerequisiteChain(suffix);
        _mastery.AtRiskObjectiveKeys = [prerequisite.Key];

        await _sut.RegeneratePlanAsync(_profile.Id, "test");

        var next = await _sut.GetNextPlannedObjectiveAsync(_profile.Id);

        (next is null || next.ObjectiveKey != dependent.Key).Should().BeTrue(
            "the blocked objective must never be returned as the next planned objective");
    }
}

/// <summary>Trivial fake — <see cref="LearningPlanService"/> only needs a non-null resolved
/// context; the goal-context resolution logic itself is out of scope for this fix's tests.</summary>
internal sealed class FakeLearningGoalContextResolver : ILearningGoalContextResolver
{
    public ResolvedLearningGoalContext Resolve(StudentProfile profile, LearningGoalResolutionContext? context = null) =>
        new();
}
