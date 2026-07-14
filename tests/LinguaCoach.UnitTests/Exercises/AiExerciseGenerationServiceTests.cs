using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Exercises;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Exercises;

/// <summary>
/// Phase J2b — AI-assisted "Generate Activity" composer ("from resources" entry point only).
/// Uses SQLite in-memory plus the SwappableFakeAiProvider/FakeAiProviderResolver/
/// NeverCalledUsageQuotaService fakes already defined internal to
/// LinguaCoach.UnitTests.ResourceImport (same assembly, so reusable here) — never calls a real AI
/// provider. Special attention to the safety checks that don't exist for Lesson generation:
/// gap_fill answer-leak detection, and multiple_choice_single's correct-answer/scoring never being
/// AI-supplied.
/// </summary>
public sealed class AiExerciseGenerationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly LinguaCoach.UnitTests.ResourceImport.SwappableFakeAiProvider _provider = new();
    private readonly AiExerciseGenerationService _sut;

    public AiExerciseGenerationServiceTests()
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

        _sut = new AiExerciseGenerationService(
            _db, new FormIoSchemaValidationService(), new DbPromptAiContextBuilder(_db), aiExecution,
            NullLogger<AiExerciseGenerationService>.Instance);
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

    private ResourceBankItem SeedVocabulary(Guid sourceId, string word = "resilient", string? notes = "able to recover quickly")
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Vocabulary, sourceId, "B2",
            ResourceBankItemContent.Serialize(new VocabularyContent(word, "adjective", notes)));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    private ResourceBankItem SeedReadingReference(Guid sourceId)
    {
        var e = new ResourceBankItem(
            PublishedResourceType.ReadingReference, sourceId, "B1",
            ResourceBankItemContent.Serialize(new ReadingReferenceContent("Article", "Moderate difficulty", "A short excerpt about a delayed flight.")));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    // ── gap_fill ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Gap_fill_valid_ai_sentence_creates_exercise_with_deterministic_answer_key()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        _provider.NextResponses.Enqueue("""{"promptText": "After the layoffs, the team remained ___ and kept working hard.", "distractors": []}""");

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "gap_fill"));

        result.Activity.ReviewStatus.Should().Be("PendingReview");
        result.Activity.AnswerKeyJson.Should().Contain("resilient");
        result.Activity.FormSchemaJson.Should().Contain("layoffs");
        result.Activity.GenerationProvider.Should().Be("fake-provider");
        _provider.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Gap_fill_missing_blank_marker_retries_and_succeeds_on_valid_second_response()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        _provider.NextResponses.Enqueue("""{"promptText": "This sentence has no blank at all.", "distractors": []}""");
        _provider.NextResponses.Enqueue("""{"promptText": "The team stayed ___ under pressure.", "distractors": []}""");

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "gap_fill"));

        result.Activity.FormSchemaJson.Should().Contain("stayed");
        _provider.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Gap_fill_leaking_the_answer_term_outside_the_blank_is_rejected_both_attempts_throws()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        // Both responses leak "resilient" outside the "___" marker — must be rejected, never saved.
        _provider.NextResponses.Enqueue("""{"promptText": "Being resilient, the team stayed ___ under pressure.", "distractors": []}""");
        _provider.NextResponses.Enqueue("""{"promptText": "The resilient team kept working despite the ___.", "distractors": []}""");

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "gap_fill"));

        (await act.Should().ThrowAsync<ExerciseValidationException>())
            .WithMessage("*could not be parsed*deterministic Generate action instead*");
        _provider.CallCount.Should().Be(2);
        (await _db.Exercises.CountAsync()).Should().Be(0);
    }

    // ── multiple_choice_single ──────────────────────────────────────────────

    [Fact]
    public async Task Multiple_choice_valid_distractors_creates_exercise_with_deterministic_correct_answer()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        _provider.NextResponses.Enqueue(
            """{"promptText": "", "distractors": ["easily discouraged by setbacks", "unable to adapt to change", "overly cautious in decisions"]}""");

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "multiple_choice_single"));

        result.Activity.AnswerKeyJson.Should().Contain("able to recover quickly");
        result.Activity.FormSchemaJson.Should().Contain("easily discouraged by setbacks");
        result.Activity.ScoringRulesJson.Should().Contain("opt_0");
    }

    [Fact]
    public async Task Multiple_choice_all_distractors_matching_correct_answer_both_attempts_throws()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        // Both responses only offer the correct answer text itself as a "distractor" — filtered to
        // zero usable distractors both times.
        _provider.NextResponses.Enqueue("""{"promptText": "", "distractors": ["able to recover quickly"]}""");
        _provider.NextResponses.Enqueue("""{"promptText": "", "distractors": ["ABLE TO RECOVER QUICKLY"]}""");

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "multiple_choice_single"));

        (await act.Should().ThrowAsync<ExerciseValidationException>())
            .WithMessage("*could not be parsed*deterministic Generate action instead*");
        _provider.CallCount.Should().Be(2);
        (await _db.Exercises.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Multiple_choice_resource_with_no_definition_throws_before_any_ai_call()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id, notes: null);

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "multiple_choice_single"));

        (await act.Should().ThrowAsync<ExerciseValidationException>())
            .WithMessage("*no definition*");
        _provider.CallCount.Should().Be(0);
    }

    // ── short_answer ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Short_answer_valid_ai_question_creates_exercise_requiring_manual_evaluation()
    {
        var source = SeedSource();
        var reading = SeedReadingReference(source.Id);
        _provider.NextResponses.Enqueue("""{"promptText": "Why was the flight delayed?", "distractors": []}""");

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingReference", reading.Id, "Primary") },
            RequestedActivityType: "short_answer"));

        result.Activity.FormSchemaJson.Should().Contain("Why was the flight delayed?");
        result.Activity.ScoringRulesJson.Should().Contain("RequiresManualOrAiEvaluation");
    }

    // ── reading_multiple_choice_single (Phase K17) ──────────────────────────────
    // AI supplies the question, correct answer, AND distractors — a deliberate scoped exception,
    // unlike gap_fill/multiple_choice_single where the correct answer always comes from the
    // resource's own field.

    [Fact]
    public async Task Reading_multiple_choice_single_valid_response_stores_ai_supplied_correct_answer()
    {
        var source = SeedSource();
        var reading = SeedReadingReference(source.Id);
        _provider.NextResponses.Enqueue(
            """{"promptText": "Why was the flight delayed?", "correctAnswerText": "Bad weather", "distractors": ["Mechanical issues", "Crew shortage", "Air traffic control"]}""");

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingReference", reading.Id, "Primary") },
            RequestedActivityType: "reading_multiple_choice_single"));

        result.Activity.ReviewStatus.Should().Be("PendingReview");
        result.Activity.ActivityType.Should().Be("reading_multiple_choice_single");
        result.Activity.FormSchemaJson.Should().Contain("radio").And.Contain("Why was the flight delayed?").And.Contain("Bad weather");
        result.Activity.AnswerKeyJson.Should().Contain("Bad weather");
        result.Activity.ScoringRulesJson.Should().Contain("single_choice").And.Contain("opt_0");
    }

    [Fact]
    public async Task Reading_multiple_choice_single_missing_correct_answer_text_retries_then_throws()
    {
        var source = SeedSource();
        var reading = SeedReadingReference(source.Id);
        _provider.NextResponses.Enqueue(
            """{"promptText": "Why was the flight delayed?", "correctAnswerText": "", "distractors": ["A", "B", "C"]}""");
        _provider.NextResponses.Enqueue(
            """{"promptText": "Why was the flight delayed?", "correctAnswerText": "", "distractors": ["A", "B", "C"]}""");

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingReference", reading.Id, "Primary") },
            RequestedActivityType: "reading_multiple_choice_single"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
        _provider.CallCount.Should().Be(2);
        (await _db.Exercises.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Reading_multiple_choice_single_resource_with_no_excerpt_throws_before_any_ai_call()
    {
        var source = SeedSource();
        var e = new ResourceBankItem(
            PublishedResourceType.ReadingReference, source.Id, "B1",
            ResourceBankItemContent.Serialize(new ReadingReferenceContent("Article", null, null)));
        _db.ResourceBankItems.Add(e);
        await _db.SaveChangesAsync();

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingReference", e.Id, "Primary") },
            RequestedActivityType: "reading_multiple_choice_single"));

        (await act.Should().ThrowAsync<ExerciseValidationException>())
            .WithMessage("*no excerpt/passage text*");
        _provider.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Reading_multiple_choice_single_not_supported_for_vocabulary()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "reading_multiple_choice_single"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
        _provider.CallCount.Should().Be(0);
    }

    // ── failure paths shared with Lesson generation's pattern ──────────────────

    [Fact]
    public async Task Ai_provider_unavailable_throws_exercise_validation_exception_no_exercise_created()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        _provider.ThrowUnavailable = true;

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "gap_fill"));

        (await act.Should().ThrowAsync<ExerciseValidationException>())
            .WithMessage("*AI generation is currently unavailable*deterministic Generate action instead*");
        (await _db.Exercises.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task No_resources_throws_before_any_ai_call()
    {
        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            Array.Empty<ExerciseResourceLinkInput>()));

        (await act.Should().ThrowAsync<ExerciseValidationException>())
            .WithMessage("*At least one resource is required*");
        _provider.CallCount.Should().Be(0);
    }
}
