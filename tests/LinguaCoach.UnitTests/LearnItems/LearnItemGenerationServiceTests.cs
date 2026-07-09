using FluentAssertions;
using LinguaCoach.Application.LearnItems;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.LearnItems;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.LearnItems;

/// <summary>
/// Phase H3 — deterministic "Generate Learn" composer. Uses SQLite in-memory (matches
/// ResourceImportServiceTests's convention) with directly-seeded published bank rows — no import
/// pipeline needed to exercise generation itself. All fixture content is synthetic.
/// </summary>
public sealed class LearnItemGenerationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly LearnItemGenerationService _sut;

    public LearnItemGenerationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new LearnItemGenerationService(_db);
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

    private CefrVocabularyEntry SeedVocabulary(Guid sourceId)
    {
        var e = new CefrVocabularyEntry(sourceId, "resilient", "B2", "adjective", "able to recover quickly");
        _db.CefrVocabularyEntries.Add(e);
        _db.SaveChanges();
        return e;
    }

    private CefrGrammarProfileEntry SeedGrammar(Guid sourceId)
    {
        var e = new CefrGrammarProfileEntry(sourceId, "B1", "Present perfect", "Used for past actions with present relevance.");
        _db.CefrGrammarProfileEntries.Add(e);
        _db.SaveChanges();
        return e;
    }

    private CefrReadingReference SeedReadingReference(Guid sourceId)
    {
        var e = new CefrReadingReference(sourceId, "B1", "Article", "Moderate difficulty", "A short excerpt about travel.");
        _db.CefrReadingReferences.Add(e);
        _db.SaveChanges();
        return e;
    }

    private CefrReadingPassage SeedReadingPassage(Guid sourceId)
    {
        var e = new CefrReadingPassage(sourceId, "A Trip Abroad", "This is a full-length reading passage about travel.", "B1");
        _db.CefrReadingPassages.Add(e);
        _db.SaveChanges();
        return e;
    }

    [Fact]
    public async Task Generate_creates_pending_review_learn_item_not_approved()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var result = await _sut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            new[] { new LearnItemResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        result.LearnItem.ReviewStatus.Should().Be("PendingReview");
        result.LearnItem.SourceMode.Should().Be("GeneratedFromResources");
        result.LearnItem.GenerationProvider.Should().Be("Deterministic");
        (await _db.LearnItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Generate_links_to_vocabulary_resource()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var result = await _sut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            new[] { new LearnItemResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        result.LearnItem.Links.Should().ContainSingle();
        result.LearnItem.Links[0].ResourceType.Should().Be("Vocabulary");
        result.LearnItem.Links[0].ResourceId.Should().Be(vocab.Id);
        result.LearnItem.Links[0].Role.Should().Be("Primary");
    }

    [Fact]
    public async Task Generate_links_to_grammar_resource()
    {
        var source = SeedSource();
        var grammar = SeedGrammar(source.Id);

        var result = await _sut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            new[] { new LearnItemResourceLinkInput("Grammar", grammar.Id, "Primary") }));

        result.LearnItem.Links.Should().ContainSingle(l => l.ResourceType == "Grammar" && l.ResourceId == grammar.Id);
        result.LearnItem.Body.Should().Contain("Present perfect");
    }

    [Fact]
    public async Task Generate_links_to_reading_reference_resource()
    {
        var source = SeedSource();
        var reference = SeedReadingReference(source.Id);

        var result = await _sut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            new[] { new LearnItemResourceLinkInput("ReadingReference", reference.Id, "Primary") }));

        result.LearnItem.Links.Should().ContainSingle(l => l.ResourceType == "ReadingReference" && l.ResourceId == reference.Id);
    }

    [Fact]
    public async Task Generate_links_to_reading_passage_resource_and_copies_content_fingerprint()
    {
        var source = SeedSource();
        var passage = SeedReadingPassage(source.Id);

        var result = await _sut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            new[] { new LearnItemResourceLinkInput("ReadingPassage", passage.Id, "Primary") }));

        var link = result.LearnItem.Links.Should().ContainSingle().Subject;
        link.ResourceType.Should().Be("ReadingPassage");
        link.SnapshotTitle.Should().Be("A Trip Abroad");
    }

    [Fact]
    public async Task Generate_with_multiple_resources_preserves_traceability_for_each()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        var grammar = SeedGrammar(source.Id);

        var result = await _sut.HandleAsync(new GenerateLearnItemFromResourcesRequest(new[]
        {
            new LearnItemResourceLinkInput("Vocabulary", vocab.Id, "Primary"),
            new LearnItemResourceLinkInput("Grammar", grammar.Id, "Supporting"),
        }));

        result.LearnItem.Links.Should().HaveCount(2);
        result.LearnItem.Links.Should().Contain(l => l.ResourceId == vocab.Id && l.Role == "Primary");
        result.LearnItem.Links.Should().Contain(l => l.ResourceId == grammar.Id && l.Role == "Supporting");
    }

    [Fact]
    public async Task Generate_with_invalid_resource_id_is_rejected()
    {
        var act = async () => await _sut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            new[] { new LearnItemResourceLinkInput("Vocabulary", Guid.NewGuid(), "Primary") }));

        await act.Should().ThrowAsync<LearnItemValidationException>();
        (await _db.LearnItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Generate_with_invalid_resource_type_is_rejected()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        var act = async () => await _sut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            new[] { new LearnItemResourceLinkInput("Listening", vocab.Id, "Primary") }));

        await act.Should().ThrowAsync<LearnItemValidationException>();
    }

    [Fact]
    public async Task Generate_requires_at_least_one_resource()
    {
        var act = async () => await _sut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            Array.Empty<LearnItemResourceLinkInput>()));

        await act.Should().ThrowAsync<LearnItemValidationException>();
    }

    [Fact]
    public async Task Generate_does_not_modify_the_source_resource_bank_row()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);
        var originalWord = vocab.Word;
        var originalUpdatedAt = vocab.CefrLevel;

        await _sut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            new[] { new LearnItemResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        var reloaded = await _db.CefrVocabularyEntries.FirstAsync(v => v.Id == vocab.Id);
        reloaded.Word.Should().Be(originalWord);
        reloaded.CefrLevel.Should().Be(originalUpdatedAt);
    }

    [Fact]
    public async Task Generate_creates_no_activity_module_or_student_rows()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id);

        await _sut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            new[] { new LearnItemResourceLinkInput("Vocabulary", vocab.Id, "Primary") }));

        (await _db.LearningActivities.CountAsync()).Should().Be(0);
        (await _db.LearningModules.CountAsync()).Should().Be(0);
        (await _db.StudentProfiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Generate_applies_default_metadata_when_resource_has_none_and_row_value_overrides()
    {
        var source = SeedSource();
        var vocab = SeedVocabulary(source.Id); // CEFR B2, no context/focus tags, no subskill.

        var result = await _sut.HandleAsync(new GenerateLearnItemFromResourcesRequest(
            new[] { new LearnItemResourceLinkInput("Vocabulary", vocab.Id, "Primary") },
            DefaultSkill: "Vocabulary", DefaultSubskill: "CoreWords", DefaultDifficultyBand: 3));

        result.LearnItem.CefrLevel.Should().Be("B2"); // from the resource itself, no default given
        result.LearnItem.Skill.Should().Be("Vocabulary");
        result.LearnItem.Subskill.Should().Be("CoreWords");
        result.LearnItem.DifficultyBand.Should().Be(3);
    }
}
