using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Lessons;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.UsageGovernance;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Lessons;

/// <summary>
/// Phase J2a — AI-assisted "Generate Learn" composer. Uses SQLite in-memory plus the
/// SwappableFakeAiProvider/FakeAiProviderResolver/NeverCalledUsageQuotaService fakes already
/// defined internal to LinguaCoach.UnitTests.ResourceImport (same assembly, so reusable here) —
/// never calls a real AI provider.
/// </summary>
public sealed class AiLessonGenerationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly LinguaCoach.UnitTests.ResourceImport.SwappableFakeAiProvider _provider = new();
    private readonly AiLessonGenerationService _sut;

    public AiLessonGenerationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _db.AiPrompts.Add(new AiPrompt(
            AiLessonGenerationService.GeneratePromptKey,
            "Teach: {{resourcesSummary}} {{cefrLevel}} {{skill}} {{subskill}} {{contextTags}} {{focusTags}} {{notes}}"));
        _db.SaveChanges();

        var aiExecution = new AiExecutionService(
            _db, new LinguaCoach.UnitTests.ResourceImport.FakeAiProviderResolver(_provider),
            new LinguaCoach.UnitTests.ResourceImport.NeverCalledUsageQuotaService(),
            NullLogger<AiExecutionService>.Instance);

        _sut = new AiLessonGenerationService(
            _db, new DbPromptAiContextBuilder(_db), aiExecution, NullLogger<AiLessonGenerationService>.Instance);
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

    private ResourceBankItem SeedVocabulary(Guid sourceId)
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Vocabulary, sourceId, "B2",
            ResourceBankItemContent.Serialize(new VocabularyContent("resilient", "adjective", "able to recover quickly")));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    private const string ValidAiJson = """
        {
          "title": "Understanding 'resilient'",
          "body": "Resilient describes someone or something that recovers quickly from difficulty.\n\nIt is often used to praise people at work.",
          "examples": ["The team stayed resilient after the setback.", "She is known for her resilient attitude."],
          "commonMistakes": ["Confusing 'resilient' with 'resistant'."],
          "usageNotes": "Common in workplace performance reviews."
        }
        """;

    [Fact]
    public async Task Valid_ai_response_creates_pending_review_lesson_with_ai_provider_attribution()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        _provider.NextResponses.Enqueue(ValidAiJson);

        var result = await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        result.Lesson.ReviewStatus.Should().Be("PendingReview");
        result.Lesson.Title.Should().Be("Understanding 'resilient'");
        result.Lesson.Body.Should().Contain("recovers quickly");
        result.Lesson.GenerationProvider.Should().Be("fake-provider");
        result.Lesson.GenerationModel.Should().Be("fake-model");
        result.Lesson.UsageNotes.Should().Contain("AI draft").And.Contain("fake-provider/fake-model");
        _provider.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Bad_json_on_first_attempt_retries_once_and_succeeds_on_valid_second_response()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        _provider.NextResponses.Enqueue("not json at all");
        _provider.NextResponses.Enqueue(ValidAiJson);

        var result = await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        result.Lesson.Title.Should().Be("Understanding 'resilient'");
        _provider.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Bad_json_on_both_attempts_throws_lesson_validation_exception()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        _provider.NextResponses.Enqueue("not json at all");
        _provider.NextResponses.Enqueue("still not json");

        var act = async () => await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        (await act.Should().ThrowAsync<LessonValidationException>())
            .WithMessage("*could not be parsed*deterministic Generate action instead*");
        _provider.CallCount.Should().Be(2);

        (await _db.Lessons.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Missing_title_or_body_is_treated_as_unparseable_and_retried()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        _provider.NextResponses.Enqueue("""{"body": "some body but no title"}""");
        _provider.NextResponses.Enqueue(ValidAiJson);

        var result = await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        result.Lesson.Title.Should().Be("Understanding 'resilient'");
        _provider.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Ai_provider_unavailable_throws_lesson_validation_exception_no_lesson_created()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        _provider.ThrowUnavailable = true;

        var act = async () => await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        (await act.Should().ThrowAsync<LessonValidationException>())
            .WithMessage("*AI generation is currently unavailable*deterministic Generate action instead*");

        (await _db.Lessons.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task No_resources_throws_before_any_ai_call()
    {
        var act = async () => await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            Array.Empty<LessonResourceLinkInput>()));

        (await act.Should().ThrowAsync<LessonValidationException>())
            .WithMessage("*At least one resource is required*");
        _provider.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Unknown_resource_throws_before_any_ai_call()
    {
        var act = async () => await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", Guid.NewGuid(), "Primary") }));

        (await act.Should().ThrowAsync<LessonValidationException>())
            .WithMessage("*was not found in the published Resource Bank*");
        _provider.CallCount.Should().Be(0);
    }
}
