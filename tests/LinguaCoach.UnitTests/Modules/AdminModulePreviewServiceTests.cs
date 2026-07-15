using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.Modules;
using LinguaCoach.Application.Placement;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Modules;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.Modules;

/// <summary>
/// Phase J3 — admin "preview as a learner" for a Module. Uses SQLite in-memory, matching sibling
/// generation-service test conventions. Builds a real Module via the existing deterministic
/// ModuleGenerationService (not hand-rolled links) so link wiring stays exactly what production
/// code produces.
/// </summary>
public sealed class AdminModulePreviewServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly AdminModulePreviewService _sut;
    private readonly ModuleGenerationService _moduleGen;

    public AdminModulePreviewServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new AdminModulePreviewService(_db);
        _moduleGen = new ModuleGenerationService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private Lesson SeedLesson()
    {
        var item = new Lesson(
            "Resilient", "Resilient means able to recover quickly.", LessonSourceMode.Manual,
            "B2", "Vocabulary", examplesJson: """["The team stayed resilient."]""");
        item.Approve(null);
        _db.Lessons.Add(item);
        _db.SaveChanges();
        return item;
    }

    private Exercise SeedGapFillExercise(Guid lessonId)
    {
        var formSchema = JsonSerializer.Serialize(new
        {
            components = new object[]
            {
                new { type = "content", key = "prompt", html = "<p>Type the missing word.</p>" },
                new { type = "textfield", key = "answer", label = "Your answer", input = true },
            }
        });
        var answerKey = JsonSerializer.Serialize(new Dictionary<string, string> { ["answer"] = "resilient" });
        var scoringRules = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.TextNormalized, CorrectAnswer: "resilient", Points: 1.0) }));
        var feedbackPlan = JsonSerializer.Serialize(new { correctFeedback = "Correct!", incorrectFeedback = "Not quite — the answer was \"resilient\"." });

        var activity = new Exercise(
            "Gap fill: resilient", "Type the missing word.", "gap_fill", ExerciseRendererType.Formio,
            ExerciseSourceMode.Manual, formSchemaJson: formSchema, answerKeyJson: answerKey,
            scoringRulesJson: scoringRules, feedbackPlanJson: feedbackPlan, cefrLevel: "B2", skill: "Vocabulary",
            estimatedMinutes: 3, lessonId: lessonId);
        activity.Approve(null);
        _db.Exercises.Add(activity);
        _db.SaveChanges();
        return activity;
    }

    private Exercise SeedShortAnswerExercise(Guid lessonId)
    {
        var scoringRules = JsonSerializer.Serialize(new ScoringRulesDocument(
            new Dictionary<string, ComponentScoringRule> { ["answer"] = new(ScoringRuleKinds.TextNormalized, RequiresManualOrAiEvaluation: true) }));
        var activity = new Exercise(
            "Comprehension question", "Answer the question.", "short_answer", ExerciseRendererType.Formio,
            ExerciseSourceMode.Manual, formSchemaJson: """{"components":[{"type":"textarea","key":"answer"}]}""",
            scoringRulesJson: scoringRules, cefrLevel: "B1", skill: "Reading", lessonId: lessonId);
        activity.Approve(null);
        _db.Exercises.Add(activity);
        _db.SaveChanges();
        return activity;
    }

    private async Task<Guid> CreatePendingModuleAsync(Lesson lesson, Exercise exercise)
    {
        var result = await _moduleGen.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") },
            new[] { new ModuleExerciseLinkInput(exercise.Id, "PrimaryPractice") }));
        return result.Module.Id;
    }

    // ── Preview query ────────────────────────────────────────────────────────

    [Fact]
    public async Task Preview_works_for_a_pending_review_module_not_just_approved()
    {
        var lesson = SeedLesson();
        var activity = SeedGapFillExercise(lesson.Id);
        var moduleId = await CreatePendingModuleAsync(lesson, activity);

        var result = await _sut.HandleAsync(moduleId);

        result.Should().NotBeNull();
        result!.ModuleReviewStatus.Should().Be("PendingReview");
        result.Lesson.Should().NotBeNull();
        result.Lesson!.Title.Should().Be("Resilient");
        result.Lesson.Examples.Should().ContainSingle().Which.Should().Contain("resilient");
        result.Exercise.Should().NotBeNull();
        result.Exercise!.CanScore.Should().BeTrue();
        result.Exercise.UnscorableReason.Should().BeNull();
    }

    [Fact]
    public async Task Preview_never_exposes_answer_key_or_scoring_rules()
    {
        var lesson = SeedLesson();
        var activity = SeedGapFillExercise(lesson.Id);
        var moduleId = await CreatePendingModuleAsync(lesson, activity);

        var result = await _sut.HandleAsync(moduleId);

        // ModulePreviewExerciseDto has no AnswerKeyJson/ScoringRulesJson property at all — this
        // assertion documents the intent; the compiler itself enforces it (DTO shape has no field
        // to leak through). Confirm the schema shown is the student-safe one.
        result!.Exercise!.FormSchemaJson.Should().NotContain("resilient");
    }

    [Fact]
    public async Task Preview_of_short_answer_exercise_reports_not_scorable_with_a_reason()
    {
        var lesson = SeedLesson();
        var activity = SeedShortAnswerExercise(lesson.Id);
        var moduleId = await CreatePendingModuleAsync(lesson, activity);

        var result = await _sut.HandleAsync(moduleId);

        result!.Exercise!.CanScore.Should().BeFalse();
        result.Exercise.UnscorableReason.Should().Contain("not launchable yet");
    }

    [Fact]
    public async Task Preview_of_nonexistent_module_returns_null()
    {
        var result = await _sut.HandleAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    // ── Preview submit / scoring ────────────────────────────────────────────

    [Fact]
    public async Task Submitting_the_correct_answer_scores_100_percent_with_correct_feedback()
    {
        var lesson = SeedLesson();
        var activity = SeedGapFillExercise(lesson.Id);
        var moduleId = await CreatePendingModuleAsync(lesson, activity);

        var answers = new Dictionary<string, JsonElement>
        {
            ["answer"] = JsonSerializer.SerializeToElement("resilient"),
        };

        var result = await _sut.HandleAsync(new ModulePreviewSubmitRequest(moduleId, answers));

        result.Scored.Should().BeTrue();
        result.AllCorrect.Should().BeTrue();
        result.ScorePercent.Should().Be(100.0);
        result.FeedbackMessage.Should().Be("Correct!");
        result.Components.Should().ContainSingle(c => c.ComponentKey == "answer" && c.IsCorrect);
    }

    [Fact]
    public async Task Submitting_a_wrong_answer_scores_0_percent_with_incorrect_feedback()
    {
        var lesson = SeedLesson();
        var activity = SeedGapFillExercise(lesson.Id);
        var moduleId = await CreatePendingModuleAsync(lesson, activity);

        var answers = new Dictionary<string, JsonElement>
        {
            ["answer"] = JsonSerializer.SerializeToElement("determined"),
        };

        var result = await _sut.HandleAsync(new ModulePreviewSubmitRequest(moduleId, answers));

        result.Scored.Should().BeTrue();
        result.AllCorrect.Should().BeFalse();
        result.ScorePercent.Should().Be(0.0);
        result.FeedbackMessage.Should().Contain("resilient");
    }

    [Fact]
    public async Task Submitting_against_an_unscorable_exercise_returns_scored_false_with_reason()
    {
        var lesson = SeedLesson();
        var activity = SeedShortAnswerExercise(lesson.Id);
        var moduleId = await CreatePendingModuleAsync(lesson, activity);

        var result = await _sut.HandleAsync(new ModulePreviewSubmitRequest(moduleId, new Dictionary<string, JsonElement>()));

        result.Scored.Should().BeFalse();
        result.UnscorableReason.Should().Contain("not launchable yet");
        result.ScorePercent.Should().BeNull();
    }

    [Fact]
    public async Task Preview_submit_never_creates_a_learning_activity_or_attempt()
    {
        var lesson = SeedLesson();
        var activity = SeedGapFillExercise(lesson.Id);
        var moduleId = await CreatePendingModuleAsync(lesson, activity);

        await _sut.HandleAsync(new ModulePreviewSubmitRequest(
            moduleId, new Dictionary<string, JsonElement> { ["answer"] = JsonSerializer.SerializeToElement("resilient") }));

        (await _db.LearningActivities.CountAsync()).Should().Be(0);
        (await _db.ActivityAttempts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Submit_against_nonexistent_module_throws()
    {
        var act = async () => await _sut.HandleAsync(
            new ModulePreviewSubmitRequest(Guid.NewGuid(), new Dictionary<string, JsonElement>()));

        await act.Should().ThrowAsync<ModuleValidationException>();
    }
}
