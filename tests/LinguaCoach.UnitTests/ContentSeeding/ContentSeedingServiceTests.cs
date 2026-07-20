using FluentAssertions;
using LinguaCoach.Application.ContentSeeding;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ContentSeeding;
using LinguaCoach.Infrastructure.Exercises;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Infrastructure.Modules;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.ContentSeeding;

/// <summary>
/// Adaptive Curriculum Sprint 6 — bulk content seeding. Uses SQLite in-memory plus the real
/// deterministic generation handlers (LessonGenerationService/ActivityGenerationService/
/// ModuleAutoLinkService) — never mocked — so these tests exercise the actual Lesson→Exercise→
/// Module→approve chain end to end. Only the AI-backed skill-graph tagging step uses a fake
/// (FakeModuleSkillGraphTaggingService), per this repo's "tests use fake providers, never real AI"
/// convention.
/// </summary>
public sealed class ContentSeedingServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly FakeModuleSkillGraphTaggingService _tagging = new();
    private readonly ContentSeedingService _sut;

    public ContentSeedingServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var lessonHandler = new LessonGenerationService(_db);
        var singleActivityHandler = new ActivityGenerationService(_db, new FormIoSchemaValidationService());
        var moduleAutoLink = new ModuleAutoLinkService(_db);
        var activitiesHandler = new LessonExerciseBatchGenerationService(
            singleActivityHandler, new NeverCalledAiActivityHandler(), moduleAutoLink, _db);

        _sut = new ContentSeedingService(
            _db, lessonHandler, activitiesHandler, _tagging, NullLogger<ContentSeedingService>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private CefrResourceSource SeedSource()
    {
        var source = new CefrResourceSource($"Test Source {Guid.NewGuid():N}", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        _db.CefrResourceSources.Add(source);
        _db.SaveChanges();
        return source;
    }

    private ResourceBankItem SeedVocabulary(string cefrLevel, string word)
    {
        var source = SeedSource();
        var item = new ResourceBankItem(
            PublishedResourceType.Vocabulary, source.Id, cefrLevel,
            ResourceBankItemContent.Serialize(new VocabularyContent(word, "adjective", $"means {word}")));
        _db.ResourceBankItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    private ResourceBankItem SeedGrammar(string cefrLevel, string point)
    {
        var source = SeedSource();
        var item = new ResourceBankItem(
            PublishedResourceType.Grammar, source.Id, cefrLevel,
            ResourceBankItemContent.Serialize(new GrammarContent(point, $"{point} explained.")));
        _db.ResourceBankItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    private ResourceBankItem SeedReadingReference(string cefrLevel, string excerpt)
    {
        var source = SeedSource();
        var item = new ResourceBankItem(
            PublishedResourceType.ReadingReference, source.Id, cefrLevel,
            ResourceBankItemContent.Serialize(new ReadingReferenceContent(
                TextType: "Notice", DifficultyNotes: null, ReferenceExcerpt: excerpt)));
        _db.ResourceBankItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    [Fact]
    public async Task Seeds_a_real_approved_module_from_a_vocabulary_resource()
    {
        SeedVocabulary("B1", "resilient");

        var result = await _sut.RunAsync(new ContentSeedingRequest(CefrLevels: ["B1"]));

        result.ResourcesConsidered.Should().Be(1);
        result.ModulesCreatedAndApproved.Should().Be(1);
        var item = result.Items.Should().ContainSingle().Subject;
        item.Success.Should().BeTrue();
        item.ModuleId.Should().NotBeNull();

        var module = await _db.Modules.AsNoTracking().SingleAsync(m => m.Id == item.ModuleId);
        module.ReviewStatus.Should().Be(AdminReviewStatus.Approved);
        module.CefrLevel.Should().Be("B1");
        module.Skill.Should().Be("Vocabulary");
    }

    [Fact]
    public async Task Seeds_a_real_approved_module_from_a_reading_reference_resource()
    {
        // Sprint 9 — Reading extension. Needs enough long content words (>=5 chars) for the
        // deterministic reading_fill_in_blanks cloze algorithm to find blanks from.
        SeedReadingReference("B1", "Employees must submit expense reports before the monthly deadline arrives.");

        var result = await _sut.RunAsync(new ContentSeedingRequest(CefrLevels: ["B1"]));

        result.ResourcesConsidered.Should().Be(1);
        result.ModulesCreatedAndApproved.Should().Be(1);
        var item = result.Items.Should().ContainSingle().Subject;
        item.Success.Should().BeTrue();
        item.ModuleId.Should().NotBeNull();

        var module = await _db.Modules.AsNoTracking().SingleAsync(m => m.Id == item.ModuleId);
        module.ReviewStatus.Should().Be(AdminReviewStatus.Approved);
        module.CefrLevel.Should().Be("B1");

        var moduleId = item.ModuleId!.Value;
        var exerciseLinks = await _db.ModuleExerciseLinks.AsNoTracking().Where(l => l.ModuleId == moduleId).ToListAsync();
        exerciseLinks.Should().NotBeEmpty();
        var exercise = await _db.Exercises.AsNoTracking().SingleAsync(e => e.Id == exerciseLinks[0].ExerciseId);
        exercise.ActivityType.Should().Be("reading_fill_in_blanks");
        exercise.ReviewStatus.Should().Be(AdminReviewStatus.Approved);
    }

    [Fact]
    public async Task Approves_the_generated_lesson_and_exercises_too()
    {
        SeedGrammar("B2", "Present perfect");

        var result = await _sut.RunAsync(new ContentSeedingRequest(CefrLevels: ["B2"], ExercisesPerLesson: 2));

        var moduleId = result.Items.Single().ModuleId!.Value;
        var lessonLink = await _db.ModuleLessonLinks.AsNoTracking().SingleAsync(l => l.ModuleId == moduleId);
        var lesson = await _db.Lessons.AsNoTracking().SingleAsync(l => l.Id == lessonLink.LessonId);
        lesson.ReviewStatus.Should().Be(AdminReviewStatus.Approved);

        var exerciseLinks = await _db.ModuleExerciseLinks.AsNoTracking().Where(l => l.ModuleId == moduleId).ToListAsync();
        exerciseLinks.Should().HaveCount(2);
        foreach (var link in exerciseLinks)
        {
            var exercise = await _db.Exercises.AsNoTracking().SingleAsync(e => e.Id == link.ExerciseId);
            exercise.ReviewStatus.Should().Be(AdminReviewStatus.Approved);
        }
    }

    [Fact]
    public async Task Tags_the_module_with_skill_graph_nodes_when_the_fake_tagger_returns_a_match()
    {
        SeedVocabulary("B1", "resilient");
        var node = new SkillGraphNode("b1.vocabulary.seed_test", "Seed Test Node", "desc", "B1", "vocabulary");
        node.Approve(null);
        _db.SkillGraphNodes.Add(node);
        _db.SaveChanges();
        _tagging.NodeKeyToMatch = node.Key;

        var result = await _sut.RunAsync(new ContentSeedingRequest(CefrLevels: ["B1"]));

        result.NodeLinksCreated.Should().Be(1);
        var moduleId = result.Items.Single().ModuleId!.Value;
        var link = await _db.ModuleSkillGraphNodeLinks.AsNoTracking().SingleOrDefaultAsync(l => l.ModuleId == moduleId);
        link.Should().NotBeNull();
        link!.SkillGraphNodeId.Should().Be(node.Id);
    }

    [Fact]
    public async Task Skips_resources_already_consumed_by_an_existing_lesson()
    {
        var vocab = SeedVocabulary("B1", "resilient");
        var lesson = new Lesson("Resilient", "body", LessonSourceMode.Manual, "B1", "Vocabulary");
        _db.Lessons.Add(lesson);
        _db.SaveChanges();
        _db.LessonResourceLinks.Add(new LessonResourceLink(lesson.Id, PublishedResourceType.Vocabulary, vocab.Id, LessonResourceRole.Primary));
        _db.SaveChanges();

        var result = await _sut.RunAsync(new ContentSeedingRequest(CefrLevels: ["B1"]));

        result.ResourcesConsidered.Should().Be(0);
    }

    [Fact]
    public async Task Respects_max_resources_per_cefr_level_per_type()
    {
        SeedVocabulary("B1", "resilient");
        SeedVocabulary("B1", "diligent");
        SeedVocabulary("B1", "candid");

        var result = await _sut.RunAsync(new ContentSeedingRequest(CefrLevels: ["B1"], MaxResourcesPerCefrLevelPerType: 2));

        result.ResourcesConsidered.Should().Be(2);
    }

    [Fact]
    public async Task Continues_after_a_single_resource_failure()
    {
        // A Vocabulary resource whose word is a single space collapses to an empty Lesson title
        // after trimming, which fails Lesson's own validation — a real, deterministic failure mode
        // to verify continue-on-error against, not a contrived exception.
        var source = SeedSource();
        _db.ResourceBankItems.Add(new ResourceBankItem(
            PublishedResourceType.Vocabulary, source.Id, "B1",
            ResourceBankItemContent.Serialize(new VocabularyContent(" ", null, null))));
        _db.SaveChanges();
        SeedVocabulary("B1", "resilient");

        var result = await _sut.RunAsync(new ContentSeedingRequest(CefrLevels: ["B1"]));

        result.ResourcesConsidered.Should().Be(2);
        result.ModulesCreatedAndApproved.Should().Be(1);
        result.Items.Should().Contain(i => !i.Success && i.ErrorMessage != null);
    }
}

/// <summary>Never routed to — gap_fill (this test suite's only activity type) is not in
/// LessonExerciseBatchGenerationService's AI-preferred set. Throws if ever called, so a future
/// change accidentally routing gap_fill through AI would fail these tests loudly.</summary>
internal sealed class NeverCalledAiActivityHandler : IGenerateActivityFromLessonWithAiHandler
{
    public Task<GenerateExerciseResult> HandleAsync(GenerateActivityFromLessonRequest request, CancellationToken ct = default) =>
        throw new InvalidOperationException("NeverCalledAiActivityHandler was called — gap_fill should never route through AI.");
}

/// <summary>Test double for <see cref="IModuleSkillGraphTaggingService"/> — matches
/// <see cref="NodeKeyToMatch"/> against the request's candidate list (real validation-against-
/// candidate-list behavior preserved), returns no matches otherwise.</summary>
internal sealed class FakeModuleSkillGraphTaggingService : IModuleSkillGraphTaggingService
{
    public string? NodeKeyToMatch { get; set; }

    public Task<ModuleSkillGraphTaggingResult> ProposeCoverageAsync(
        ModuleSkillGraphTaggingRequest request, CancellationToken ct = default)
    {
        var candidate = NodeKeyToMatch is not null
            ? request.CandidateNodes.FirstOrDefault(c => c.Key == NodeKeyToMatch)
            : null;

        var matches = candidate is not null
            ? new List<ModuleSkillGraphNodeMatch> { new(candidate.Id, 0.9) }
            : [];

        return Task.FromResult(new ModuleSkillGraphTaggingResult(true, matches, null));
    }
}
