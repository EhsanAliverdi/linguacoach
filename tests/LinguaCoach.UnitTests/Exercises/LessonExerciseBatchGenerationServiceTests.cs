using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Exercises;
using LinguaCoach.Infrastructure.Modules;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Exercises;

/// <summary>
/// Phase 1 (2026-07-15 pipeline safety audit) — Lesson-based batch Exercise generation. Covers
/// the confirmed root cause: AI-preferred activity types (multiple_choice_single,
/// reading_multiple_choice_single/multi, listening_multiple_choice_single/multi) were routed
/// through the AI resources-based handler with lessonId hardcoded to null, so a batch mixing
/// deterministic and AI-preferred types could silently produce Exercises with
/// <c>Exercise.LessonId == null</c> even though the whole call was Lesson-scoped. Uses SQLite
/// in-memory plus the same SwappableFakeAiProvider fakes AiExerciseGenerationServiceTests uses —
/// never calls a real AI provider.
/// </summary>
public sealed class LessonExerciseBatchGenerationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly LinguaCoach.UnitTests.ResourceImport.SwappableFakeAiProvider _provider = new();
    private readonly LessonExerciseBatchGenerationService _sut;

    public LessonExerciseBatchGenerationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _db.AiPrompts.Add(new AiPrompt(
            AiExerciseGenerationService.GeneratePromptKey,
            "Write: {{activityType}} {{resourceTitle}} {{resourceDefinition}} {{resourceType}} {{cefrLevel}} {{skill}} {{notes}}"));
        _db.SaveChanges();

        var aiExecution = new AiExecutionService(
            _db, new LinguaCoach.UnitTests.ResourceImport.FakeAiProviderResolver(_provider),
            new LinguaCoach.UnitTests.ResourceImport.NeverCalledUsageQuotaService(),
            NullLogger<AiExecutionService>.Instance);

        var singleHandler = new ActivityGenerationService(_db, new FormIoSchemaValidationService());
        var aiResourcesHandler = new AiExerciseGenerationService(
            _db, new FormIoSchemaValidationService(), new DbPromptAiContextBuilder(_db), aiExecution,
            NullLogger<AiExerciseGenerationService>.Instance);
        var moduleAutoLink = new ModuleAutoLinkService(_db);

        _sut = new LessonExerciseBatchGenerationService(singleHandler, aiResourcesHandler, moduleAutoLink, _db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private CefrResourceSource SeedSource()
    {
        var source = new CefrResourceSource("Test Source", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        _db.CefrResourceSources.Add(source);
        _db.SaveChanges();
        return source;
    }

    private Lesson SeedLessonWithVocabularyLink(out ResourceBankItem vocab)
    {
        var source = SeedSource();
        vocab = new ResourceBankItem(
            PublishedResourceType.Vocabulary, source.Id, "B2",
            ResourceBankItemContent.Serialize(new VocabularyContent("resilient", "adjective", "able to recover quickly")));
        _db.ResourceBankItems.Add(vocab);
        _db.SaveChanges();

        var lesson = new Lesson("Resilient", "Resilient means able to recover quickly.",
            LessonSourceMode.Manual, "B1", "Vocabulary");
        _db.Lessons.Add(lesson);
        _db.SaveChanges();

        _db.LessonResourceLinks.Add(new LessonResourceLink(
            lesson.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        _db.SaveChanges();

        return lesson;
    }

    [Fact]
    public async Task Deterministic_type_generated_from_lesson_retains_lesson_id()
    {
        var lesson = SeedLessonWithVocabularyLink(out _);

        var result = await _sut.HandleAsync(new GenerateActivitiesFromLessonRequest(
            lesson.Id, new[] { new ActivityGenerationSpec("gap_fill", 1) }));

        result.Activities.Should().ContainSingle().Which.LessonId.Should().Be(lesson.Id);
    }

    [Fact]
    public async Task Ai_preferred_type_generated_from_lesson_retains_lesson_id()
    {
        var lesson = SeedLessonWithVocabularyLink(out _);
        _provider.NextResponses.Enqueue(
            """{"promptText": "", "distractors": ["easily discouraged by setbacks", "unable to adapt to change", "overly cautious in decisions"]}""");

        var result = await _sut.HandleAsync(new GenerateActivitiesFromLessonRequest(
            lesson.Id, new[] { new ActivityGenerationSpec("multiple_choice_single", 1) }));

        result.Activities.Should().ContainSingle().Which.LessonId.Should().Be(lesson.Id);
        var persisted = await _db.Exercises.AsNoTracking().SingleAsync();
        persisted.LessonId.Should().Be(lesson.Id);
    }

    [Fact]
    public async Task Batch_mixing_deterministic_and_ai_preferred_types_all_retain_lesson_id()
    {
        var lesson = SeedLessonWithVocabularyLink(out _);
        _provider.NextResponses.Enqueue(
            """{"promptText": "", "distractors": ["easily discouraged by setbacks", "unable to adapt to change", "overly cautious in decisions"]}""");

        var result = await _sut.HandleAsync(new GenerateActivitiesFromLessonRequest(
            lesson.Id,
            new[]
            {
                new ActivityGenerationSpec("gap_fill", 1),
                new ActivityGenerationSpec("multiple_choice_single", 1),
            }));

        result.Activities.Should().HaveCount(2);
        result.Activities.Should().OnlyContain(a => a.LessonId == lesson.Id);

        var persisted = await _db.Exercises.AsNoTracking().ToListAsync();
        persisted.Should().OnlyContain(e => e.LessonId == lesson.Id);
    }

    [Fact]
    public async Task Exercise_retrieval_by_lesson_returns_all_exercises_from_batch()
    {
        var lesson = SeedLessonWithVocabularyLink(out _);
        _provider.NextResponses.Enqueue(
            """{"promptText": "", "distractors": ["easily discouraged by setbacks", "unable to adapt to change", "overly cautious in decisions"]}""");

        await _sut.HandleAsync(new GenerateActivitiesFromLessonRequest(
            lesson.Id,
            new[]
            {
                new ActivityGenerationSpec("gap_fill", 1),
                new ActivityGenerationSpec("multiple_choice_single", 1),
            }));

        var byLesson = await _db.Exercises.AsNoTracking().Where(e => e.LessonId == lesson.Id).ToListAsync();
        byLesson.Should().HaveCount(2);
    }

    [Fact]
    public async Task Batch_result_reports_module_auto_link_for_ai_preferred_exercise()
    {
        var lesson = SeedLessonWithVocabularyLink(out _);
        _provider.NextResponses.Enqueue(
            """{"promptText": "", "distractors": ["easily discouraged by setbacks", "unable to adapt to change", "overly cautious in decisions"]}""");

        var result = await _sut.HandleAsync(new GenerateActivitiesFromLessonRequest(
            lesson.Id, new[] { new ActivityGenerationSpec("multiple_choice_single", 1) }));

        result.ModuleId.Should().NotBeEmpty();
        var link = await _db.ModuleExerciseLinks.AsNoTracking()
            .SingleAsync(l => l.ModuleId == result.ModuleId && l.ExerciseId == result.Activities[0].Id);
        link.Should().NotBeNull();
    }
}
