using FluentAssertions;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.Lessons;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Exercises;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.Exercises;

/// <summary>
/// Phase H4 — deterministic "Generate Activity" composer (both entry points). Uses SQLite
/// in-memory (matches LessonGenerationServiceTests's convention) with directly-seeded
/// published bank rows. All fixture content is synthetic.
/// </summary>
public sealed class ActivityGenerationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ActivityGenerationService _sut;
    private readonly LessonGenerationService _lessonSut;

    public ActivityGenerationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ActivityGenerationService(_db, new FormIoSchemaValidationService());
        _lessonSut = new LessonGenerationService(_db);
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

    private ResourceBankItem SeedGrammar(Guid sourceId)
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Grammar, sourceId, "B1",
            ResourceBankItemContent.Serialize(new GrammarContent("Present perfect", "Used for past actions with present relevance.")));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    private ResourceBankItem SeedReadingReference(Guid sourceId)
    {
        var e = new ResourceBankItem(
            PublishedResourceType.ReadingReference, sourceId, "B1",
            ResourceBankItemContent.Serialize(new ReadingReferenceContent("Article", "Moderate difficulty", "A short excerpt about travel.")));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    private ResourceBankItem SeedListeningLecture(Guid sourceId, string? transcript = "Today's lecture covers the basics of project management, including timelines, budgets, and stakeholder communication.")
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Listening, sourceId, "B2",
            ResourceBankItemContent.Serialize(new ListeningPassageContent("Project Management Basics", transcript, "storage/lecture.mp3", "audio/mpeg", null)));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    private ResourceBankItem SeedListeningPassage(Guid sourceId, string? transcript = "This is a full-length audio transcript about travel plans.")
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Listening, sourceId, "B1",
            ResourceBankItemContent.Serialize(new ListeningPassageContent("Office Announcement", transcript, "storage/key.mp3", "audio/mpeg", null)));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    private ResourceBankItem SeedSpeakingPrompt(Guid sourceId, string promptText = "Describe your daily responsibilities at work.")
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Speaking, sourceId, "B1",
            ResourceBankItemContent.Serialize(new SpeakingPromptContent("Describe Your Role", promptText, 60)));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    private ResourceBankItem SeedWritingPrompt(Guid sourceId, string promptText = "Write an email asking a colleague for a status update.")
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Writing, sourceId, "B1",
            ResourceBankItemContent.Serialize(new WritingPromptContent("Email a Colleague", promptText, "email", 80)));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    private ResourceBankItem SeedReadingPassage(Guid sourceId)
    {
        const string passageText = "This is a full-length reading passage about travel.";
        var wordCount = passageText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var e = new ResourceBankItem(
            PublishedResourceType.ReadingPassage, sourceId, "B1",
            ResourceBankItemContent.Serialize(new ReadingPassageContent(
                "A Trip Abroad", passageText, null, "Reading", null, wordCount, 1, null, null)));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    [Fact]
    public async Task Generate_from_resources_creates_pending_review_activity_not_approved()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        result.Activity.ReviewStatus.Should().Be("PendingReview");
        result.Activity.SourceMode.Should().Be("GeneratedFromResources");
        result.Activity.GenerationProvider.Should().Be("Deterministic");
        (await _db.Exercises.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Generate_stores_cefr_skill_subskill_context_focus_difficulty_metadata()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            DefaultSkill: "Vocabulary", DefaultSubskill: "CoreWords", DefaultDifficultyBand: 3));

        result.Activity.CefrLevel.Should().Be("B2");
        result.Activity.Skill.Should().Be("Vocabulary");
        result.Activity.Subskill.Should().Be("CoreWords");
        result.Activity.DifficultyBand.Should().Be(3);
    }

    [Fact]
    public async Task Generate_gap_fill_stores_formio_schema_and_renderer_type()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "gap_fill"));

        result.Activity.RendererType.Should().Be("Formio");
        result.Activity.ActivityType.Should().Be("gap_fill");
        result.Activity.FormSchemaJson.Should().NotBeNullOrWhiteSpace();
        result.Activity.FormSchemaJson.Should().Contain("textfield");
    }

    [Fact]
    public async Task Generate_gap_fill_stores_answer_key_and_scoring_and_feedback_plan()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id, word: "resilient");

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "gap_fill"));

        result.Activity.AnswerKeyJson.Should().Contain("resilient");
        result.Activity.ScoringRulesJson.Should().Contain("text_normalized");
        result.Activity.FeedbackPlanJson.Should().Contain("correctFeedback");
    }

    [Fact]
    public async Task Generate_links_to_vocabulary_resource()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        result.Activity.Links.Should().ContainSingle(l => l.ResourceType == "Vocabulary" && l.ResourceId == vocab.Id);
    }

    [Fact]
    public async Task Generate_links_to_grammar_resource()
    {
        var source = SeedSource();
        var grammar = SeedGrammar(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Grammar", grammar.Id, "Primary") }));

        result.Activity.Links.Should().ContainSingle(l => l.ResourceType == "Grammar" && l.ResourceId == grammar.Id);
        result.Activity.ActivityType.Should().Be("gap_fill");
    }

    [Fact]
    public async Task Generate_links_to_reading_reference_resource_and_defaults_to_short_answer()
    {
        var source = SeedSource();
        var reference = SeedReadingReference(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingReference", reference.Id, "Primary") }));

        result.Activity.Links.Should().ContainSingle(l => l.ResourceType == "ReadingReference");
        result.Activity.ActivityType.Should().Be("short_answer");
        result.Activity.ScoringRulesJson.Should().Contain("RequiresManualOrAiEvaluation").And.Contain("true");
    }

    // ── Phase J4 — honest "will this ever launch" flag, independent of review status ──────────

    [Fact]
    public async Task Generated_gap_fill_draft_is_flagged_as_launchable_once_approved()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "gap_fill"));

        result.Activity.ReviewStatus.Should().Be("PendingReview");
        result.Activity.CanLaunchOnceApproved.Should().BeTrue();
        result.Activity.LaunchUnsupportedReason.Should().BeNull();
    }

    [Fact]
    public async Task Generated_short_answer_draft_is_flagged_as_not_launchable_with_a_reason_even_while_pending()
    {
        var source = SeedSource();
        var reference = SeedReadingReference(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingReference", reference.Id, "Primary") }));

        result.Activity.ReviewStatus.Should().Be("PendingReview");
        result.Activity.CanLaunchOnceApproved.Should().BeFalse();
        result.Activity.LaunchUnsupportedReason.Should().Contain("not launchable yet");
    }

    [Fact]
    public async Task Generate_links_to_reading_passage_resource()
    {
        var source = SeedSource();
        var passage = SeedReadingPassage(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingPassage", passage.Id, "Primary") }));

        result.Activity.Links.Should().ContainSingle(l => l.ResourceType == "ReadingPassage" && l.ResourceId == passage.Id);
    }

    [Fact]
    public async Task Multiple_choice_single_uses_a_sibling_resource_as_distractor()
    {
        var source = SeedSource();
        var vocab1 = SeedVocabulary(source.Id, "resilient", "able to recover quickly");
        SeedVocabulary(source.Id, "diligent", "showing care in one's work");

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab1.Id, "Primary") },
            RequestedActivityType: "multiple_choice_single"));

        result.Activity.ActivityType.Should().Be("multiple_choice_single");
        result.Activity.FormSchemaJson.Should().Contain("radio").And.Contain("showing care in one");
    }

    [Fact]
    public async Task Multiple_choice_single_without_a_distractor_is_rejected()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "multiple_choice_single"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
        (await _db.Exercises.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Generate_from_lesson_creates_pending_activity_linked_to_the_lesson()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        var lessonResult = await _lessonSut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        var result = await _sut.HandleAsync(new GenerateActivityFromLessonRequest(lessonResult.Lesson.Id));

        result.Activity.ReviewStatus.Should().Be("PendingReview");
        result.Activity.SourceMode.Should().Be("GeneratedFromLesson");
        result.Activity.LessonId.Should().Be(lessonResult.Lesson.Id);
    }

    [Fact]
    public async Task Generate_from_lesson_preserves_the_lessons_own_resource_traceability()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        var lessonResult = await _lessonSut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        var result = await _sut.HandleAsync(new GenerateActivityFromLessonRequest(lessonResult.Lesson.Id));

        result.Activity.Links.Should().ContainSingle(l => l.ResourceType == "Vocabulary" && l.ResourceId == vocab.Id);
    }

    [Fact]
    public async Task Generate_from_lesson_with_no_links_is_rejected()
    {
        var lesson = new Lesson("Manual title", "Manual body", LessonSourceMode.Manual);
        _db.Lessons.Add(lesson);
        await _db.SaveChangesAsync();

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromLessonRequest(lesson.Id));

        await act.Should().ThrowAsync<ExerciseValidationException>();
    }

    [Fact]
    public async Task Generate_with_invalid_resource_id_is_rejected()
    {
        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", Guid.NewGuid(), "Primary") }));

        await act.Should().ThrowAsync<ExerciseValidationException>();
        (await _db.Exercises.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Generate_with_invalid_resource_type_is_rejected()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", vocab.Id, "Primary") }));

        await act.Should().ThrowAsync<ExerciseValidationException>();
    }

    [Fact]
    public async Task Generate_does_not_modify_the_source_resource_bank_row()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        var originalWord = ResourceBankItemContent.Deserialize<VocabularyContent>(vocab.ContentJson).Word;

        await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        var reloaded = await _db.ResourceBankItems.FirstAsync(v => v.Id == vocab.Id);
        ResourceBankItemContent.Deserialize<VocabularyContent>(reloaded.ContentJson).Word.Should().Be(originalWord);
    }

    [Fact]
    public async Task Generate_from_lesson_does_not_modify_the_lesson()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        var lessonResult = await _lessonSut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));
        var originalTitle = lessonResult.Lesson.Title;
        var originalStatus = lessonResult.Lesson.ReviewStatus;

        await _sut.HandleAsync(new GenerateActivityFromLessonRequest(lessonResult.Lesson.Id));

        var reloaded = await _db.Lessons.FirstAsync(l => l.Id == lessonResult.Lesson.Id);
        reloaded.Title.Should().Be(originalTitle);
        reloaded.ReviewStatus.ToString().Should().Be(originalStatus);
    }

    // ── Phase K16 — reading_fill_in_blanks ──────────────────────────────────────────────────

    [Fact]
    public async Task Reading_fill_in_blanks_from_passage_creates_cloze_with_numbered_blanks()
    {
        var source = SeedSource();
        var passage = SeedReadingPassage(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingPassage", passage.Id, "Primary") },
            RequestedActivityType: "reading_fill_in_blanks"));

        result.Activity.ActivityType.Should().Be("reading_fill_in_blanks");
        result.Activity.FormSchemaJson.Should().Contain("textfield").And.Contain("Blank 1");
        result.Activity.FormSchemaJson.Should().NotContain("reading"); // blanked-out word must not leak into the schema
    }

    [Fact]
    public async Task Reading_fill_in_blanks_stores_answer_key_and_text_normalized_scoring()
    {
        var source = SeedSource();
        var passage = SeedReadingPassage(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingPassage", passage.Id, "Primary") },
            RequestedActivityType: "reading_fill_in_blanks"));

        result.Activity.AnswerKeyJson.Should().Contain("answer_0");
        result.Activity.ScoringRulesJson.Should().Contain("text_normalized");
        result.Activity.FeedbackPlanJson.Should().Contain("correctFeedback");
    }

    [Fact]
    public async Task Reading_fill_in_blanks_from_reading_reference_uses_the_excerpt_text()
    {
        var source = SeedSource();
        var reference = SeedReadingReference(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingReference", reference.Id, "Primary") },
            RequestedActivityType: "reading_fill_in_blanks"));

        result.Activity.ActivityType.Should().Be("reading_fill_in_blanks");
        result.Activity.Links.Should().ContainSingle(l => l.ResourceType == "ReadingReference");
    }

    [Fact]
    public async Task Reading_fill_in_blanks_rejected_when_source_text_has_too_few_content_words()
    {
        var source = SeedSource();
        var e = new ResourceBankItem(
            PublishedResourceType.ReadingReference, source.Id, "B1",
            ResourceBankItemContent.Serialize(new ReadingReferenceContent("Note", null, "Go now.")));
        _db.ResourceBankItems.Add(e);
        await _db.SaveChangesAsync();

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingReference", e.Id, "Primary") },
            RequestedActivityType: "reading_fill_in_blanks"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
        (await _db.Exercises.CountAsync()).Should().Be(0);
    }

    // ── Phase K18 — reading_writing_fill_in_blanks ("choose", not "type") ──────────────────────

    [Fact]
    public async Task Reading_writing_fill_in_blanks_creates_radio_choices_with_distractors()
    {
        var source = SeedSource();
        var passage = SeedReadingPassage(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingPassage", passage.Id, "Primary") },
            RequestedActivityType: "reading_writing_fill_in_blanks"));

        result.Activity.ActivityType.Should().Be("reading_writing_fill_in_blanks");
        result.Activity.FormSchemaJson.Should().Contain("radio").And.Contain("Blank 1");
        result.Activity.ScoringRulesJson.Should().Contain("single_choice").And.Contain("opt_0");
    }

    [Fact]
    public async Task Reading_writing_fill_in_blanks_rejected_when_fewer_than_three_content_words()
    {
        var source = SeedSource();
        var e = new ResourceBankItem(
            PublishedResourceType.ReadingReference, source.Id, "B1",
            ResourceBankItemContent.Serialize(new ReadingReferenceContent("Note", null, "It is here now.")));
        _db.ResourceBankItems.Add(e);
        await _db.SaveChangesAsync();

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingReference", e.Id, "Primary") },
            RequestedActivityType: "reading_writing_fill_in_blanks"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
        (await _db.Exercises.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Reading_writing_fill_in_blanks_not_supported_for_vocabulary()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "reading_writing_fill_in_blanks"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
    }

    [Fact]
    public async Task Reading_fill_in_blanks_not_supported_for_vocabulary()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "reading_fill_in_blanks"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
    }

    // ── Phase K17 — email_reply / open_writing_task / write_essay (Writing resources) ──────────

    [Fact]
    public async Task Email_reply_shows_the_resources_own_prompt_text_and_requires_manual_evaluation()
    {
        var source = SeedSource();
        var writing = SeedWritingPrompt(source.Id, "Reply to the customer complaint below.");

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Writing", writing.Id, "Primary") },
            RequestedActivityType: "email_reply"));

        result.Activity.ActivityType.Should().Be("email_reply");
        result.Activity.FormSchemaJson.Should().Contain("Reply to the customer complaint below.").And.Contain("textarea");
        result.Activity.ScoringRulesJson.Should().Contain("RequiresManualOrAiEvaluation").And.Contain("true");
    }

    [Fact]
    public async Task Open_writing_task_is_supported_for_writing_resources()
    {
        var source = SeedSource();
        var writing = SeedWritingPrompt(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Writing", writing.Id, "Primary") },
            RequestedActivityType: "open_writing_task"));

        result.Activity.ActivityType.Should().Be("open_writing_task");
    }

    [Fact]
    public async Task Write_essay_is_supported_for_writing_resources()
    {
        var source = SeedSource();
        var writing = SeedWritingPrompt(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Writing", writing.Id, "Primary") },
            RequestedActivityType: "write_essay"));

        result.Activity.ActivityType.Should().Be("write_essay");
    }

    [Fact]
    public async Task Writing_resource_defaults_to_email_reply_when_no_type_requested()
    {
        var source = SeedSource();
        var writing = SeedWritingPrompt(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Writing", writing.Id, "Primary") }));

        result.Activity.ActivityType.Should().Be("email_reply");
    }

    // ── Phase K17 — summarize_written_text (Writing-skill, Reading-resource-sourced) ───────────

    [Fact]
    public async Task Summarize_written_text_shows_excerpt_and_requires_manual_evaluation()
    {
        var source = SeedSource();
        var reference = SeedReadingReference(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingReference", reference.Id, "Primary") },
            RequestedActivityType: "summarize_written_text"));

        result.Activity.ActivityType.Should().Be("summarize_written_text");
        result.Activity.FormSchemaJson.Should().Contain("A short excerpt about travel.").And.Contain("textarea");
        result.Activity.ScoringRulesJson.Should().Contain("RequiresManualOrAiEvaluation").And.Contain("true");
    }

    [Fact]
    public async Task Summarize_written_text_supported_for_reading_passage_too()
    {
        var source = SeedSource();
        var passage = SeedReadingPassage(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("ReadingPassage", passage.Id, "Primary") },
            RequestedActivityType: "summarize_written_text"));

        result.Activity.ActivityType.Should().Be("summarize_written_text");
    }

    [Fact]
    public async Task Summarize_written_text_not_supported_for_writing_resource()
    {
        var source = SeedSource();
        var writing = SeedWritingPrompt(source.Id);

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Writing", writing.Id, "Primary") },
            RequestedActivityType: "summarize_written_text"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
    }

    // ── Phase K17 — listening_fill_in_blanks (Listening resources) ─────────────────────────────

    [Fact]
    public async Task Listening_fill_in_blanks_creates_cloze_with_numbered_blanks()
    {
        var source = SeedSource();
        var listening = SeedListeningPassage(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", listening.Id, "Primary") },
            RequestedActivityType: "listening_fill_in_blanks"));

        result.Activity.ActivityType.Should().Be("listening_fill_in_blanks");
        result.Activity.FormSchemaJson.Should().Contain("textfield").And.Contain("Blank 1");
        result.Activity.ScoringRulesJson.Should().Contain("text_normalized");
    }

    [Fact]
    public async Task Listening_fill_in_blanks_rejected_when_transcript_missing()
    {
        var source = SeedSource();
        var listening = SeedListeningPassage(source.Id, transcript: null);

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", listening.Id, "Primary") },
            RequestedActivityType: "listening_fill_in_blanks"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
        (await _db.Exercises.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Listening_fill_in_blanks_not_supported_for_writing_resource()
    {
        var source = SeedSource();
        var writing = SeedWritingPrompt(source.Id);

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Writing", writing.Id, "Primary") },
            RequestedActivityType: "listening_fill_in_blanks"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
    }

    // ── Phase K18 — spoken_response_from_prompt / respond_to_situation / answer_short_question /
    // speaking_roleplay_turn / read_aloud (Speaking resources) ─────────────────────────────────

    [Fact]
    public async Task Spoken_response_from_prompt_shows_own_prompt_text_and_requires_manual_evaluation()
    {
        var source = SeedSource();
        var speaking = SeedSpeakingPrompt(source.Id, "Talk about a challenge you faced at work.");

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Speaking", speaking.Id, "Primary") },
            RequestedActivityType: "spoken_response_from_prompt"));

        result.Activity.ActivityType.Should().Be("spoken_response_from_prompt");
        result.Activity.FormSchemaJson.Should().Contain("Talk about a challenge you faced at work.").And.Contain("speakingResponse");
        result.Activity.ScoringRulesJson.Should().Contain("speaking").And.Contain("RequiresManualOrAiEvaluation").And.Contain("true");
    }

    [Theory]
    [InlineData("respond_to_situation")]
    [InlineData("answer_short_question")]
    [InlineData("speaking_roleplay_turn")]
    [InlineData("read_aloud")]
    public async Task Other_speaking_types_are_supported_for_speaking_resources(string activityType)
    {
        var source = SeedSource();
        var speaking = SeedSpeakingPrompt(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Speaking", speaking.Id, "Primary") },
            RequestedActivityType: activityType));

        result.Activity.ActivityType.Should().Be(activityType);
    }

    [Fact]
    public async Task Speaking_resource_defaults_to_spoken_response_from_prompt_when_no_type_requested()
    {
        var source = SeedSource();
        var speaking = SeedSpeakingPrompt(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Speaking", speaking.Id, "Primary") }));

        result.Activity.ActivityType.Should().Be("spoken_response_from_prompt");
    }

    // ── Phase K18 — summarize_spoken_text / retell_lecture / summarize_group_discussion
    // (Listening resources, reuse ComposeWritingPrompt/ComposeSpeakingPrompt unchanged) ─────────

    [Fact]
    public async Task Summarize_spoken_text_shows_transcript_and_requires_manual_evaluation()
    {
        var source = SeedSource();
        var lecture = SeedListeningLecture(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", lecture.Id, "Primary") },
            RequestedActivityType: "summarize_spoken_text"));

        result.Activity.ActivityType.Should().Be("summarize_spoken_text");
        result.Activity.FormSchemaJson.Should().Contain("project management").And.Contain("textarea");
        result.Activity.ScoringRulesJson.Should().Contain("RequiresManualOrAiEvaluation").And.Contain("true");
    }

    [Theory]
    [InlineData("retell_lecture")]
    [InlineData("summarize_group_discussion")]
    public async Task Spoken_summary_types_are_supported_for_listening_resources(string activityType)
    {
        var source = SeedSource();
        var lecture = SeedListeningLecture(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", lecture.Id, "Primary") },
            RequestedActivityType: activityType));

        result.Activity.ActivityType.Should().Be(activityType);
        result.Activity.FormSchemaJson.Should().Contain("speakingResponse");
    }

    [Fact]
    public async Task Gap_fill_not_supported_for_listening_resource()
    {
        var source = SeedSource();
        var lecture = SeedListeningLecture(source.Id);

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Listening", lecture.Id, "Primary") },
            RequestedActivityType: "gap_fill"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
    }

    [Fact]
    public async Task Gap_fill_not_supported_for_speaking_resource()
    {
        var source = SeedSource();
        var speaking = SeedSpeakingPrompt(source.Id);

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Speaking", speaking.Id, "Primary") },
            RequestedActivityType: "gap_fill"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
    }

    [Fact]
    public async Task Gap_fill_not_supported_for_writing_resource()
    {
        var source = SeedSource();
        var writing = SeedWritingPrompt(source.Id);

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Writing", writing.Id, "Primary") },
            RequestedActivityType: "gap_fill"));

        await act.Should().ThrowAsync<ExerciseValidationException>();
    }

    [Fact]
    public async Task Generate_creates_no_module_or_student_rows()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ExerciseResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        (await _db.StudentProfiles.CountAsync()).Should().Be(0);
        (await _db.LearningActivities.CountAsync()).Should().Be(0);
    }
}
