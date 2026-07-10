using FluentAssertions;
using LinguaCoach.Application.ActivityDefinitions;
using LinguaCoach.Application.LearnItems;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ActivityDefinitions;
using LinguaCoach.Infrastructure.LearnItems;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ActivityDefinitions;

/// <summary>
/// Phase H4 — deterministic "Generate Activity" composer (both entry points). Uses SQLite
/// in-memory (matches LearnItemGenerationServiceTests's convention) with directly-seeded
/// published bank rows. All fixture content is synthetic.
/// </summary>
public sealed class ActivityGenerationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ActivityGenerationService _sut;
    private readonly LearnItemGenerationService _learnItemSut;

    public ActivityGenerationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ActivityGenerationService(_db, new FormIoSchemaValidationService());
        _learnItemSut = new LearnItemGenerationService(_db);
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
            new[] { new ActivityResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        result.Activity.ReviewStatus.Should().Be("PendingReview");
        result.Activity.SourceMode.Should().Be("GeneratedFromResources");
        result.Activity.GenerationProvider.Should().Be("Deterministic");
        (await _db.ActivityDefinitions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Generate_stores_cefr_skill_subskill_context_focus_difficulty_metadata()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ActivityResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
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
            new[] { new ActivityResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
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
            new[] { new ActivityResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
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
            new[] { new ActivityResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        result.Activity.Links.Should().ContainSingle(l => l.ResourceType == "Vocabulary" && l.ResourceId == vocab.Id);
    }

    [Fact]
    public async Task Generate_links_to_grammar_resource()
    {
        var source = SeedSource();
        var grammar = SeedGrammar(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ActivityResourceLinkInput("Grammar", grammar.Id, "Primary") }));

        result.Activity.Links.Should().ContainSingle(l => l.ResourceType == "Grammar" && l.ResourceId == grammar.Id);
        result.Activity.ActivityType.Should().Be("gap_fill");
    }

    [Fact]
    public async Task Generate_links_to_reading_reference_resource_and_defaults_to_short_answer()
    {
        var source = SeedSource();
        var reference = SeedReadingReference(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ActivityResourceLinkInput("ReadingReference", reference.Id, "Primary") }));

        result.Activity.Links.Should().ContainSingle(l => l.ResourceType == "ReadingReference");
        result.Activity.ActivityType.Should().Be("short_answer");
        result.Activity.ScoringRulesJson.Should().Contain("RequiresManualOrAiEvaluation").And.Contain("true");
    }

    [Fact]
    public async Task Generate_links_to_reading_passage_resource()
    {
        var source = SeedSource();
        var passage = SeedReadingPassage(source.Id);

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ActivityResourceLinkInput("ReadingPassage", passage.Id, "Primary") }));

        result.Activity.Links.Should().ContainSingle(l => l.ResourceType == "ReadingPassage" && l.ResourceId == passage.Id);
    }

    [Fact]
    public async Task Multiple_choice_single_uses_a_sibling_resource_as_distractor()
    {
        var source = SeedSource();
        var vocab1 = SeedVocabulary(source.Id, "resilient", "able to recover quickly");
        SeedVocabulary(source.Id, "diligent", "showing care in one's work");

        var result = await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ActivityResourceLinkInput("Vocabulary", vocab1.Id, "Primary") },
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
            new[] { new ActivityResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            RequestedActivityType: "multiple_choice_single"));

        await act.Should().ThrowAsync<ActivityDefinitionValidationException>();
        (await _db.ActivityDefinitions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Generate_from_learn_item_creates_pending_activity_linked_to_the_learn_item()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        var learnItemResult = await _learnItemSut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            new[] { new LearnItemResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        var result = await _sut.HandleAsync(new GenerateActivityFromLearnItemRequest(learnItemResult.LearnItem.Id));

        result.Activity.ReviewStatus.Should().Be("PendingReview");
        result.Activity.SourceMode.Should().Be("GeneratedFromLearnItem");
        result.Activity.LearnItemId.Should().Be(learnItemResult.LearnItem.Id);
    }

    [Fact]
    public async Task Generate_from_learn_item_preserves_the_learn_items_own_resource_traceability()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        var learnItemResult = await _learnItemSut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            new[] { new LearnItemResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        var result = await _sut.HandleAsync(new GenerateActivityFromLearnItemRequest(learnItemResult.LearnItem.Id));

        result.Activity.Links.Should().ContainSingle(l => l.ResourceType == "Vocabulary" && l.ResourceId == vocab.Id);
    }

    [Fact]
    public async Task Generate_from_learn_item_with_no_links_is_rejected()
    {
        var learnItem = new LearnItem("Manual title", "Manual body", LearnItemSourceMode.Manual);
        _db.LearnItems.Add(learnItem);
        await _db.SaveChangesAsync();

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromLearnItemRequest(learnItem.Id));

        await act.Should().ThrowAsync<ActivityDefinitionValidationException>();
    }

    [Fact]
    public async Task Generate_with_invalid_resource_id_is_rejected()
    {
        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ActivityResourceLinkInput("Vocabulary", Guid.NewGuid(), "Primary") }));

        await act.Should().ThrowAsync<ActivityDefinitionValidationException>();
        (await _db.ActivityDefinitions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Generate_with_invalid_resource_type_is_rejected()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var act = async () => await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ActivityResourceLinkInput("Listening", vocab.Id, "Primary") }));

        await act.Should().ThrowAsync<ActivityDefinitionValidationException>();
    }

    [Fact]
    public async Task Generate_does_not_modify_the_source_resource_bank_row()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        var originalWord = ResourceBankItemContent.Deserialize<VocabularyContent>(vocab.ContentJson).Word;

        await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ActivityResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        var reloaded = await _db.ResourceBankItems.FirstAsync(v => v.Id == vocab.Id);
        ResourceBankItemContent.Deserialize<VocabularyContent>(reloaded.ContentJson).Word.Should().Be(originalWord);
    }

    [Fact]
    public async Task Generate_from_learn_item_does_not_modify_the_learn_item()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        var learnItemResult = await _learnItemSut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            new[] { new LearnItemResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));
        var originalTitle = learnItemResult.LearnItem.Title;
        var originalStatus = learnItemResult.LearnItem.ReviewStatus;

        await _sut.HandleAsync(new GenerateActivityFromLearnItemRequest(learnItemResult.LearnItem.Id));

        var reloaded = await _db.LearnItems.FirstAsync(l => l.Id == learnItemResult.LearnItem.Id);
        reloaded.Title.Should().Be(originalTitle);
        reloaded.ReviewStatus.ToString().Should().Be(originalStatus);
    }

    [Fact]
    public async Task Generate_creates_no_module_or_student_rows()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        await _sut.HandleAsync(new GenerateActivityFromResourcesRequest(
            new[] { new ActivityResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        (await _db.StudentProfiles.CountAsync()).Should().Be(0);
        (await _db.LearningActivities.CountAsync()).Should().Be(0);
    }
}
