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
/// <c>DailyLessonModuleSelectionServiceTests</c> conventions) with directly-seeded Module
/// Definitions/Learn Items/Activity Definitions. All fixture content is synthetic.
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
        string? subskill = null,
        string? objectiveKey = null,
        int? difficultyBand = null,
        int? estimatedMinutes = 10,
        string contextTagsJson = "[]",
        string focusTagsJson = "[]",
        bool linkApprovedLearnItem = true,
        bool linkApprovedActivity = true)
    {
        var module = new ModuleDefinition("Resilience Module", ModuleSourceMode.Manual,
            objectiveKey: objectiveKey, cefrLevel: cefrLevel, skill: skill, subskill: subskill,
            estimatedMinutes: estimatedMinutes, contextTagsJson: contextTagsJson, focusTagsJson: focusTagsJson,
            difficultyBand: difficultyBand);
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

    private static PracticeGymModuleSelectionRequest Request(
        Guid studentId, string? cefr = "B1", string? skill = null, string? subskill = null,
        string? objectiveKey = null, int? difficulty = null,
        IReadOnlyList<string>? focusAreas = null, IReadOnlyList<string>? contextTags = null,
        IReadOnlyList<string>? weaknessSignals = null,
        IReadOnlyList<Guid>? recentIds = null, bool allowFallback = true, int maxSuggestions = 4) =>
        new(studentId, cefr, RequestedSkill: skill, RequestedSubskill: subskill,
            RequestedObjectiveKey: objectiveKey, RequestedDifficulty: difficulty,
            FocusAreas: focusAreas, ContextTags: contextTags, WeaknessSignals: weaknessSignals,
            RecentSuggestedModuleDefinitionIds: recentIds, AllowFallback: allowFallback, MaxSuggestions: maxSuggestions);

    [Fact]
    public async Task Approved_module_selected_for_matching_skill_and_cefr()
    {
        var module = SeedModule(cefrLevel: "B1", skill: "Vocabulary");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), cefr: "B1", skill: "Vocabulary"));

        result.FallbackRequired.Should().BeFalse();
        result.Suggestions.Should().ContainSingle(s => s.ModuleDefinitionId == module.Id);
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
        result.Suggestions.Should().ContainSingle(s => s.ModuleDefinitionId == b1Module.Id);
    }

    [Fact]
    public async Task Lower_level_module_selected_only_with_explicit_review_scaffold_remediation_reason()
    {
        var a2Module = SeedModule(cefrLevel: "A2", skill: "Vocabulary");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), cefr: "B1"));

        result.FallbackRequired.Should().BeFalse();
        var suggestion = result.Suggestions.Should().ContainSingle(s => s.ModuleDefinitionId == a2Module.Id).Subject;
        suggestion.Reason.Should().ContainAny("review", "scaffold", "remediation", "fallback");
        suggestion.IsReview.Should().BeTrue();
    }

    [Fact]
    public async Task Requested_skill_influences_selection()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary");
        var writingModule = SeedModule(cefrLevel: "B1", skill: "Writing");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), skill: "Writing"));

        result.Suggestions.Should().ContainSingle(s => s.ModuleDefinitionId == writingModule.Id);
    }

    [Fact]
    public async Task Requested_subskill_influences_selection()
    {
        SeedModule(cefrLevel: "B1", skill: "Writing", subskill: "Emails");
        var reportsModule = SeedModule(cefrLevel: "B1", skill: "Writing", subskill: "Reports");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), skill: "Writing", subskill: "Reports"));

        result.Suggestions.Should().Contain(s => s.ModuleDefinitionId == reportsModule.Id);
        result.Suggestions[0].ModuleDefinitionId.Should().Be(reportsModule.Id);
    }

    [Fact]
    public async Task Weakness_signals_influence_selection_and_mark_remediation()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary");
        var writingModule = SeedModule(cefrLevel: "B1", skill: "Writing");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), weaknessSignals: ["Writing"], maxSuggestions: 1));

        var suggestion = result.Suggestions.Should().ContainSingle().Subject;
        suggestion.ModuleDefinitionId.Should().Be(writingModule.Id);
        suggestion.IsRemediation.Should().BeTrue();
    }

    [Fact]
    public async Task Context_and_focus_tags_influence_selection()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary", focusTagsJson: "[\"phrasal_verbs\"]", contextTagsJson: "[\"workplace\"]");
        var matching = SeedModule(cefrLevel: "B1", skill: "Grammar", focusTagsJson: "[\"reported_speech\"]", contextTagsJson: "[\"travel\"]");

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(),
            focusAreas: ["reported_speech"], contextTags: ["travel"], maxSuggestions: 1));

        result.Suggestions.Should().ContainSingle(s => s.ModuleDefinitionId == matching.Id);
    }

    [Fact]
    public async Task Estimated_minutes_and_difficulty_are_used_where_feasible()
    {
        SeedModule(cefrLevel: "B1", skill: "Vocabulary", difficultyBand: 5);
        var closeFit = SeedModule(cefrLevel: "B1", skill: "Grammar", difficultyBand: 2);

        var result = await _sut.SelectAsync(Request(Guid.NewGuid(), difficulty: 2, maxSuggestions: 1));

        result.Suggestions.Should().ContainSingle(s => s.ModuleDefinitionId == closeFit.Id);
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
    public async Task Selection_does_not_mutate_learn_item()
    {
        SeedModule();
        var learnItem = await _db.LearnItems.AsNoTracking().SingleAsync();

        await _sut.SelectAsync(Request(Guid.NewGuid()));

        var reloaded = await _db.LearnItems.AsNoTracking().SingleAsync(i => i.Id == learnItem.Id);
        reloaded.UpdatedAtUtc.Should().Be(learnItem.UpdatedAtUtc);
    }

    [Fact]
    public async Task Selection_does_not_mutate_activity_definition()
    {
        SeedModule();
        var activity = await _db.ActivityDefinitions.AsNoTracking().SingleAsync();

        await _sut.SelectAsync(Request(Guid.NewGuid()));

        var reloaded = await _db.ActivityDefinitions.AsNoTracking().SingleAsync(a => a.Id == activity.Id);
        reloaded.UpdatedAtUtc.Should().Be(activity.UpdatedAtUtc);
    }

    [Fact]
    public async Task Selection_does_not_delete_or_replace_practice_gym_cache_records()
    {
        var studentProfileId = Guid.NewGuid();
        SeedModule();
        var cache = new PracticeActivityCache(studentProfileId, "gap_fill", "B1", "1", "fingerprint-1");
        _db.PracticeActivityCache.Add(cache);
        _db.SaveChanges();

        await _sut.SelectAsync(Request(Guid.NewGuid()));

        (await _db.PracticeActivityCache.CountAsync()).Should().Be(1);
        var reloaded = await _db.PracticeActivityCache.AsNoTracking().SingleAsync();
        reloaded.Status.Should().Be(PracticeCacheStatus.Pending);
    }

    [Fact]
    public async Task Today_module_pipeline_remains_unaffected()
    {
        var studentId = Guid.NewGuid();
        var module = SeedModule();
        _db.StudentDailyModuleAssignments.Add(new StudentDailyModuleAssignment(
            studentId, module.Id, DateTime.UtcNow.Date, DailyModuleAssignmentStatus.Selected));
        _db.SaveChanges();

        await _sut.SelectAsync(Request(studentId));

        (await _db.StudentDailyModuleAssignments.CountAsync()).Should().Be(1);
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
        var module = new ModuleDefinition("Launchable Module", ModuleSourceMode.Manual, cefrLevel: "B1", skill: "Vocabulary");
        module.Approve(null);
        _db.ModuleDefinitions.Add(module);
        _db.SaveChanges();

        var learnItem = SeedLearnItem(true);
        var activity = new ActivityDefinition("Gap fill: launchable", "Type the missing word.", "gap_fill", ActivityRendererType.Formio,
            ActivitySourceMode.Manual, cefrLevel: "B1", skill: "Vocabulary", estimatedMinutes: 5,
            formSchemaJson: "{\"components\":[{\"key\":\"answer\",\"type\":\"textfield\"}]}",
            scoringRulesJson: "{\"Components\":{\"answer\":{\"Kind\":\"text_normalized\",\"CorrectAnswer\":\"resilient\"}}}");
        activity.Approve(null);
        _db.ActivityDefinitions.Add(activity);
        _db.SaveChanges();

        _db.ModuleDefinitionLearnItemLinks.Add(new ModuleDefinitionLearnItemLink(module.Id, learnItem.Id, LearnItemResourceRole.Primary, 0));
        _db.ModuleDefinitionActivityLinks.Add(new ModuleDefinitionActivityLink(module.Id, activity.Id, ModuleActivityRole.PrimaryPractice, 0));
        _db.SaveChanges();

        var result = await _sut.SelectAsync(Request(Guid.NewGuid()));

        result.FallbackRequired.Should().BeFalse();
        result.Suggestions.Should().ContainSingle().Which.CanLaunch.Should().BeTrue();
    }
}
