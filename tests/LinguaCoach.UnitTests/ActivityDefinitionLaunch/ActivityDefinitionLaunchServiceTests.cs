using FluentAssertions;
using LinguaCoach.Application.ActivityDefinitionLaunch;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ActivityDefinitionLaunch;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.ActivityDefinitionLaunch;

/// <summary>
/// Phase H10 — the ActivityDefinition launch bridge. Uses SQLite in-memory (matches H6/H7's
/// selection-service test conventions) with directly-seeded Module Definitions/Learn Items/
/// Activity Definitions. All fixture content is synthetic.
/// </summary>
public sealed class ActivityDefinitionLaunchServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ActivityDefinitionLaunchService _sut;

    public ActivityDefinitionLaunchServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ActivityDefinitionLaunchService(_db, NullLogger<ActivityDefinitionLaunchService>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private const string ValidFormSchema = "{\"components\":[{\"key\":\"answer\",\"type\":\"textfield\"}]}";
    // ScoringRulesDocument/ComponentScoringRule are deserialized with default (PascalCase,
    // case-sensitive) System.Text.Json settings — matches how ActivityGenerationService actually
    // serializes ActivityDefinition.ScoringRulesJson (no camelCase naming policy is applied there).
    private const string ValidScoring = "{\"Components\":{\"answer\":{\"Kind\":\"text_normalized\",\"CorrectAnswer\":\"resilient\"}}}";
    private const string ManualEvalScoring = "{\"Components\":{\"answer\":{\"Kind\":\"text_normalized\",\"RequiresManualOrAiEvaluation\":true}}}";

    private LearnItem SeedLearnItem(bool approved = true)
    {
        var item = new LearnItem("Resilient", "Resilient means able to recover quickly.", LearnItemSourceMode.Manual, "B1", "Vocabulary");
        if (approved) item.Approve(null);
        _db.LearnItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    private ActivityDefinition SeedActivity(
        bool approved = true,
        string activityType = "gap_fill",
        ActivityRendererType rendererType = ActivityRendererType.Formio,
        string? formSchemaJson = ValidFormSchema,
        string? scoringRulesJson = ValidScoring,
        string? answerKeyJson = "{\"word_answer\":\"resilient\"}")
    {
        var activity = new ActivityDefinition("Gap fill: resilient", "Type the missing word.", activityType, rendererType,
            ActivitySourceMode.Manual, cefrLevel: "B1", skill: "Vocabulary", estimatedMinutes: 5,
            formSchemaJson: formSchemaJson, answerKeyJson: answerKeyJson, scoringRulesJson: scoringRulesJson);
        if (approved) activity.Approve(null);
        _db.ActivityDefinitions.Add(activity);
        _db.SaveChanges();
        return activity;
    }

    private ModuleDefinition SeedModule(
        bool approved = true,
        LearnItem? learnItem = null,
        ActivityDefinition? activity = null,
        bool linkActivity = true)
    {
        var module = new ModuleDefinition("Resilience Module", ModuleSourceMode.Manual, cefrLevel: "B1", skill: "Vocabulary");
        if (approved) module.Approve(null);
        _db.ModuleDefinitions.Add(module);
        _db.SaveChanges();

        learnItem ??= SeedLearnItem();
        _db.ModuleDefinitionLearnItemLinks.Add(new ModuleDefinitionLearnItemLink(module.Id, learnItem.Id, LearnItemResourceRole.Primary, 0));

        if (linkActivity)
        {
            activity ??= SeedActivity();
            _db.ModuleDefinitionActivityLinks.Add(new ModuleDefinitionActivityLink(module.Id, activity.Id, ModuleActivityRole.PrimaryPractice, 0));
        }

        _db.SaveChanges();
        return module;
    }

    private static ActivityDefinitionLaunchRequest Request(Guid moduleId, Guid studentId) =>
        new(studentId, moduleId, ActivityDefinitionLaunchSource.PracticeGym);

    [Fact]
    public async Task Launch_allowed_for_approved_module_with_approved_supported_activity()
    {
        var module = SeedModule();

        var result = await _sut.LaunchAsync(Request(module.Id, Guid.NewGuid()));

        result.Success.Should().BeTrue();
        result.LearningActivityId.Should().NotBeNull();
        result.CanSubmit.Should().BeTrue();
    }

    [Fact]
    public async Task Launch_rejected_for_pending_module()
    {
        var module = SeedModule(approved: false);

        var result = await _sut.LaunchAsync(Request(module.Id, Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.UnsupportedReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Launch_rejected_for_rejected_module()
    {
        var module = new ModuleDefinition("Bad Module", ModuleSourceMode.Manual, cefrLevel: "B1", skill: "Vocabulary");
        module.Reject("not good enough", null);
        _db.ModuleDefinitions.Add(module);
        _db.SaveChanges();

        var result = await _sut.LaunchAsync(Request(module.Id, Guid.NewGuid()));

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Launch_rejected_for_pending_activity_definition()
    {
        var activity = SeedActivity(approved: false);
        var module = SeedModule(activity: activity);

        var result = await _sut.LaunchAsync(Request(module.Id, Guid.NewGuid()));

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Launch_rejected_for_unsupported_renderer_type()
    {
        var activity = SeedActivity(rendererType: ActivityRendererType.Custom, formSchemaJson: null);
        var module = SeedModule(activity: activity);

        var result = await _sut.LaunchAsync(Request(module.Id, Guid.NewGuid()));

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Launch_rejected_for_invalid_formio_schema()
    {
        var activity = SeedActivity(formSchemaJson: "{not valid json");
        var module = SeedModule(activity: activity);

        var result = await _sut.LaunchAsync(Request(module.Id, Guid.NewGuid()));

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Launch_rejected_for_unsupported_activity_type()
    {
        var activity = SeedActivity(activityType: "short_answer");
        var module = SeedModule(activity: activity);

        var result = await _sut.LaunchAsync(Request(module.Id, Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.UnsupportedReason.Should().Contain("not launchable yet");
    }

    [Fact]
    public async Task Launch_rejected_for_manual_or_ai_evaluated_activity()
    {
        var activity = SeedActivity(scoringRulesJson: ManualEvalScoring);
        var module = SeedModule(activity: activity);

        var result = await _sut.LaunchAsync(Request(module.Id, Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.UnsupportedReason.Should().Contain("manual or AI-assisted review");
    }

    [Fact]
    public async Task Launch_result_does_not_expose_answer_key()
    {
        var module = SeedModule();

        var result = await _sut.LaunchAsync(Request(module.Id, Guid.NewGuid()));

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        json.Should().NotContain("AnswerKeyJson");
        json.Should().NotContain("word_answer");
    }

    [Fact]
    public async Task Launch_result_does_not_expose_scoring_rules()
    {
        var module = SeedModule();

        var result = await _sut.LaunchAsync(Request(module.Id, Guid.NewGuid()));

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        json.Should().NotContain("ScoringRulesJson");
        json.Should().NotContain("correctAnswer");
    }

    [Fact]
    public async Task Launch_preserves_module_definition_traceability()
    {
        var module = SeedModule();
        var studentId = Guid.NewGuid();

        var result = await _sut.LaunchAsync(Request(module.Id, studentId));

        var bridge = await _db.StudentActivityDefinitionLaunches.SingleAsync();
        bridge.ModuleDefinitionId.Should().Be(module.Id);
        result.ModuleDefinitionId.Should().Be(module.Id);
    }

    [Fact]
    public async Task Launch_preserves_activity_definition_traceability()
    {
        var activity = SeedActivity();
        var module = SeedModule(activity: activity);
        var studentId = Guid.NewGuid();

        var result = await _sut.LaunchAsync(Request(module.Id, studentId));

        var bridge = await _db.StudentActivityDefinitionLaunches.SingleAsync();
        bridge.ActivityDefinitionId.Should().Be(activity.Id);
        result.ActivityDefinitionId.Should().Be(activity.Id);
    }

    [Fact]
    public async Task Launch_scopes_bridge_row_to_the_launching_student()
    {
        var module = SeedModule();
        var studentA = Guid.NewGuid();
        var studentB = Guid.NewGuid();

        await _sut.LaunchAsync(Request(module.Id, studentA));
        await _sut.LaunchAsync(Request(module.Id, studentB));

        var bridges = await _db.StudentActivityDefinitionLaunches.ToListAsync();
        bridges.Should().HaveCount(2);
        bridges.Should().Contain(b => b.StudentId == studentA);
        bridges.Should().Contain(b => b.StudentId == studentB);
    }

    [Fact]
    public async Task Launch_failure_does_not_mutate_module_definition()
    {
        var module = SeedModule(approved: false);
        var updatedAtBefore = module.UpdatedAtUtc;

        await _sut.LaunchAsync(Request(module.Id, Guid.NewGuid()));

        var reloaded = await _db.ModuleDefinitions.AsNoTracking().SingleAsync(m => m.Id == module.Id);
        reloaded.UpdatedAtUtc.Should().Be(updatedAtBefore);
    }

    [Fact]
    public async Task Launch_failure_does_not_mutate_activity_definition()
    {
        var activity = SeedActivity(approved: false);
        var module = SeedModule(activity: activity);
        var updatedAtBefore = activity.UpdatedAtUtc;

        await _sut.LaunchAsync(Request(module.Id, Guid.NewGuid()));

        var reloaded = await _db.ActivityDefinitions.AsNoTracking().SingleAsync(a => a.Id == activity.Id);
        reloaded.UpdatedAtUtc.Should().Be(updatedAtBefore);
    }
}
