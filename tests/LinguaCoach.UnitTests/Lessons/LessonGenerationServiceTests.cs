using FluentAssertions;
using LinguaCoach.Application.Lessons;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.Lessons;

/// <summary>
/// Phase H3 — deterministic "Generate Learn" composer. Uses SQLite in-memory (matches
/// ResourceImportServiceTests's convention) with directly-seeded published bank rows — no import
/// pipeline needed to exercise generation itself. All fixture content is synthetic.
/// </summary>
public sealed class LessonGenerationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly LessonGenerationService _sut;

    public LessonGenerationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new LessonGenerationService(_db);
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
    public async Task Generate_creates_pending_review_lesson_not_approved()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var result = await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        result.Lesson.ReviewStatus.Should().Be("PendingReview");
        result.Lesson.SourceMode.Should().Be("GeneratedFromResources");
        result.Lesson.GenerationProvider.Should().Be("Deterministic");
        (await _db.Lessons.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Generate_links_to_vocabulary_resource()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var result = await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        result.Lesson.Links.Should().ContainSingle();
        result.Lesson.Links[0].ResourceType.Should().Be("Vocabulary");
        result.Lesson.Links[0].ResourceId.Should().Be(vocab.Id);
        result.Lesson.Links[0].Role.Should().Be("Primary");
    }

    [Fact]
    public async Task Generate_links_to_grammar_resource()
    {
        var source = SeedSource();
        var grammar = SeedGrammar(source.Id);

        var result = await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Grammar", grammar.Id, "Primary") }));

        result.Lesson.Links.Should().ContainSingle(l => l.ResourceType == "Grammar" && l.ResourceId == grammar.Id);
        result.Lesson.Body.Should().Contain("Present perfect");
    }

    [Fact]
    public async Task Generate_links_to_reading_reference_resource()
    {
        var source = SeedSource();
        var reference = SeedReadingReference(source.Id);

        var result = await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("ReadingReference", reference.Id, "Primary") }));

        result.Lesson.Links.Should().ContainSingle(l => l.ResourceType == "ReadingReference" && l.ResourceId == reference.Id);
    }

    [Fact]
    public async Task Generate_links_to_reading_passage_resource_and_copies_content_fingerprint()
    {
        var source = SeedSource();
        var passage = SeedReadingPassage(source.Id);

        var result = await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("ReadingPassage", passage.Id, "Primary") }));

        var link = result.Lesson.Links.Should().ContainSingle().Subject;
        link.ResourceType.Should().Be("ReadingPassage");
        link.SnapshotTitle.Should().Be("A Trip Abroad");
    }

    // ── Phase K17 — Writing/Listening/Speaking resources are now resolvable by
    // LessonResourceLookup.FindAsync (previously always null, silently blocking Lesson generation
    // from these resource types despite them being importable/publishable since J5a/J5c/J5d) ─────

    private ResourceBankItem SeedWritingPrompt(Guid sourceId)
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Writing, sourceId, "B1",
            ResourceBankItemContent.Serialize(new WritingPromptContent("Email a Colleague", "Write an email asking a colleague for a status update.", "email", 80)));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    private ResourceBankItem SeedListeningPassage(Guid sourceId)
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Listening, sourceId, "B1",
            ResourceBankItemContent.Serialize(new ListeningPassageContent("Office Announcement", "The meeting has been moved to Friday.", "storage/key.mp3", "audio/mpeg", null)));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    private ResourceBankItem SeedSpeakingPrompt(Guid sourceId)
    {
        var e = new ResourceBankItem(
            PublishedResourceType.Speaking, sourceId, "B1",
            ResourceBankItemContent.Serialize(new SpeakingPromptContent("Describe Your Role", "Describe your daily responsibilities at work.", 60)));
        _db.ResourceBankItems.Add(e);
        _db.SaveChanges();
        return e;
    }

    [Fact]
    public async Task Generate_links_to_writing_resource()
    {
        var source = SeedSource();
        var writing = SeedWritingPrompt(source.Id);

        var result = await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Writing", writing.Id, "Primary") }));

        result.Lesson.Links.Should().ContainSingle(l => l.ResourceType == "Writing" && l.ResourceId == writing.Id);
        result.Lesson.Body.Should().Contain("status update");
    }

    [Fact]
    public async Task Generate_links_to_listening_resource()
    {
        var source = SeedSource();
        var listening = SeedListeningPassage(source.Id);

        var result = await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Listening", listening.Id, "Primary") }));

        result.Lesson.Links.Should().ContainSingle(l => l.ResourceType == "Listening" && l.ResourceId == listening.Id);
        result.Lesson.Body.Should().Contain("moved to Friday");
    }

    [Fact]
    public async Task Generate_links_to_speaking_resource()
    {
        var source = SeedSource();
        var speaking = SeedSpeakingPrompt(source.Id);

        var result = await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Speaking", speaking.Id, "Primary") }));

        result.Lesson.Links.Should().ContainSingle(l => l.ResourceType == "Speaking" && l.ResourceId == speaking.Id);
        result.Lesson.Body.Should().Contain("daily responsibilities");
    }

    [Fact]
    public async Task Generate_with_multiple_resources_preserves_traceability_for_each()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        var grammar = SeedGrammar(source.Id);

        var result = await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(new[]
        {
            new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary"),
            new LessonResourceLinkInput("Grammar", grammar.Id, "Supporting"),
        }));

        result.Lesson.Links.Should().HaveCount(2);
        result.Lesson.Links.Should().Contain(l => l.ResourceId == vocab.Id && l.Role == "Primary");
        result.Lesson.Links.Should().Contain(l => l.ResourceId == grammar.Id && l.Role == "Supporting");
    }

    [Fact]
    public async Task Generate_with_invalid_resource_id_is_rejected()
    {
        var act = async () => await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", Guid.NewGuid(), "Primary") }));

        await act.Should().ThrowAsync<LessonValidationException>();
        (await _db.Lessons.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Generate_with_invalid_resource_type_is_rejected()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var act = async () => await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Listening", vocab.Id, "Primary") }));

        await act.Should().ThrowAsync<LessonValidationException>();
    }

    [Fact]
    public async Task Generate_requires_at_least_one_resource()
    {
        var act = async () => await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            Array.Empty<LessonResourceLinkInput>()));

        await act.Should().ThrowAsync<LessonValidationException>();
    }

    [Fact]
    public async Task Generate_does_not_modify_the_source_resource_bank_row()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        var originalWord = ResourceBankItemContent.Deserialize<VocabularyContent>(vocab.ContentJson).Word;
        var originalUpdatedAt = vocab.CefrLevel;

        await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        var reloaded = await _db.ResourceBankItems.FirstAsync(v => v.Id == vocab.Id);
        ResourceBankItemContent.Deserialize<VocabularyContent>(reloaded.ContentJson).Word.Should().Be(originalWord);
        reloaded.CefrLevel.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public async Task Generate_creates_no_activity_module_or_student_rows()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        (await _db.LearningActivities.CountAsync()).Should().Be(0);
        (await _db.LearningModules.CountAsync()).Should().Be(0);
        (await _db.StudentProfiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Generate_applies_default_metadata_when_resource_has_none_and_row_value_overrides()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id); // CEFR B2, no context/focus tags, no subskill.

        var result = await _sut.HandleAsync(new GenerateLessonFromResourcesRequest(
            new[] { new LessonResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            DefaultSkill: "Vocabulary", DefaultSubskill: "CoreWords", DefaultDifficultyBand: 3));

        result.Lesson.CefrLevel.Should().Be("B2"); // from the resource itself, no default given
        result.Lesson.Skill.Should().Be("Vocabulary");
        result.Lesson.Subskill.Should().Be("CoreWords");
        result.Lesson.DifficultyBand.Should().Be(3);
    }
}
