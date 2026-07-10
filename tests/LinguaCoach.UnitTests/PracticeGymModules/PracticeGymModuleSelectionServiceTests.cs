using FluentAssertions;
using LinguaCoach.Application.PracticeGymModules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.PracticeGymModules;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.PracticeGymModules;

/// <summary>
/// Phase H7 — deterministic Practice Gym module selector. Uses SQLite in-memory (matches H6's
/// <c>TodayPlanModuleSelectionServiceTests</c> conventions) with directly-seeded Module
/// Definitions/Lessons/Exercises. All fixture content is synthetic.
/// </summary>
public sealed class PracticeGymModuleSelectionServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly PracticeGymModuleSelectionService _sut;

    public PracticeGymModuleSelectionServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new PracticeGymModuleSelectionService(_db);
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
        string? subskill = null,
        string? objectiveKey = null,
        int? difficultyBand = null,
        int? estimatedMinutes = 10,
        string contextTagsJson = "[]",
        string focusTagsJson = "[]",
        bool linkApprovedLesson = true,
        bool linkApprovedActivity = true)
    {
        var module = new Module("Resilience Module", ModuleSourceMode.Manual,
            objectiveKey: objectiveKey, cefrLevel: cefrLevel, skill: skill, subskill: subskill,
            estimatedMinutes: estimatedMinutes, contextTagsJson: contextTagsJson, focusTagsJson: focusTagsJson,
            difficultyBand: difficultyBand);
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

    private static PracticeGymModuleSelectionRequest Request(
        Guid studentId, string? cefr = "B1", string? skill = null, string? subskill = null,
        string? objectiveKey = null, int? difficulty = null,
        IReadOnlyList<string>? focusAreas = null, IReadOnlyList<string>? contextTags = null,
        IReadOnlyList<string>? weaknessSignals = null,
        IReadOnlyList<Guid>? recentIds = null, bool allowFallback = true, int maxSuggestions = 4) =>
        new(studentId, cefr, RequestedSkill: skill, RequestedSubskill: subskill,
            RequestedObjectiveKey: objectiveKey, RequestedDifficulty: difficulty,
            FocusAreas: focusAreas, ContextTags: contextTags, WeaknessSignals: weaknessSignals,
            RecentSuggestedModuleIds: recentIds, AllowFallback: allowFallback, MaxSuggestions: maxSuggestions);

    [Fact]
    public async Task Approved_module_selected_for_matching_skill_and_cefr()
    {
        var module = SeedModule(cefrLevel: "B1", skill: "Vocabulary");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), cefr: "B1", skill: "Vocabulary"));

        result.FallbackRequired.Should().BeFalse();
        result.Suggestions.Should().ContainSingle(s => s.ModuleId == module.Id);
    }

    [Fact]
    public async Task Pending_module_not_selected()
    {
        SeedModule(approved: false);

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        result.FallbackRequired.Should().BeTrue();
        result.Suggestions.Should().BeEmpty();
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
        result.Suggestions.Should().ContainSingle(s => s.ModuleId == b1Module.Id);
    }

    [Fact]
    public async Task Lower_level_module_selected_only_with_explicit_review_scaffold_remediation_reason()
    {
        var a2Module = SeedModule(cefrLevel: "A2", skill: "Vocabulary");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), cefr: "B1"));

        result.FallbackRequired.Should().BeFalse();
        var suggestion = result.Suggestions.Should().ContainSingle(s => s.ModuleId == a2Module.Id).Subject;
        suggestion.Reason.Should().ContainAny("review", "scaffold", "remediation", "fallback");
        suggestion.IsReview.Should().BeTrue();
    }

    [Fact]
    public async Task Requested_skill_influences_selection()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary");
        var writingModule = SeedModule(cefrLevel: "B1", skill: "Writing");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), skill: "Writing"));

        result.Suggestions.Should().ContainSingle(s => s.ModuleId == writingModule.Id);
    }

    [Fact]
    public async Task Requested_subskill_influences_selection()
    {
        SeedModule(cefrLevel: "B1", skill: "Writing", subskill: "Emails");
        var reportsModule = SeedModule(cefrLevel: "B1", skill: "Writing", subskill: "Reports");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), skill: "Writing", subskill: "Reports"));

        result.Suggestions.Should().Contain(s => s.ModuleId == reportsModule.Id);
        result.Suggestions[0].ModuleId.Should().Be(reportsModule.Id);
    }

    [Fact]
    public async Task Weakness_signals_influence_selection_and_mark_remediation()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary");
        var writingModule = SeedModule(cefrLevel: "B1", skill: "Writing");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), weaknessSignals: ["Writing"], maxSuggestions: 1));

        var suggestion = result.Suggestions.Should().ContainSingle().Subject;
        suggestion.ModuleId.Should().Be(writingModule.Id);
        suggestion.IsRemediation.Should().BeTrue();
    }

    [Fact]
    public async Task Context_and_focus_tags_influence_selection()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary", focusTagsJson: "[\"phrasal_verbs\"]", contextTagsJson: "[\"workplace\"]");
        var matching = SeedModule(cefrLevel: "B1", skill: "Grammar", focusTagsJson: "[\"reported_speech\"]", contextTagsJson: "[\"travel\"]");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(),
            focusAreas: ["reported_speech"], contextTags: ["travel"], maxSuggestions: 1));

        result.Suggestions.Should().ContainSingle(s => s.ModuleId == matching.Id);
    }

    [Fact]
    public async Task Estimated_minutes_and_difficulty_are_used_where_feasible()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary", difficultyBand: 5);
        var closeFit = SeedModule(cefrLevel: "B1", skill: "Grammar", difficultyBand: 2);

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), difficulty: 2, maxSuggestions: 1));

        result.Suggestions.Should().ContainSingle(s => s.ModuleId == closeFit.Id);
    }

    [Fact]
    public async Task Recently_suggested_module_not_suggested_again_too_soon()
    {
        var studentId = Guid.NewGuid();
        var module = SeedModule();

        var result = await _sut.SelectAsync(Request(studentId, recentIds: [module.Id]));

        result.FallbackRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Recently_suggested_module_excluded_via_assignment_history()
    {
        var studentId = Guid.NewGuid();
        var module = SeedModule();
        _db.StudentPracticeGymModuleAssignments.Add(new StudentPracticeGymModuleAssignment(
            studentId, module.Id, DateTimeOffset.UtcNow.AddDays(-3), PracticeGymModuleAssignmentStatus.Suggested));
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
    public async Task Selection_result_does_not_expose_answer_keys()
    {
        SeedModule();

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        json.Should().NotContain("AnswerKeyJson");
        json.Should().NotContain("word_answer");
    }

    [Fact]
    public async Task Selection_result_does_not_expose_scoring_rules()
    {
        SeedModule();

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        json.Should().NotContain("ScoringRulesJson");
        json.Should().NotContain("\"kind\":\"exact\"");
    }

    [Fact]
    public async Task Selection_creates_no_module_attempts()
    {
        SeedModule();

        await _sut.SelectAsync(Request(Guid.NewGuid()));

        (await _db.ActivityAttempts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Selection_does_not_update_mastery()
    {
        SeedModule();

        await _sut.SelectAsync(Request(Guid.NewGuid()));

        (await _db.StudentSkillProfiles.CountAsync()).Should().Be(0);
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
    public async Task Selection_does_not_mutate_lesson()
    {
        SeedModule();
        var lesson = await _db.Lessons.AsNoTracking().SingleAsync();

        await _sut.SelectAsync(Request(Guid.NewGuid()));

        var reloaded = await _db.Lessons.AsNoTracking().SingleAsync(i => i.Id == lesson.Id);
        reloaded.UpdatedAtUtc.Should().Be(lesson.UpdatedAtUtc);
    }

    [Fact]
    public async Task Selection_does_not_mutate_exercise()
    {
        SeedModule();
        var activity = await _db.Exercises.AsNoTracking().SingleAsync();

        await _sut.SelectAsync(Request(Guid.NewGuid()));

        var reloaded = await _db.Exercises.AsNoTracking().SingleAsync(a => a.Id == activity.Id);
        reloaded.UpdatedAtUtc.Should().Be(activity.UpdatedAtUtc);
    }

    [Fact]
    public async Task Today_module_pipeline_remains_unaffected()
    {
        var studentId = Guid.NewGuid();
        var module = SeedModule();
        _db.StudentTodayPlanModuleAssignments.Add(new StudentTodayPlanModuleAssignment(
            studentId, module.Id, DateTime.UtcNow.Date, TodayPlanModuleAssignmentStatus.Selected));
        _db.SaveChanges();

        await _sut.SelectAsync(Request(studentId));

        (await _db.StudentTodayPlanModuleAssignments.CountAsync()).Should().Be(1);
    }

    // ── Phase H10 — CanLaunch precomputation ─────────────────────────────────

    [Fact]
    public async Task Suggestion_reports_can_launch_false_for_unsupported_scoring_shape()
    {
        // This file's own SeedActivity fixture ("{"word":{"kind":"exact"}}") does not match the
        // real ScoringRulesDocument shape ActivityGenerationService actually produces — exactly
        // the kind of malformed/incompatible data H10's eligibility check must fail closed on.
        SeedModule();

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        result.FallbackRequired.Should().BeFalse();
        result.Suggestions.Should().ContainSingle().Which.CanLaunch.Should().BeFalse();
    }

    [Fact]
    public async Task Suggestion_reports_can_launch_true_for_launch_eligible_activity()
    {
        var module = new Module("Launchable Module", ModuleSourceMode.Manual, cefrLevel: "B1", skill: "Vocabulary");
        module.Approve(null);
        _db.Modules.Add(module);
        _db.SaveChanges();

        var lesson = SeedLesson(true);
        var activity = new Exercise("Gap fill: launchable", "Type the missing word.", "gap_fill", ExerciseRendererType.Formio,
            ExerciseSourceMode.Manual, cefrLevel: "B1", skill: "Vocabulary", estimatedMinutes: 5,
            formSchemaJson: "{\"components\":[{\"key\":\"answer\",\"type\":\"textfield\"}]}",
            scoringRulesJson: "{\"Components\":{\"answer\":{\"Kind\":\"text_normalized\",\"CorrectAnswer\":\"resilient\"}}}");
        activity.Approve(null);
        _db.Exercises.Add(activity);
        _db.SaveChanges();

        _db.ModuleLessonLinks.Add(new ModuleLessonLink(module.Id, lesson.Id, LessonResourceRole.Primary, 0));
        _db.ModuleExerciseLinks.Add(new ModuleExerciseLink(module.Id, activity.Id, ModuleExerciseRole.PrimaryPractice, 0));
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        result.FallbackRequired.Should().BeFalse();
        result.Suggestions.Should().ContainSingle().Which.CanLaunch.Should().BeTrue();
    }
}
