using FluentAssertions;
using LinguaCoach.Application.DailyLessonModules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.DailyLessonModules;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.DailyLessonModules;

/// <summary>
/// Phase H6 — deterministic Daily Lesson module selector. Uses SQLite in-memory (matches H3/H4/H5
/// generation test conventions) with directly-seeded Module Definitions/Learn Items/Activity
/// Definitions. All fixture content is synthetic.
/// </summary>
public sealed class DailyLessonModuleSelectionServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly DailyLessonModuleSelectionService _sut;

    public DailyLessonModuleSelectionServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new DailyLessonModuleSelectionService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private LearnItem SeedLearnItem(bool approved, string cefrLevel = "B1", string skill = "Vocabulary")
    {
        var item = new LearnItem("Resilient", "Resilient means able to recover quickly.", LearnItemSourceMode.Manual, cefrLevel, skill,
            examplesJson: "[\"She is resilient.\"]", commonMistakesJson: "[\"resilent\"]");
        if (approved) item.Approve(null);
        _db.LearnItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    private ActivityDefinition SeedActivity(bool approved, string cefrLevel = "B1", string skill = "Vocabulary")
    {
        var activity = new ActivityDefinition("Gap fill: resilient", "Type the missing word.", "gap_fill", ActivityRendererType.Formio,
            ActivitySourceMode.Manual, cefrLevel: cefrLevel, skill: skill, estimatedMinutes: 5,
            formSchemaJson: "{\"components\":[]}", answerKeyJson: "{\"word_answer\":\"resilient\"}",
            scoringRulesJson: "{\"word\":{\"kind\":\"exact\"}}");
        if (approved) activity.Approve(null);
        _db.ActivityDefinitions.Add(activity);
        _db.SaveChanges();
        return activity;
    }

    private ModuleDefinition SeedModule(
        bool approved = true,
        string? cefrLevel = "B1",
        string? skill = "Vocabulary",
        int? estimatedMinutes = 10,
        string contextTagsJson = "[]",
        string focusTagsJson = "[]",
        bool linkApprovedLearnItem = true,
        bool linkApprovedActivity = true)
    {
        var module = new ModuleDefinition("Resilience Module", ModuleSourceMode.Manual,
            cefrLevel: cefrLevel, skill: skill, estimatedMinutes: estimatedMinutes,
            contextTagsJson: contextTagsJson, focusTagsJson: focusTagsJson);
        if (approved) module.Approve(null);
        _db.ModuleDefinitions.Add(module);
        _db.SaveChanges();

        var learnItem = SeedLearnItem(linkApprovedLearnItem, cefrLevel ?? "B1", skill ?? "Vocabulary");
        var activity = SeedActivity(linkApprovedActivity, cefrLevel ?? "B1", skill ?? "Vocabulary");

        _db.ModuleDefinitionLearnItemLinks.Add(new ModuleDefinitionLearnItemLink(module.Id, learnItem.Id, LearnItemResourceRole.Primary, 0));
        _db.ModuleDefinitionActivityLinks.Add(new ModuleDefinitionActivityLink(module.Id, activity.Id, ModuleActivityRole.PrimaryPractice, 0));
        _db.SaveChanges();

        return module;
    }

    private static DailyLessonModuleSelectionRequest Request(
        Guid studentId, string? cefr = "B1", string? skill = null, int? preferredMinutes = null,
        IReadOnlyList<string>? focusAreas = null, IReadOnlyList<string>? contextTags = null,
        IReadOnlyList<Guid>? recentIds = null, bool allowFallback = true, int maxModules = 1) =>
        new(studentId, cefr, LearningPlanId: null, TargetDate: DateTime.UtcNow.Date,
            PreferredSessionLengthMinutes: preferredMinutes, RequestedSkill: skill,
            FocusAreas: focusAreas, ContextTags: contextTags,
            RecentAssignedModuleDefinitionIds: recentIds, AllowFallback: allowFallback, MaxModules: maxModules);

    [Fact]
    public async Task Approved_module_selected_for_matching_cefr_and_skill()
    {
        var module = SeedModule(cefrLevel: "B1", skill: "Vocabulary");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), cefr: "B1", skill: "Vocabulary"));

        result.FallbackRequired.Should().BeFalse();
        result.SelectedModules.Should().ContainSingle(m => m.ModuleDefinitionId == module.Id);
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
        var module = new ModuleDefinition("Bad Module", ModuleSourceMode.Manual, cefrLevel: "B1", skill: "Vocabulary");
        module.Reject("not good enough", null);
        _db.ModuleDefinitions.Add(module);
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        result.FallbackRequired.Should().BeTrue();
    }

    [Fact]
    public async Task Approved_module_with_pending_learn_item_not_selected()
    {
        SeedModule(linkApprovedLearnItem: false);

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        result.FallbackRequired.Should().BeTrue();
        result.FallbackReason.Should().Contain("approved Learn Item");
    }

    [Fact]
    public async Task Approved_module_with_pending_activity_definition_not_selected()
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
        result.SelectedModules.Should().ContainSingle(m => m.ModuleDefinitionId == b1Module.Id);
    }

    [Fact]
    public async Task Lower_level_module_selected_only_with_explicit_review_scaffold_reason()
    {
        var a2Module = SeedModule(cefrLevel: "A2", skill: "Vocabulary");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), cefr: "B1"));

        result.FallbackRequired.Should().BeFalse();
        result.SelectedModules.Should().ContainSingle(m => m.ModuleDefinitionId == a2Module.Id);
        result.SelectedModules[0].Reason.Should().ContainAny("review", "scaffold", "fallback");
    }

    [Fact]
    public async Task Context_and_focus_tags_influence_selection()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary", focusTagsJson: "[\"phrasal_verbs\"]", contextTagsJson: "[\"workplace\"]");
        var matching = SeedModule(cefrLevel: "B1", skill: "Grammar", focusTagsJson: "[\"reported_speech\"]", contextTagsJson: "[\"travel\"]");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(),
            focusAreas: ["reported_speech"], contextTags: ["travel"]));

        result.SelectedModules.Should().ContainSingle(m => m.ModuleDefinitionId == matching.Id);
    }

    [Fact]
    public async Task Learning_plan_derived_skill_signal_influences_selection()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary");
        var weakSkillModule = SeedModule(cefrLevel: "B1", skill: "Writing");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), skill: "Writing"));

        result.SelectedModules.Should().ContainSingle(m => m.ModuleDefinitionId == weakSkillModule.Id);
    }

    [Fact]
    public async Task Estimated_minutes_respects_preferred_session_length()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary", estimatedMinutes: 30);
        var closeFit = SeedModule(cefrLevel: "B1", skill: "Grammar", estimatedMinutes: 10);

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), preferredMinutes: 10));

        result.SelectedModules.Should().ContainSingle(m => m.ModuleDefinitionId == closeFit.Id);
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
        _db.StudentDailyModuleAssignments.Add(new StudentDailyModuleAssignment(
            studentId, module.Id, DateTime.UtcNow.Date.AddDays(-3), DailyModuleAssignmentStatus.Selected));
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
    public async Task Selection_does_not_mutate_module_definition()
    {
        var module = SeedModule();
        var titleBefore = module.Title;
        var updatedAtBefore = module.UpdatedAtUtc;

        await _sut.SelectAsync(Request(Guid.NewGuid()));

        var reloaded = await _db.ModuleDefinitions.AsNoTracking().SingleAsync(m => m.Id == module.Id);
        reloaded.Title.Should().Be(titleBefore);
        reloaded.UpdatedAtUtc.Should().Be(updatedAtBefore);
    }

    [Fact]
    public async Task Selection_does_not_mutate_learn_item_or_activity_definition()
    {
        var module = SeedModule();
        var learnItem = await _db.LearnItems.AsNoTracking().SingleAsync();
        var activity = await _db.ActivityDefinitions.AsNoTracking().SingleAsync();

        await _sut.SelectAsync(Request(Guid.NewGuid()));

        var reloadedLearnItem = await _db.LearnItems.AsNoTracking().SingleAsync(i => i.Id == learnItem.Id);
        var reloadedActivity = await _db.ActivityDefinitions.AsNoTracking().SingleAsync(a => a.Id == activity.Id);
        reloadedLearnItem.UpdatedAtUtc.Should().Be(learnItem.UpdatedAtUtc);
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
