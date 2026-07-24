using FluentAssertions;
using LinguaCoach.Application.Composer;
using LinguaCoach.Application.Mastery;
using LinguaCoach.Application.TodayPlanModules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.TodayPlanModules;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.TodayPlanModules;

/// <summary>
/// Phase H6 (renamed I4 Pass 3) — Today Plan module selector. Uses SQLite in-memory (matches
/// H3/H4/H5 generation test conventions) with directly-seeded Modules/Lessons/Activity
/// Definitions. All fixture content is synthetic.
///
/// Adaptive Curriculum Sprint 5 — ranking is delegated to <see cref="ICurriculumComposerService"/>
/// (a real AI call in production), so these tests use <see cref="FakeCurriculumComposerService"/>
/// (pass-through by default, or a programmable ranking override) and
/// <see cref="FakeStudentMasteryEvaluationService"/> — never a real AI call, per this repo's
/// "tests use fake providers, never real AI" convention.
/// </summary>
public sealed class TodayPlanModuleSelectionServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly FakeCurriculumComposerService _composer;
    private readonly FakeStudentMasteryEvaluationService _mastery;
    private readonly TodayPlanModuleSelectionService _sut;

    public TodayPlanModuleSelectionServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _composer = new FakeCurriculumComposerService();
        _mastery = new FakeStudentMasteryEvaluationService();
        _sut = new TodayPlanModuleSelectionService(_db, _composer, _mastery);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private Lesson SeedLesson(bool approved, string cefrLevel = "B1", string skill = "Vocabulary")
    {
        var item = new Lesson("Resilient", "Resilient means able to recover quickly.", LessonSourceMode.Manual, cefrLevel, skill,
            examplesJson: "[\"She is resilient.\"]", commonMistakesJson: "[\"resilent\"]");
        if (approved) item.Approve(null);
        _db.Lessons.Add(item);
        _db.SaveChanges();
        return item;
    }

    private Exercise SeedActivity(bool approved, Guid lessonId, string cefrLevel = "B1", string skill = "Vocabulary")
    {
        var activity = new Exercise("Gap fill: resilient", "Type the missing word.", "gap_fill", ExerciseRendererType.Formio,
            ExerciseSourceMode.Manual, cefrLevel: cefrLevel, skill: skill, estimatedMinutes: 5,
            formSchemaJson: "{\"components\":[]}", answerKeyJson: "{\"word_answer\":\"resilient\"}",
            scoringRulesJson: "{\"word\":{\"kind\":\"exact\"}}", lessonId: lessonId);
        if (approved) activity.Approve(null);
        _db.Exercises.Add(activity);
        _db.SaveChanges();
        return activity;
    }

    private Module SeedModule(
        bool approved = true,
        string? cefrLevel = "B1",
        string? skill = "Vocabulary",
        int? estimatedMinutes = 10,
        string contextTagsJson = "[]",
        string focusTagsJson = "[]",
        bool linkApprovedLesson = true,
        bool linkApprovedActivity = true)
    {
        var module = new Module("Resilience Module", ModuleSourceMode.Manual,
            cefrLevel: cefrLevel, skill: skill, estimatedMinutes: estimatedMinutes,
            contextTagsJson: contextTagsJson, focusTagsJson: focusTagsJson);
        if (approved) module.Approve(null);
        _db.Modules.Add(module);
        _db.SaveChanges();

        var lesson = SeedLesson(linkApprovedLesson, cefrLevel ?? "B1", skill ?? "Vocabulary");
        var activity = SeedActivity(linkApprovedActivity, lesson.Id, cefrLevel ?? "B1", skill ?? "Vocabulary");

        _db.ModuleLessonLinks.Add(new ModuleLessonLink(module.Id, lesson.Id, LessonResourceRole.Primary, 0));
        _db.ModuleExerciseLinks.Add(new ModuleExerciseLink(module.Id, activity.Id, ModuleExerciseRole.PrimaryPractice, 0));
        _db.SaveChanges();

        return module;
    }

    private static TodayPlanModuleSelectionRequest Request(
        Guid studentId, string? cefr = "B1", string? skill = null, int? preferredMinutes = null,
        IReadOnlyList<string>? focusAreas = null, IReadOnlyList<string>? contextTags = null,
        IReadOnlyList<Guid>? recentIds = null, bool allowFallback = true, int maxModules = 1) =>
        new(studentId, cefr, LearningPlanId: null, TargetDate: DateTime.UtcNow.Date,
            PreferredSessionLengthMinutes: preferredMinutes, RequestedSkill: skill,
            FocusAreas: focusAreas, ContextTags: contextTags,
            RecentAssignedModuleIds: recentIds, AllowFallback: allowFallback, MaxModules: maxModules);

    [Fact]
    public async Task Approved_module_selected_for_matching_cefr_and_skill()
    {
        var module = SeedModule(cefrLevel: "B1", skill: "Vocabulary");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), cefr: "B1", skill: "Vocabulary"));

        result.FallbackRequired.Should().BeFalse();
        result.SelectedModules.Should().ContainSingle(m => m.ModuleId == module.Id);
    }

    [Fact]
    public async Task Pending_module_not_selected()
    {
        SeedModule(approved: false);

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        result.FallbackRequired.Should().BeTrue();
        result.SelectedModules.Should().BeEmpty();
    }

    [Fact]
    public async Task Rejected_module_not_selected()
    {
        var module = new Module("Bad Module", ModuleSourceMode.Manual, cefrLevel: "B1", skill: "Vocabulary");
        module.Reject("not good enough", null);
        _db.Modules.Add(module);
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        result.FallbackRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Approved_archived_module_not_selected()
    {
        var module = SeedModule(cefrLevel: "B1", skill: "Vocabulary");
        module.Archive();
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), cefr: "B1", skill: "Vocabulary"));

        result.FallbackRequired.Should().BeTrue();
        result.SelectedModules.Should().NotContain(m => m.ModuleId == module.Id);
    }

    [Fact]
    public async Task Approved_module_with_pending_lesson_not_selected()
    {
        SeedModule(linkApprovedLesson: false);

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        result.FallbackRequired.Should().BeTrue();
        result.FallbackReason.Should().Contain("approved Lesson");
    }

    [Fact]
    public async Task Approved_module_with_pending_exercise_not_selected()
    {
        SeedModule(linkApprovedActivity: false);

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        result.FallbackRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Wrong_cefr_module_not_silently_selected_when_exact_match_exists()
    {
        SeedModule(cefrLevel: "A2", skill: "Vocabulary");
        var b1Module = SeedModule(cefrLevel: "B1", skill: "Vocabulary");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), cefr: "B1"));

        result.FallbackRequired.Should().BeFalse();
        result.SelectedModules.Should().ContainSingle(m => m.ModuleId == b1Module.Id);
    }

    [Fact]
    public async Task Lower_level_module_selected_only_with_explicit_review_scaffold_reason()
    {
        var a2Module = SeedModule(cefrLevel: "A2", skill: "Vocabulary");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), cefr: "B1"));

        result.FallbackRequired.Should().BeFalse();
        result.SelectedModules.Should().ContainSingle(m => m.ModuleId == a2Module.Id);
        result.SelectedModules[0].Reason.Should().ContainAny("review", "scaffold", "fallback");
    }

    [Fact]
    public async Task Requested_skill_and_preferred_session_length_are_passed_to_composer()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary");
        SeedModule(cefrLevel: "B1", skill: "Writing");

        await _sut.SelectAsync(Request(Guid.NewGuid(), skill: "Writing", preferredMinutes: 12));

        _composer.LastRequest.Should().NotBeNull();
        _composer.LastRequest!.RequestedSkill.Should().Be("Writing");
        _composer.LastRequest.PreferredSessionLengthMinutes.Should().Be(12);
        _composer.LastRequest.Candidates.Should().HaveCount(2);
    }

    [Fact]
    public async Task Composers_ranked_order_determines_selected_module()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary");
        var preferred = SeedModule(cefrLevel: "B1", skill: "Grammar");

        _composer.RankingOverride = _ => [preferred.Id];

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        result.FallbackRequired.Should().BeFalse();
        result.SelectedModules.Should().ContainSingle(m => m.ModuleId == preferred.Id);
    }

    [Fact]
    public async Task Goal_vector_match_is_computed_and_passed_to_composer()
    {
        var studentId = Guid.NewGuid();
        var profile = new StudentProfile(studentId);
        _db.StudentProfiles.Add(profile);
        _db.SaveChanges();

        var goalMatching = SeedModule(cefrLevel: "B1", skill: "Vocabulary", contextTagsJson: "[\"workplace\"]");
        _db.StudentGoalWeights.Add(new StudentGoalWeight(profile.Id, "workplace", 0.8, StudentGoalWeightSource.Explicit));
        _db.SaveChanges();

        await _sut.SelectAsync(Request(profile.Id));

        var candidate = _composer.LastRequest!.Candidates.Single(c => c.ModuleId == goalMatching.Id);
        candidate.IsGoalMatch.Should().BeTrue();
    }

    [Fact]
    public async Task Skill_graph_weakness_match_is_computed_and_passed_to_composer()
    {
        var studentId = Guid.NewGuid();
        var weakModule = SeedModule(cefrLevel: "B1", skill: "Vocabulary");

        var node = new SkillGraphNode("b1.vocabulary.gap_test", "Gap Test Node", "desc", "B1", "Vocabulary");
        node.Approve(null);
        _db.SkillGraphNodes.Add(node);
        _db.SaveChanges();
        _db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(weakModule.Id, node.Id));
        _db.SaveChanges();

        _mastery.WeakObjectiveKeys = ["b1.vocabulary.gap_test"];

        await _sut.SelectAsync(Request(studentId));

        var candidate = _composer.LastRequest!.Candidates.Single(c => c.ModuleId == weakModule.Id);
        candidate.IsWeaknessMatch.Should().BeTrue();
    }

    // ── Skill Graph pipeline audit (2026-07-24, Bug #4) — prerequisite-gap signal ────────────────

    [Fact]
    public async Task Candidate_with_atrisk_prerequisite_is_flagged_as_unmet_prerequisite()
    {
        var studentId = Guid.NewGuid();
        var module = SeedModule(cefrLevel: "B1", skill: "Grammar");

        var prerequisite = new SkillGraphNode("b1.grammar.prereq", "Prereq", "desc", "B1", "Grammar");
        prerequisite.Approve(null);
        var node = new SkillGraphNode("b1.grammar.dependent", "Dependent", "desc", "B1", "Grammar");
        node.Approve(null);
        _db.SkillGraphNodes.AddRange(prerequisite, node);
        _db.SaveChanges();
        _db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(node.Id, prerequisite.Id));
        _db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(module.Id, node.Id));
        _db.SaveChanges();

        _mastery.AtRiskObjectiveKeys = ["b1.grammar.prereq"];

        await _sut.SelectAsync(Request(studentId));

        var candidate = _composer.LastRequest!.Candidates.Single(c => c.ModuleId == module.Id);
        candidate.HasUnmetPrerequisite.Should().BeTrue();
    }

    [Fact]
    public async Task Candidate_with_never_attempted_prerequisite_is_not_flagged()
    {
        // Bootstrap safety: a prerequisite with zero evidence must never block content, or nothing
        // could ever be shown to a brand-new student (every prerequisite starts unattempted).
        var studentId = Guid.NewGuid();
        var module = SeedModule(cefrLevel: "B1", skill: "Grammar");

        var prerequisite = new SkillGraphNode("b1.grammar.prereq_fresh", "Prereq", "desc", "B1", "Grammar");
        prerequisite.Approve(null);
        var node = new SkillGraphNode("b1.grammar.dependent_fresh", "Dependent", "desc", "B1", "Grammar");
        node.Approve(null);
        _db.SkillGraphNodes.AddRange(prerequisite, node);
        _db.SaveChanges();
        _db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(node.Id, prerequisite.Id));
        _db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(module.Id, node.Id));
        _db.SaveChanges();

        // No AtRisk/Weak keys set at all — the prerequisite has never been attempted.

        await _sut.SelectAsync(Request(studentId));

        var candidate = _composer.LastRequest!.Candidates.Single(c => c.ModuleId == module.Id);
        candidate.HasUnmetPrerequisite.Should().BeFalse();
    }

    [Fact]
    public async Task Candidate_with_merely_weak_not_atrisk_prerequisite_is_not_flagged()
    {
        // Threshold check: "Weak" (NeedsPractice/NeedsReview) is real signal for IsWeaknessMatch,
        // but must NOT count as an unmet prerequisite — only AtRisk (clear struggle) does.
        var studentId = Guid.NewGuid();
        var module = SeedModule(cefrLevel: "B1", skill: "Grammar");

        var prerequisite = new SkillGraphNode("b1.grammar.prereq_weak", "Prereq", "desc", "B1", "Grammar");
        prerequisite.Approve(null);
        var node = new SkillGraphNode("b1.grammar.dependent_weak", "Dependent", "desc", "B1", "Grammar");
        node.Approve(null);
        _db.SkillGraphNodes.AddRange(prerequisite, node);
        _db.SaveChanges();
        _db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(node.Id, prerequisite.Id));
        _db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(module.Id, node.Id));
        _db.SaveChanges();

        _mastery.WeakObjectiveKeys = ["b1.grammar.prereq_weak"];

        await _sut.SelectAsync(Request(studentId));

        var candidate = _composer.LastRequest!.Candidates.Single(c => c.ModuleId == module.Id);
        candidate.HasUnmetPrerequisite.Should().BeFalse();
    }

    [Fact]
    public async Task Candidate_with_no_prerequisite_edges_is_not_flagged()
    {
        var studentId = Guid.NewGuid();
        var module = SeedModule(cefrLevel: "B1", skill: "Grammar");

        var node = new SkillGraphNode("b1.grammar.no_prereq", "No Prereq", "desc", "B1", "Grammar");
        node.Approve(null);
        _db.SkillGraphNodes.Add(node);
        _db.SaveChanges();
        _db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(module.Id, node.Id));
        _db.SaveChanges();

        // Something else is AtRisk, but nothing this module's node depends on.
        _mastery.AtRiskObjectiveKeys = ["some.unrelated.node"];

        await _sut.SelectAsync(Request(studentId));

        var candidate = _composer.LastRequest!.Candidates.Single(c => c.ModuleId == module.Id);
        candidate.HasUnmetPrerequisite.Should().BeFalse();
    }

    [Fact]
    public async Task Candidate_two_hops_from_the_atrisk_node_is_not_flagged()
    {
        // Only DIRECT prerequisites are checked (documented, deliberate one-hop scope) — C requires
        // B, B requires A, student is AtRisk on A only. C's candidate must NOT be flagged, since
        // C's own direct prerequisite (B) isn't itself AtRisk.
        var studentId = Guid.NewGuid();
        var module = SeedModule(cefrLevel: "B1", skill: "Grammar");

        var nodeA = new SkillGraphNode("b1.grammar.chain_a", "A", "desc", "B1", "Grammar");
        nodeA.Approve(null);
        var nodeB = new SkillGraphNode("b1.grammar.chain_b", "B", "desc", "B1", "Grammar");
        nodeB.Approve(null);
        var nodeC = new SkillGraphNode("b1.grammar.chain_c", "C", "desc", "B1", "Grammar");
        nodeC.Approve(null);
        _db.SkillGraphNodes.AddRange(nodeA, nodeB, nodeC);
        _db.SaveChanges();
        _db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(nodeB.Id, nodeA.Id)); // B requires A
        _db.SkillGraphPrerequisiteEdges.Add(new SkillGraphPrerequisiteEdge(nodeC.Id, nodeB.Id)); // C requires B
        _db.ModuleSkillGraphNodeLinks.Add(new ModuleSkillGraphNodeLink(module.Id, nodeC.Id));
        _db.SaveChanges();

        _mastery.AtRiskObjectiveKeys = ["b1.grammar.chain_a"];

        await _sut.SelectAsync(Request(studentId));

        var candidate = _composer.LastRequest!.Candidates.Single(c => c.ModuleId == module.Id);
        candidate.HasUnmetPrerequisite.Should().BeFalse();
    }

    [Fact]
    public async Task Candidate_with_no_linked_node_is_not_flagged()
    {
        var studentId = Guid.NewGuid();
        var module = SeedModule(cefrLevel: "B1", skill: "Grammar");
        _mastery.AtRiskObjectiveKeys = ["some.unrelated.node"];

        await _sut.SelectAsync(Request(studentId));

        var candidate = _composer.LastRequest!.Candidates.Single(c => c.ModuleId == module.Id);
        candidate.HasUnmetPrerequisite.Should().BeFalse();
    }

    [Fact]
    public async Task Composer_failure_returns_fallback_not_exception()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary");
        _composer.ForceFailure = true;

        var act = async () => await _sut.SelectAsync(Request(Guid.NewGuid()));

        var result = await act.Should().NotThrowAsync();
        result.Subject.FallbackRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Recently_used_module_not_selected_again_too_soon()
    {
        var studentId = Guid.NewGuid();
        var module = SeedModule();

        var result = await _sut.SelectAsync(Request(studentId, recentIds: [module.Id]));

        result.FallbackRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Recently_used_module_excluded_via_assignment_history()
    {
        var studentId = Guid.NewGuid();
        var module = SeedModule();
        _db.StudentTodayPlanModuleAssignments.Add(new StudentTodayPlanModuleAssignment(
            studentId, module.Id, DateTime.UtcNow.Date.AddDays(-3), TodayPlanModuleAssignmentStatus.Selected));
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(studentId));

        result.FallbackRequired.Should().BeTrue();
    }

    [Fact]
    public async Task No_suitable_module_returns_fallback_required_not_exception()
    {
        var act = async () => await _sut.SelectAsync(Request(Guid.NewGuid()));

        var result = await act.Should().NotThrowAsync();
        result.Subject.FallbackRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Malformed_module_json_handled_safely()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary", contextTagsJson: "{not valid json", focusTagsJson: "also not json");

        var act = async () => await _sut.SelectAsync(Request(Guid.NewGuid(), contextTags: ["workplace"]));

        var result = await act.Should().NotThrowAsync();
        result.Subject.FallbackRequired.Should().BeFalse();
    }

    [Fact]
    public async Task No_cefr_student_uses_safe_broad_matching()
    {
        SeedModule(cefrLevel: "C1", skill: "Vocabulary");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), cefr: null));

        result.FallbackRequired.Should().BeFalse();
    }

    [Fact]
    public async Task No_learning_plan_signal_uses_safe_broad_matching()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), skill: null, focusAreas: null, contextTags: null));

        result.FallbackRequired.Should().BeFalse();
    }

    [Fact]
    public async Task Selection_result_does_not_expose_answer_keys()
    {
        SeedModule();

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        json.Should().NotContain("resilient\":\"resilient");
        json.Should().NotContain("AnswerKeyJson");
        json.Should().NotContain("ScoringRulesJson");
    }

    [Fact]
    public async Task Selection_creates_no_module_attempts()
    {
        SeedModule();

        await _sut.SelectAsync(Request(Guid.NewGuid()));

        (await _db.ActivityAttempts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Selection_does_not_mutate_module()
    {
        var module = SeedModule();
        var titleBefore = module.Title;
        var updatedAtBefore = module.UpdatedAtUtc;

        await _sut.SelectAsync(Request(Guid.NewGuid()));

        var reloaded = await _db.Modules.AsNoTracking().SingleAsync(m => m.Id == module.Id);
        reloaded.Title.Should().Be(titleBefore);
        reloaded.UpdatedAtUtc.Should().Be(updatedAtBefore);
    }

    [Fact]
    public async Task Selection_does_not_mutate_lesson_or_exercise()
    {
        var module = SeedModule();
        var lesson = await _db.Lessons.AsNoTracking().SingleAsync();
        var activity = await _db.Exercises.AsNoTracking().SingleAsync();

        await _sut.SelectAsync(Request(Guid.NewGuid()));

        var reloadedLesson = await _db.Lessons.AsNoTracking().SingleAsync(i => i.Id == lesson.Id);
        var reloadedActivity = await _db.Exercises.AsNoTracking().SingleAsync(a => a.Id == activity.Id);
        reloadedLesson.UpdatedAtUtc.Should().Be(lesson.UpdatedAtUtc);
        reloadedActivity.UpdatedAtUtc.Should().Be(activity.UpdatedAtUtc);
    }

    [Fact]
    public async Task Selection_creates_no_learning_activity_records()
    {
        SeedModule();

        await _sut.SelectAsync(Request(Guid.NewGuid()));

        (await _db.LearningActivities.CountAsync()).Should().Be(0);
    }
}

/// <summary>Adaptive Curriculum Sprint 5 — test double for <see cref="ICurriculumComposerService"/>.
/// Pass-through (candidates in given order, capped to MaxResults) by default; set
/// <see cref="RankingOverride"/> to control exact selection/order, or <see cref="ForceFailure"/> to
/// exercise the caller's fallback path. Captures the last request for assertions on what the
/// selector computed and handed to the composer.</summary>
internal sealed class FakeCurriculumComposerService : ICurriculumComposerService
{
    public Func<ComposerRankingRequest, IReadOnlyList<Guid>>? RankingOverride { get; set; }
    public bool ForceFailure { get; set; }
    public ComposerRankingRequest? LastRequest { get; private set; }

    public Task<ComposerRankingResult> RankCandidatesAsync(ComposerRankingRequest request, CancellationToken ct = default)
    {
        LastRequest = request;

        if (ForceFailure)
            return Task.FromResult(new ComposerRankingResult(false, [], null, "forced failure for test"));

        var ranked = RankingOverride?.Invoke(request)
            ?? request.Candidates.Select(c => c.ModuleId).Take(request.MaxResults).ToList();

        return Task.FromResult(new ComposerRankingResult(true, ranked, "fake composer selection", null));
    }
}

/// <summary>Adaptive Curriculum Sprint 5 — test double for <see cref="IStudentMasteryEvaluationService"/>.
/// Returns an empty report by default; set <see cref="WeakObjectiveKeys"/>/
/// <see cref="AtRiskObjectiveKeys"/> to control which skill-graph node keys the selector under
/// test resolves as weakness matches.</summary>
internal sealed class FakeStudentMasteryEvaluationService : IStudentMasteryEvaluationService
{
    public IReadOnlyList<string> WeakObjectiveKeys { get; set; } = [];
    public IReadOnlyList<string> AtRiskObjectiveKeys { get; set; } = [];

    public Task<StudentMasteryReport> EvaluateStudentAsync(
        Guid studentId, MasteryEvaluationReason reason, CancellationToken ct = default) =>
        Task.FromResult(new StudentMasteryReport
        {
            StudentId = studentId,
            EvaluatedAtUtc = DateTime.UtcNow,
            Reason = reason,
            MasteredObjectiveKeys = [],
            CompletedObjectiveKeys = [],
            WeakObjectiveKeys = WeakObjectiveKeys,
            AtRiskObjectiveKeys = AtRiskObjectiveKeys,
            DemotedCount = 0,
            SkippedCount = 0,
            MarkedReviewOnlyCount = 0
        });

    public Task<ObjectiveMasterySignal> EvaluateObjectiveMasteryAsync(
        Guid studentId, string objectiveKey, CancellationToken ct = default) =>
        Task.FromResult(new ObjectiveMasterySignal
        {
            ObjectiveKey = objectiveKey,
            SkillKey = null,
            MasteryStatus = MasteryStatus.InsufficientEvidence,
            EvidenceCount = 0,
            ConsecutiveSuccesses = 0,
            ConsecutiveFailures = 0,
            RecentAverageScore = 0,
            LastSeenUtc = null
        });
}
