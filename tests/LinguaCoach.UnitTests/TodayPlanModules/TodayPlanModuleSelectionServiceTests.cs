using FluentAssertions;
using LinguaCoach.Application.TodayPlanModules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.TodayPlanModules;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.TodayPlanModules;

/// <summary>
/// Phase H6 (renamed I4 Pass 3) — deterministic Today Plan module selector. Uses SQLite in-memory
/// (matches H3/H4/H5 generation test conventions) with directly-seeded Modules/Lessons/Activity
/// Definitions. All fixture content is synthetic.
/// </summary>
public sealed class TodayPlanModuleSelectionServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly TodayPlanModuleSelectionService _sut;

    public TodayPlanModuleSelectionServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new TodayPlanModuleSelectionService(_db);
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

    private Exercise SeedActivity(bool approved, string cefrLevel = "B1", string skill = "Vocabulary")
    {
        var activity = new Exercise("Gap fill: resilient", "Type the missing word.", "gap_fill", ExerciseRendererType.Formio,
            ExerciseSourceMode.Manual, cefrLevel: cefrLevel, skill: skill, estimatedMinutes: 5,
            formSchemaJson: "{\"components\":[]}", answerKeyJson: "{\"word_answer\":\"resilient\"}",
            scoringRulesJson: "{\"word\":{\"kind\":\"exact\"}}");
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
        var activity = SeedActivity(linkApprovedActivity, cefrLevel ?? "B1", skill ?? "Vocabulary");

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
    public async Task Context_and_focus_tags_influence_selection()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary", focusTagsJson: "[\"phrasal_verbs\"]", contextTagsJson: "[\"workplace\"]");
        var matching = SeedModule(cefrLevel: "B1", skill: "Grammar", focusTagsJson: "[\"reported_speech\"]", contextTagsJson: "[\"travel\"]");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(),
            focusAreas: ["reported_speech"], contextTags: ["travel"]));

        result.SelectedModules.Should().ContainSingle(m => m.ModuleId == matching.Id);
    }

    [Fact]
    public async Task Learning_plan_derived_skill_signal_influences_selection()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary");
        var weakSkillModule = SeedModule(cefrLevel: "B1", skill: "Writing");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), skill: "Writing"));

        result.SelectedModules.Should().ContainSingle(m => m.ModuleId == weakSkillModule.Id);
    }

    [Fact]
    public async Task Estimated_minutes_respects_preferred_session_length()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary", estimatedMinutes: 30);
        var closeFit = SeedModule(cefrLevel: "B1", skill: "Grammar", estimatedMinutes: 10);

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), preferredMinutes: 10));

        result.SelectedModules.Should().ContainSingle(m => m.ModuleId == closeFit.Id);
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
