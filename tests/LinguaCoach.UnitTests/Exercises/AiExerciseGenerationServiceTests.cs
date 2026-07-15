using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.Exercises;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Exercises;

/// <summary>
/// Phase J2b — AI-assisted "Generate Activity" composer for the Lesson-based entry point. Uses
/// SQLite in-memory plus the SwappableFakeAiProvider/FakeAiProviderResolver/
/// NeverCalledUsageQuotaService fakes already defined internal to
/// LinguaCoach.UnitTests.ResourceImport (same assembly, so reusable here) — never calls a real AI
/// provider. Special attention to the safety checks that don't exist for Lesson generation:
/// gap_fill answer-leak detection, and multiple_choice_single's correct-answer/scoring never being
/// AI-supplied.
///
/// Phase 2 (2026-07-15 exercise pipeline boundary consolidation) — this file predates the removal
/// of the direct "generate from resources" entry point and exercises the shared AI composition
/// logic (still fully intact) via what used to be its own public request/entry point. Rather than
/// hand-editing 25+ call sites, <see cref="GenerateFromResourcesAsync"/> seeds a throwaway Lesson
/// (and a LessonResourceLink for the same resource each test already selects) and routes through
/// the sole surviving <see cref="IGenerateActivityFromLessonWithAiHandler"/> entry point — exactly
/// what a real caller must do post-Phase-2.
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

    private sealed record TestResourcesRequest(
        IReadOnlyList<ExerciseResourceLinkInput> Resources,
        string? RequestedActivityType = null,
        string? Title = null,
        string? DefaultCefrLevel = null,
        string? DefaultSkill = null,
        string? DefaultSubskill = null,
        IReadOnlyList<string>? DefaultContextTags = null,
        IReadOnlyList<string>? DefaultFocusTags = null,
        int? DefaultDifficultyBand = null,
        string? Notes = null,
        Guid? CreatedByUserId = null);

    private async Task<GenerateExerciseResult> GenerateFromResourcesAsync(
        TestResourcesRequest request, CancellationToken ct = default)
    {
        var lesson = new Lesson(
            "Test Lesson", "Test lesson body.", LessonSourceMode.Manual,
            request.DefaultCefrLevel, request.DefaultSkill, request.DefaultSubskill,
            contextTagsJson: request.DefaultContextTags is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(request.DefaultContextTags) : "[]",
            focusTagsJson: request.DefaultFocusTags is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(request.DefaultFocusTags) : "[]",
            difficultyBand: request.DefaultDifficultyBand);
        _db.Lessons.Add(lesson);
        _db.SaveChanges();

        foreach (var r in request.Resources)
        {
            if (!LessonResourceLookup.TryParseResourceType(r.ResourceType, out var type)
                || !LessonResourceLookup.TryParseRole(r.Role, out var role))
                continue;
            _db.LessonResourceLinks.Add(new LessonResourceLink(lesson.Id, type, r.ResourceId, role));
        }
        if (request.Resources.Count > 0)
            _db.SaveChanges();

        return await _sut.HandleAsync(new GenerateActivityFromLessonRequest(
            lesson.Id, request.RequestedActivityType, request.Title, request.Notes, request.CreatedByUserId), ct);
    }

    // ── gap_fill ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Gap_fill_valid_ai_sentence_creates_exercise_with_deterministic_answer_key()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        _provider.NextResponses.Enqueue("""{"promptText": "After the layoffs, the team remained ___ and kept working hard.", "distractors": []}""");

        var result = await GenerateFromResourcesAsync(new TestResourcesRequest(
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

        var result = await GenerateFromResourcesAsync(new TestResourcesRequest(
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

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
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

        var result = await GenerateFromResourcesAsync(new TestResourcesRequest(
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

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
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

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
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

        var result = await GenerateFromResourcesAsync(new TestResourcesRequest(
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

        var result = await GenerateFromResourcesAsync(new TestResourcesRequest(
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

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
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

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
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

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "reading_multiple_choice_single"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
        _provider.CallCount.Should().Be(0);
    }

    // ── reading_multiple_choice_multi (Phase K17) ───────────────────────────────

    [Fact]
    public async Task Reading_multiple_choice_multi_valid_response_stores_ai_supplied_correct_answers()
    {
        var source = SeedSource();
        var reading = SeedReadingReference(source.Id);
        _provider.NextResponses.Enqueue(
            """{"promptText": "Select all reasons the flight was delayed.", "correctAnswersText": ["Bad weather", "Late crew arrival"], "distractors": ["Mechanical issues", "Overbooking"]}""");

        var result = await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingReference", reading.Id, "Primary") },
            RequestedActivityType: "reading_multiple_choice_multi"));

        result.Activity.ActivityType.Should().Be("reading_multiple_choice_multi");
        result.Activity.FormSchemaJson.Should().Contain("selectboxes").And.Contain("Bad weather").And.Contain("Late crew arrival");
        result.Activity.AnswerKeyJson.Should().Contain("Bad weather").And.Contain("Late crew arrival");
        result.Activity.ScoringRulesJson.Should().Contain("multiple_choice").And.Contain("opt_0").And.Contain("opt_1");
    }

    [Fact]
    public async Task Reading_multiple_choice_multi_fewer_than_two_correct_answers_retries_then_throws()
    {
        var source = SeedSource();
        var reading = SeedReadingReference(source.Id);
        _provider.NextResponses.Enqueue(
            """{"promptText": "Select all that apply.", "correctAnswersText": ["Bad weather"], "distractors": ["A", "B"]}""");
        _provider.NextResponses.Enqueue(
            """{"promptText": "Select all that apply.", "correctAnswersText": [], "distractors": ["A", "B"]}""");

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingReference", reading.Id, "Primary") },
            RequestedActivityType: "reading_multiple_choice_multi"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
        _provider.CallCount.Should().Be(2);
        (await _db.Exercises.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Reading_multiple_choice_multi_resource_with_no_excerpt_throws_before_any_ai_call()
    {
        var source = SeedSource();
        var e = new ResourceBankItem(
            PublishedResourceType.ReadingReference, source.Id, "B1",
            ResourceBankItemContent.Serialize(new ReadingReferenceContent("Article", null, null)));
        _db.ResourceBankItems.Add(e);
        await _db.SaveChangesAsync();

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingReference", e.Id, "Primary") },
            RequestedActivityType: "reading_multiple_choice_multi"));

        (await act.Should().ThrowAsync<ExerciseValidationException>())
            .WithMessage("*no excerpt/passage text*");
        _provider.CallCount.Should().Be(0);
    }

    // ── listening_multiple_choice_single/multi (Phase K17) ──────────────────────
    // Reuse the exact same compose methods as their reading counterparts — only the source text
    // (transcript vs excerpt) differs, same "AI supplies the correct answer(s)" exception.

    private ResourceBankItem SeedListeningPassage(Guid sourceId, string? transcript = "The meeting has been moved to Friday due to a scheduling conflict.")
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Listening, sourceId, "B1",
            ResourceBankItemContent.Serialize(new ListeningPassageContent("Office Announcement", transcript, "storage/key.mp3", "audio/mpeg", null)));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    [Fact]
    public async Task Listening_multiple_choice_single_valid_response_stores_ai_supplied_correct_answer()
    {
        var source = SeedSource();
        var listening = SeedListeningPassage(source.Id);
        _provider.NextResponses.Enqueue(
            """{"promptText": "Why was the meeting moved?", "correctAnswerText": "A scheduling conflict", "distractors": ["Bad weather", "Room unavailable", "Low attendance"]}""");

        var result = await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", listening.Id, "Primary") },
            RequestedActivityType: "listening_multiple_choice_single"));

        result.Activity.ActivityType.Should().Be("listening_multiple_choice_single");
        result.Activity.FormSchemaJson.Should().Contain("radio").And.Contain("A scheduling conflict");
        result.Activity.AnswerKeyJson.Should().Contain("A scheduling conflict");
    }

    [Fact]
    public async Task Listening_multiple_choice_single_resource_with_no_transcript_throws_before_any_ai_call()
    {
        var source = SeedSource();
        var listening = SeedListeningPassage(source.Id, transcript: null);

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", listening.Id, "Primary") },
            RequestedActivityType: "listening_multiple_choice_single"));

        (await act.Should().ThrowAsync<ExerciseValidationException>())
            .WithMessage("*no transcript*");
        _provider.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Listening_multiple_choice_multi_valid_response_stores_ai_supplied_correct_answers()
    {
        var source = SeedSource();
        var listening = SeedListeningPassage(source.Id);
        _provider.NextResponses.Enqueue(
            """{"promptText": "Select all reasons given for the change.", "correctAnswersText": ["A scheduling conflict", "Room availability"], "distractors": ["Budget cuts", "Staff shortage"]}""");

        var result = await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", listening.Id, "Primary") },
            RequestedActivityType: "listening_multiple_choice_multi"));

        result.Activity.ActivityType.Should().Be("listening_multiple_choice_multi");
        result.Activity.FormSchemaJson.Should().Contain("selectboxes");
        result.Activity.ScoringRulesJson.Should().Contain("multiple_choice");
    }

    [Fact]
    public async Task Listening_multiple_choice_single_not_supported_for_vocabulary()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "listening_multiple_choice_single"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
        _provider.CallCount.Should().Be(0);
    }

    // ── highlight_correct_summary (Phase K17) — same AI-supplies-the-answer shape as
    // reading/listening_multiple_choice_single, reused verbatim ──────────────────────────────

    [Fact]
    public async Task Highlight_correct_summary_valid_response_stores_ai_supplied_correct_summary()
    {
        var source = SeedSource();
        var listening = SeedListeningPassage(source.Id);
        _provider.NextResponses.Enqueue(
            """{"promptText": "Which summary best matches what you heard?", "correctAnswerText": "The meeting was moved to Friday due to a conflict.", "distractors": ["The meeting was cancelled entirely.", "The meeting time stayed the same.", "The meeting was moved to Monday."]}""");

        var result = await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", listening.Id, "Primary") },
            RequestedActivityType: "highlight_correct_summary"));

        result.Activity.ActivityType.Should().Be("highlight_correct_summary");
        result.Activity.FormSchemaJson.Should().Contain("radio").And.Contain("The meeting was moved to Friday due to a conflict.");
        result.Activity.AnswerKeyJson.Should().Contain("The meeting was moved to Friday due to a conflict.");
    }

    [Fact]
    public async Task Highlight_correct_summary_resource_with_no_transcript_throws_before_any_ai_call()
    {
        var source = SeedSource();
        var listening = SeedListeningPassage(source.Id, transcript: null);

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", listening.Id, "Primary") },
            RequestedActivityType: "highlight_correct_summary"));

        (await act.Should().ThrowAsync<ExerciseValidationException>())
            .WithMessage("*no transcript*");
        _provider.CallCount.Should().Be(0);
    }

    // ── select_missing_word (Phase K17) — deterministic correct answer, AI only supplies
    // wrong-word distractors ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Select_missing_word_valid_response_uses_deterministic_correct_word()
    {
        var source = SeedSource();
        var listening = SeedListeningPassage(source.Id, "The meeting has been moved to Friday due to a scheduling conflict.");
        _provider.NextResponses.Enqueue("""{"distractors": ["cancelled", "postponed", "rescheduled"]}""");

        var result = await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", listening.Id, "Primary") },
            RequestedActivityType: "select_missing_word"));

        result.Activity.ActivityType.Should().Be("select_missing_word");
        // "meeting" is the first eligible content word (length >= 5, alphabetic) in the transcript.
        result.Activity.AnswerKeyJson.Should().Contain("meeting");
        result.Activity.FormSchemaJson.Should().Contain("_____").And.Contain("cancelled").And.Contain("meeting");
    }

    [Fact]
    public async Task Select_missing_word_all_distractors_matching_correct_word_both_attempts_throws()
    {
        var source = SeedSource();
        var listening = SeedListeningPassage(source.Id, "The meeting has been moved to Friday due to a scheduling conflict.");
        _provider.NextResponses.Enqueue("""{"distractors": ["meeting"]}""");
        _provider.NextResponses.Enqueue("""{"distractors": ["MEETING"]}""");

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", listening.Id, "Primary") },
            RequestedActivityType: "select_missing_word"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
        _provider.CallCount.Should().Be(2);
        (await _db.Exercises.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Select_missing_word_resource_with_no_eligible_word_throws_before_any_ai_call()
    {
        var source = SeedSource();
        var listening = SeedListeningPassage(source.Id, "It is a go.");

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", listening.Id, "Primary") },
            RequestedActivityType: "select_missing_word"));

        (await act.Should().ThrowAsync<ExerciseValidationException>())
            .WithMessage("*no eligible content word*");
        _provider.CallCount.Should().Be(0);
    }

    // ── failure paths shared with Lesson generation's pattern ──────────────────

    [Fact]
    public async Task Ai_provider_unavailable_throws_exercise_validation_exception_no_exercise_created()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        _provider.ThrowUnavailable = true;

        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "gap_fill"));

        (await act.Should().ThrowAsync<ExerciseValidationException>())
            .WithMessage("*AI generation is currently unavailable*deterministic Generate action instead*");
        (await _db.Exercises.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task No_resources_throws_before_any_ai_call()
    {
        var act = async () => await GenerateFromResourcesAsync(new TestResourcesRequest(
            Array.Empty<ExerciseResourceLinkInput>()));

        (await act.Should().ThrowAsync<ExerciseValidationException>())
            .WithMessage("*no linked resources*");
        _provider.CallCount.Should().Be(0);
    }

    // ── LessonId provenance (Phase 1, 2026-07-15 pipeline safety audit; Phase 2, 2026-07-15
    // exercise pipeline boundary consolidation) — Phase 1 fixed AI-preferred types silently
    // dropping Exercise.LessonId; Phase 2 removed the resources-only entry point entirely, so
    // there is no longer any way to call this handler without a Lesson at all.

    [Fact]
    public async Task Ai_generated_exercise_from_lesson_always_retains_lesson_id()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        var lesson = new Lesson("Resilient", "Resilient means able to recover quickly.",
            LessonSourceMode.Manual, "B1", "Vocabulary");
        _db.Lessons.Add(lesson);
        _db.SaveChanges();
        _db.LessonResourceLinks.Add(new LessonResourceLink(
            lesson.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        _db.SaveChanges();
        _provider.NextResponses.Enqueue(
            """{"promptText": "", "distractors": ["easily discouraged by setbacks", "unable to adapt to change", "overly cautious in decisions"]}""");

        var result = await _sut.HandleAsync(new GenerateActivityFromLessonRequest(
            lesson.Id, RequestedActivityType: "multiple_choice_single"));

        result.Activity.LessonId.Should().Be(lesson.Id);
        result.Activity.SourceMode.Should().Be("GeneratedFromLesson");
        var persisted = await _db.Exercises.AsNoTracking().SingleAsync(e => e.Id == result.Activity.Id);
        persisted.LessonId.Should().Be(lesson.Id);
    }

    [Fact]
    public async Task Ai_generation_fails_clearly_for_lesson_with_no_linked_resources()
    {
        var lesson = new Lesson("Empty Lesson", "No resources linked.", LessonSourceMode.Manual, "B1", "Vocabulary");
        _db.Lessons.Add(lesson);
        _db.SaveChanges();

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromLessonRequest(
            lesson.Id, RequestedActivityType: "multiple_choice_single"));

        (await act.Should().ThrowAsync<ExerciseValidationException>())
            .WithMessage("*no linked resources*");
        (await _db.Exercises.CountAsync()).Should().Be(0);
    }
}
