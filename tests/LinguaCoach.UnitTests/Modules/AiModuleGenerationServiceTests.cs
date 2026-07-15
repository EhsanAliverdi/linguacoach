using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Modules;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Modules;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Modules;

/// <summary>
/// Phase J2c — AI-assisted "Generate Module" composer ("from resource" entry point only). Uses
/// SQLite in-memory plus the SwappableFakeAiProvider/FakeAiProviderResolver/
/// NeverCalledUsageQuotaService fakes already defined internal to
/// LinguaCoach.UnitTests.ResourceImport (same assembly, so reusable here) — never calls a real AI
/// provider.
/// </summary>
public sealed class AiModuleGenerationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly LinguaCoach.UnitTests.ResourceImport.SwappableFakeAiProvider _provider = new();
    private readonly AiModuleGenerationService _sut;

    public AiModuleGenerationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _db.AiPrompts.Add(new AiPrompt(
            AiModuleGenerationService.GeneratePromptKey,
            "Write: {{lessonTitle}} {{lessonBody}} {{exerciseTitle}} {{exerciseInstructions}} {{activityType}} {{cefrLevel}} {{skill}} {{notes}}"));
        _db.SaveChanges();

        var aiExecution = new AiExecutionService(
            _db, new LinguaCoach.UnitTests.ResourceImport.FakeAiProviderResolver(_provider),
            new LinguaCoach.UnitTests.ResourceImport.NeverCalledUsageQuotaService(),
            NullLogger<AiExecutionService>.Instance);

        _sut = new AiModuleGenerationService(
            _db, new DbPromptAiContextBuilder(_db), aiExecution, NullLogger<AiModuleGenerationService>.Instance);
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

    private Exercise SeedActivity(bool approved, string title = "Gap fill: resilient", string cefrLevel = "B2", string skill = "Vocabulary", Guid? lessonId = null)
    {
        var activity = new Exercise(title, "Type the missing word.", "gap_fill", ExerciseRendererType.Formio,
            ExerciseSourceMode.Manual, cefrLevel: cefrLevel, skill: skill, estimatedMinutes: 5,
            lessonId: lessonId ?? SeedLesson(true, title, cefrLevel, skill).Id);
        if (approved) activity.Approve(null);
        _db.Exercises.Add(activity);
        _db.SaveChanges();
        return activity;
    }

    private ResourceBankItem SeedVocabularyResource()
    {
        var source = new CefrResourceSource("Test Source", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        _db.CefrResourceSources.Add(source);
        var vocab = new ResourceBankItem(
            PublishedResourceType.Vocabulary, source.Id, "B2",
            ResourceBankItemContent.Serialize(new VocabularyContent("resilient", "adjective", "able to recover quickly")));
        _db.ResourceBankItems.Add(vocab);
        _db.SaveChanges();
        return vocab;
    }

    private const string ValidAiJson = """
        {
          "title": "Understanding and Using 'Resilient'",
          "description": "Learn what resilient means and practice using it in a workplace sentence.",
          "feedbackPlan": {
            "completionMessage": "Great work — you can now use 'resilient' with confidence!",
            "evaluationCriteria": ["Correct word usage", "Spelling accuracy"],
            "feedbackFocus": "Focus on whether the student recognizes the meaning in context."
          }
        }
        """;

    [Fact]
    public async Task Valid_ai_response_creates_pending_review_module_linking_the_same_lesson_and_exercise()
    {
        var lesson = SeedLesson(approved: true);
        var activity = SeedActivity(approved: true);
        var vocab = SeedVocabularyResource();
        _db.LessonResourceLinks.Add(new LessonResourceLink(lesson.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        _db.ExerciseResourceLinks.Add(new ExerciseResourceLink(activity.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        await _db.SaveChangesAsync();
        _provider.NextResponses.Enqueue(ValidAiJson);

        var result = await _sut.HandleAsync(new GenerateModuleFromResourceRequest("Vocabulary", vocab.Id));

        result.Module.ReviewStatus.Should().Be("PendingReview");
        result.Module.SourceMode.Should().Be("GeneratedFromResources");
        result.Module.Title.Should().Be("Understanding and Using 'Resilient'");
        result.Module.GenerationProvider.Should().Be("fake-provider");
        result.Module.LessonLinks.Should().ContainSingle(l => l.LessonId == lesson.Id);
        result.Module.ExerciseLinks.Should().ContainSingle(l => l.ExerciseId == activity.Id);
        result.Module.FeedbackPlanJson.Should().Contain("Great work").And.Contain("Correct word usage");
        _provider.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Bad_json_retries_once_and_succeeds_on_valid_second_response()
    {
        var lesson = SeedLesson(approved: true);
        var activity = SeedActivity(approved: true);
        var vocab = SeedVocabularyResource();
        _db.LessonResourceLinks.Add(new LessonResourceLink(lesson.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        _db.ExerciseResourceLinks.Add(new ExerciseResourceLink(activity.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        await _db.SaveChangesAsync();
        _provider.NextResponses.Enqueue("not json");
        _provider.NextResponses.Enqueue(ValidAiJson);

        var result = await _sut.HandleAsync(new GenerateModuleFromResourceRequest("Vocabulary", vocab.Id));

        result.Module.Title.Should().Be("Understanding and Using 'Resilient'");
        _provider.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Missing_completion_message_is_treated_as_unparseable_and_throws_after_retry()
    {
        var lesson = SeedLesson(approved: true);
        var activity = SeedActivity(approved: true);
        var vocab = SeedVocabularyResource();
        _db.LessonResourceLinks.Add(new LessonResourceLink(lesson.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        _db.ExerciseResourceLinks.Add(new ExerciseResourceLink(activity.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        await _db.SaveChangesAsync();
        _provider.NextResponses.Enqueue("""{"title": "X", "description": "Y", "feedbackPlan": {}}""");
        _provider.NextResponses.Enqueue("""{"title": "X", "description": "Y", "feedbackPlan": {}}""");

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromResourceRequest("Vocabulary", vocab.Id));

        (await act.Should().ThrowAsync<ModuleValidationException>())
            .WithMessage("*could not be parsed*deterministic Generate action instead*");
        _provider.CallCount.Should().Be(2);
        (await _db.Modules.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task No_approved_lesson_linked_throws_before_any_ai_call()
    {
        var activity = SeedActivity(approved: true);
        var vocab = SeedVocabularyResource();
        _db.ExerciseResourceLinks.Add(new ExerciseResourceLink(activity.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        await _db.SaveChangesAsync();

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromResourceRequest("Vocabulary", vocab.Id));

        (await act.Should().ThrowAsync<ModuleValidationException>())
            .WithMessage("*No approved Lesson*");
        _provider.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task No_approved_exercise_linked_throws_before_any_ai_call()
    {
        var lesson = SeedLesson(approved: true);
        var vocab = SeedVocabularyResource();
        _db.LessonResourceLinks.Add(new LessonResourceLink(lesson.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        await _db.SaveChangesAsync();

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromResourceRequest("Vocabulary", vocab.Id));

        (await act.Should().ThrowAsync<ModuleValidationException>())
            .WithMessage("*No approved Exercise*");
        _provider.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Ai_provider_unavailable_throws_module_validation_exception_no_module_created()
    {
        var lesson = SeedLesson(approved: true);
        var activity = SeedActivity(approved: true);
        var vocab = SeedVocabularyResource();
        _db.LessonResourceLinks.Add(new LessonResourceLink(lesson.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        _db.ExerciseResourceLinks.Add(new ExerciseResourceLink(activity.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        await _db.SaveChangesAsync();
        _provider.ThrowUnavailable = true;

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromResourceRequest("Vocabulary", vocab.Id));

        (await act.Should().ThrowAsync<ModuleValidationException>())
            .WithMessage("*AI generation is currently unavailable*deterministic Generate action instead*");
        (await _db.Modules.CountAsync()).Should().Be(0);
    }
}
