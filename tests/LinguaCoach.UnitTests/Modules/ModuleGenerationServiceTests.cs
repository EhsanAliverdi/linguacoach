using FluentAssertions;
using LinguaCoach.Application.Modules;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Modules;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.Modules;

/// <summary>
/// Phase H5 — deterministic "Generate Module" composer (all four entry points). Uses SQLite
/// in-memory (matches Lesson/Exercise generation test conventions) with
/// directly-seeded Lessons/Exercises/published bank rows. All fixture content is
/// synthetic.
/// </summary>
public sealed class ModuleGenerationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ModuleGenerationService _sut;

    public ModuleGenerationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ModuleGenerationService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private Lesson SeedLesson(bool approved, string title = "Resilient", string cefrLevel = "B2", string skill = "Vocabulary")
    {
        var item = new Lesson(title, $"{title} means able to recover quickly.", LessonSourceMode.Manual, cefrLevel, skill);
        if (approved) item.Approve(null);
        _db.Lessons.Add(item);
        _db.SaveChanges();
        return item;
    }

    private Exercise SeedActivity(bool approved, string title = "Gap fill: resilient", string cefrLevel = "B2", string skill = "Vocabulary")
    {
        var activity = new Exercise(title, "Type the missing word.", "gap_fill", ExerciseRendererType.Formio,
            ExerciseSourceMode.Manual, cefrLevel: cefrLevel, skill: skill, estimatedMinutes: 5);
        if (approved) activity.Approve(null);
        _db.Exercises.Add(activity);
        _db.SaveChanges();
        return activity;
    }

    private (CefrResourceSource Source, ResourceBankItem Vocab) SeedVocabularyResource()
    {
        var source = new CefrResourceSource("Test Source", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        _db.CefrResourceSources.Add(source);
        var vocab = new ResourceBankItem(
            PublishedResourceType.Vocabulary, source.Id, "B2",
            ResourceBankItemContent.Serialize(new VocabularyContent("resilient", "adjective", "able to recover quickly")));
        _db.ResourceBankItems.Add(vocab);
        _db.SaveChanges();
        return (source, vocab);
    }

    // ── Generate from items ──────────────────────────────────────────────────

    [Fact]
    public async Task Generate_from_items_creates_pending_review_module_not_approved()
    {
        var lesson = SeedLesson(approved: true);
        var activity = SeedActivity(approved: true);

        var result = await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") },
            new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") }));

        result.Module.ReviewStatus.Should().Be("PendingReview");
        result.Module.SourceMode.Should().Be("GeneratedFromLessonAndExercises");
        result.Module.GenerationProvider.Should().Be("Deterministic");
        (await _db.Modules.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Generate_stores_cefr_skill_subskill_context_focus_difficulty_metadata()
    {
        var lesson = SeedLesson(approved: true, cefrLevel: "B1", skill: "Grammar");
        var activity = SeedActivity(approved: true, cefrLevel: "B1", skill: "Grammar");

        var result = await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") },
            new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") }));

        result.Module.CefrLevel.Should().Be("B1");
        result.Module.Skill.Should().Be("Grammar");
    }

    [Fact]
    public async Task Generate_stores_module_feedback_plan_json()
    {
        var lesson = SeedLesson(approved: true);
        var activity = SeedActivity(approved: true);

        var result = await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") },
            new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") }));

        result.Module.FeedbackPlanJson.Should().Contain("completionMessage");
    }

    [Fact]
    public async Task Generate_creates_lesson_links()
    {
        var lesson = SeedLesson(approved: true);
        var activity = SeedActivity(approved: true);

        var result = await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") },
            new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") }));

        result.Module.LessonLinks.Should().ContainSingle(l => l.LessonId == lesson.Id && l.Role == "Primary");
    }

    [Fact]
    public async Task Generate_creates_activity_links()
    {
        var lesson = SeedLesson(approved: true);
        var activity = SeedActivity(approved: true);

        var result = await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") },
            new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") }));

        result.Module.ExerciseLinks.Should().ContainSingle(l => l.ExerciseId == activity.Id && l.Role == "PrimaryPractice");
    }

    [Fact]
    public async Task Generate_preserves_sort_order_across_multiple_links()
    {
        var lesson1 = SeedLesson(approved: true, title: "First");
        var lesson2 = SeedLesson(approved: true, title: "Second");
        var activity = SeedActivity(approved: true);

        var result = await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[]
            {
                new ModuleLessonLinkInput(lesson1.Id, "Primary"),
                new ModuleLessonLinkInput(lesson2.Id, "Supporting"),
            },
            new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") }));

        result.Module.LessonLinks.Should().HaveCount(2);
        result.Module.LessonLinks[0].LessonId.Should().Be(lesson1.Id);
        result.Module.LessonLinks[0].SortOrder.Should().Be(0);
        result.Module.LessonLinks[1].LessonId.Should().Be(lesson2.Id);
        result.Module.LessonLinks[1].SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task Generate_from_items_rejects_a_pending_lesson()
    {
        var lesson = SeedLesson(approved: false);
        var activity = SeedActivity(approved: true);

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") },
            new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") }));

        await act.Should().ThrowAsync<ModuleValidationException>();
        (await _db.Modules.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Generate_from_items_rejects_a_pending_activity()
    {
        var lesson = SeedLesson(approved: true);
        var activity = SeedActivity(approved: false);

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") },
            new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") }));

        await act.Should().ThrowAsync<ModuleValidationException>();
        (await _db.Modules.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Generate_from_items_rejects_an_invalid_lesson_id()
    {
        var activity = SeedActivity(approved: true);

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLessonLinkInput(Guid.NewGuid(), "Primary") },
            new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") }));

        await act.Should().ThrowAsync<ModuleValidationException>();
    }

    // ── Generate from resource ───────────────────────────────────────────────

    [Fact]
    public async Task Generate_from_resource_composes_module_when_approved_lesson_and_activity_exist()
    {
        var (_, vocab) = SeedVocabularyResource();
        var lesson = SeedLesson(approved: true);
        var activity = SeedActivity(approved: true);
        _db.LessonResourceLinks.Add(new LessonResourceLink(lesson.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        _db.ExerciseResourceLinks.Add(new ExerciseResourceLink(activity.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        await _db.SaveChangesAsync();

        var result = await _sut.HandleAsync(new GenerateModuleFromResourceRequest("Vocabulary", vocab.Id));

        result.Module.SourceMode.Should().Be("GeneratedFromResources");
        result.Module.LessonLinks.Should().ContainSingle(l => l.LessonId == lesson.Id);
        result.Module.ExerciseLinks.Should().ContainSingle(l => l.ExerciseId == activity.Id);
    }

    [Fact]
    public async Task Generate_from_resource_rejected_when_no_approved_lesson_linked()
    {
        var (_, vocab) = SeedVocabularyResource();
        var activity = SeedActivity(approved: true);
        _db.ExerciseResourceLinks.Add(new ExerciseResourceLink(activity.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        await _db.SaveChangesAsync();

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromResourceRequest("Vocabulary", vocab.Id));

        await act.Should().ThrowAsync<ModuleValidationException>();
    }

    [Fact]
    public async Task Generate_from_resource_rejected_when_no_approved_activity_linked()
    {
        var (_, vocab) = SeedVocabularyResource();
        var lesson = SeedLesson(approved: true);
        _db.LessonResourceLinks.Add(new LessonResourceLink(lesson.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        await _db.SaveChangesAsync();

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromResourceRequest("Vocabulary", vocab.Id));

        await act.Should().ThrowAsync<ModuleValidationException>();
    }

    // ── Generate from Lesson / Activity ──────────────────────────────────

    [Fact]
    public async Task Generate_from_lesson_finds_a_compatible_approved_activity()
    {
        var lesson = SeedLesson(approved: true, cefrLevel: "B1", skill: "Grammar");
        var activity = SeedActivity(approved: true, cefrLevel: "B1", skill: "Grammar");

        var result = await _sut.HandleAsync(new GenerateModuleFromLessonRequest(lesson.Id));

        result.Module.ExerciseLinks.Should().ContainSingle(l => l.ExerciseId == activity.Id);
    }

    [Fact]
    public async Task Generate_from_lesson_rejected_when_no_compatible_activity_exists()
    {
        var lesson = SeedLesson(approved: true, cefrLevel: "C1", skill: "Grammar");
        SeedActivity(approved: true, cefrLevel: "A1", skill: "Vocabulary");

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromLessonRequest(lesson.Id));

        await act.Should().ThrowAsync<ModuleValidationException>();
    }

    [Fact]
    public async Task Generate_from_activity_finds_a_compatible_approved_lesson()
    {
        var lesson = SeedLesson(approved: true, cefrLevel: "B1", skill: "Grammar");
        var activity = SeedActivity(approved: true, cefrLevel: "B1", skill: "Grammar");

        var result = await _sut.HandleAsync(new GenerateModuleFromExerciseRequest(activity.Id));

        result.Module.LessonLinks.Should().ContainSingle(l => l.LessonId == lesson.Id);
    }

    [Fact]
    public async Task Generate_from_activity_rejected_when_no_compatible_lesson_exists()
    {
        SeedLesson(approved: true, cefrLevel: "A1", skill: "Vocabulary");
        var activity = SeedActivity(approved: true, cefrLevel: "C1", skill: "Grammar");

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromExerciseRequest(activity.Id));

        await act.Should().ThrowAsync<ModuleValidationException>();
    }

    [Fact]
    public async Task Generate_from_lesson_that_is_not_approved_is_rejected()
    {
        var lesson = SeedLesson(approved: false);
        SeedActivity(approved: true, cefrLevel: lesson.CefrLevel!, skill: lesson.Skill!);

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromLessonRequest(lesson.Id));

        await act.Should().ThrowAsync<ModuleValidationException>();
    }

    // ── No mutation / no side effects ────────────────────────────────────────

    [Fact]
    public async Task Generate_does_not_mutate_the_source_lesson()
    {
        var lesson = SeedLesson(approved: true);
        var activity = SeedActivity(approved: true);
        var originalTitle = lesson.Title;
        var originalStatus = lesson.ReviewStatus;

        await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") },
            new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") }));

        var reloaded = await _db.Lessons.FirstAsync(l => l.Id == lesson.Id);
        reloaded.Title.Should().Be(originalTitle);
        reloaded.ReviewStatus.Should().Be(originalStatus);
    }

    [Fact]
    public async Task Generate_does_not_mutate_the_source_exercise()
    {
        var lesson = SeedLesson(approved: true);
        var activity = SeedActivity(approved: true);
        var originalTitle = activity.Title;

        await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") },
            new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") }));

        var reloaded = await _db.Exercises.FirstAsync(a => a.Id == activity.Id);
        reloaded.Title.Should().Be(originalTitle);
    }

    [Fact]
    public async Task Generate_creates_no_student_assignment_or_today_practice_gym_records()
    {
        var lesson = SeedLesson(approved: true);
        var activity = SeedActivity(approved: true);

        await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") },
            new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") }));

        (await _db.StudentProfiles.CountAsync()).Should().Be(0);
        (await _db.LearningActivities.CountAsync()).Should().Be(0);
        (await _db.LearningModules.CountAsync()).Should().Be(0);
    }
}
