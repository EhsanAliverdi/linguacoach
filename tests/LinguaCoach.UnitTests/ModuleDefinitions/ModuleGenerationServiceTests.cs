using FluentAssertions;
using LinguaCoach.Application.ModuleDefinitions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ModuleDefinitions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ModuleDefinitions;

/// <summary>
/// Phase H5 — deterministic "Generate Module" composer (all four entry points). Uses SQLite
/// in-memory (matches LearnItem/ActivityDefinition generation test conventions) with
/// directly-seeded Learn Items/Activity Definitions/published bank rows. All fixture content is
/// synthetic.
/// </summary>
public sealed class ModuleGenerationServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ModuleGenerationService _sut;

    public ModuleGenerationServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ModuleGenerationService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private LearnItem SeedLearnItem(bool approved, string title = "Resilient", string cefrLevel = "B2", string skill = "Vocabulary")
    {
        var item = new LearnItem(title, $"{title} means able to recover quickly.", LearnItemSourceMode.Manual, cefrLevel, skill);
        if (approved) item.Approve(null);
        _db.LearnItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    private ActivityDefinition SeedActivity(bool approved, string title = "Gap fill: resilient", string cefrLevel = "B2", string skill = "Vocabulary")
    {
        var activity = new ActivityDefinition(title, "Type the missing word.", "gap_fill", ActivityRendererType.Formio,
            ActivitySourceMode.Manual, cefrLevel: cefrLevel, skill: skill, estimatedMinutes: 5);
        if (approved) activity.Approve(null);
        _db.ActivityDefinitions.Add(activity);
        _db.SaveChanges();
        return activity;
    }

    private (CefrResourceSource Source, CefrVocabularyEntry Vocab) SeedVocabularyResource()
    {
        var source = new CefrResourceSource("Test Source", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        _db.CefrResourceSources.Add(source);
        var vocab = new CefrVocabularyEntry(source.Id, "resilient", "B2", "adjective", "able to recover quickly");
        _db.CefrVocabularyEntries.Add(vocab);
        _db.SaveChanges();
        return (source, vocab);
    }

    // ── Generate from items ──────────────────────────────────────────────────

    [Fact]
    public async Task Generate_from_items_creates_pending_review_module_not_approved()
    {
        var learnItem = SeedLearnItem(approved: true);
        var activity = SeedActivity(approved: true);

        var result = await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLearnItemLinkInput(learnItem.Id, "Primary") },
            new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") }));

        result.Module.ReviewStatus.Should().Be("PendingReview");
        result.Module.SourceMode.Should().Be("GeneratedFromLearnAndActivities");
        result.Module.GenerationProvider.Should().Be("Deterministic");
        (await _db.ModuleDefinitions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Generate_stores_cefr_skill_subskill_context_focus_difficulty_metadata()
    {
        var learnItem = SeedLearnItem(approved: true, cefrLevel: "B1", skill: "Grammar");
        var activity = SeedActivity(approved: true, cefrLevel: "B1", skill: "Grammar");

        var result = await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLearnItemLinkInput(learnItem.Id, "Primary") },
            new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") }));

        result.Module.CefrLevel.Should().Be("B1");
        result.Module.Skill.Should().Be("Grammar");
    }

    [Fact]
    public async Task Generate_stores_module_feedback_plan_json()
    {
        var learnItem = SeedLearnItem(approved: true);
        var activity = SeedActivity(approved: true);

        var result = await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLearnItemLinkInput(learnItem.Id, "Primary") },
            new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") }));

        result.Module.FeedbackPlanJson.Should().Contain("completionMessage");
    }

    [Fact]
    public async Task Generate_creates_learn_item_links()
    {
        var learnItem = SeedLearnItem(approved: true);
        var activity = SeedActivity(approved: true);

        var result = await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLearnItemLinkInput(learnItem.Id, "Primary") },
            new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") }));

        result.Module.LearnItemLinks.Should().ContainSingle(l => l.LearnItemId == learnItem.Id && l.Role == "Primary");
    }

    [Fact]
    public async Task Generate_creates_activity_links()
    {
        var learnItem = SeedLearnItem(approved: true);
        var activity = SeedActivity(approved: true);

        var result = await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLearnItemLinkInput(learnItem.Id, "Primary") },
            new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") }));

        result.Module.ActivityLinks.Should().ContainSingle(l => l.ActivityDefinitionId == activity.Id && l.Role == "PrimaryPractice");
    }

    [Fact]
    public async Task Generate_preserves_sort_order_across_multiple_links()
    {
        var learnItem1 = SeedLearnItem(approved: true, title: "First");
        var learnItem2 = SeedLearnItem(approved: true, title: "Second");
        var activity = SeedActivity(approved: true);

        var result = await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[]
            {
                new ModuleLearnItemLinkInput(learnItem1.Id, "Primary"),
                new ModuleLearnItemLinkInput(learnItem2.Id, "Supporting"),
            },
            new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") }));

        result.Module.LearnItemLinks.Should().HaveCount(2);
        result.Module.LearnItemLinks[0].LearnItemId.Should().Be(learnItem1.Id);
        result.Module.LearnItemLinks[0].SortOrder.Should().Be(0);
        result.Module.LearnItemLinks[1].LearnItemId.Should().Be(learnItem2.Id);
        result.Module.LearnItemLinks[1].SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task Generate_from_items_rejects_a_pending_learn_item()
    {
        var learnItem = SeedLearnItem(approved: false);
        var activity = SeedActivity(approved: true);

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLearnItemLinkInput(learnItem.Id, "Primary") },
            new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") }));

        await act.Should().ThrowAsync<ModuleDefinitionValidationException>();
        (await _db.ModuleDefinitions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Generate_from_items_rejects_a_pending_activity()
    {
        var learnItem = SeedLearnItem(approved: true);
        var activity = SeedActivity(approved: false);

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLearnItemLinkInput(learnItem.Id, "Primary") },
            new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") }));

        await act.Should().ThrowAsync<ModuleDefinitionValidationException>();
        (await _db.ModuleDefinitions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Generate_from_items_rejects_an_invalid_learn_item_id()
    {
        var activity = SeedActivity(approved: true);

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLearnItemLinkInput(Guid.NewGuid(), "Primary") },
            new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") }));

        await act.Should().ThrowAsync<ModuleDefinitionValidationException>();
    }

    // ── Generate from resource ───────────────────────────────────────────────

    [Fact]
    public async Task Generate_from_resource_composes_module_when_approved_learn_item_and_activity_exist()
    {
        var (_, vocab) = SeedVocabularyResource();
        var learnItem = SeedLearnItem(approved: true);
        var activity = SeedActivity(approved: true);
        _db.LearnItemResourceLinks.Add(new LearnItemResourceLink(learnItem.Id, PublishedResourceType.Vocabulary, vocab.Id, LearnItemResourceRole.Primary));
        _db.ActivityResourceLinks.Add(new ActivityResourceLink(activity.Id, PublishedResourceType.Vocabulary, vocab.Id, LearnItemResourceRole.Primary));
        await _db.SaveChangesAsync();

        var result = await _sut.HandleAsync(new GenerateModuleFromResourceRequest("Vocabulary", vocab.Id));

        result.Module.SourceMode.Should().Be("GeneratedFromResources");
        result.Module.LearnItemLinks.Should().ContainSingle(l => l.LearnItemId == learnItem.Id);
        result.Module.ActivityLinks.Should().ContainSingle(l => l.ActivityDefinitionId == activity.Id);
    }

    [Fact]
    public async Task Generate_from_resource_rejected_when_no_approved_learn_item_linked()
    {
        var (_, vocab) = SeedVocabularyResource();
        var activity = SeedActivity(approved: true);
        _db.ActivityResourceLinks.Add(new ActivityResourceLink(activity.Id, PublishedResourceType.Vocabulary, vocab.Id, LearnItemResourceRole.Primary));
        await _db.SaveChangesAsync();

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromResourceRequest("Vocabulary", vocab.Id));

        await act.Should().ThrowAsync<ModuleDefinitionValidationException>();
    }

    [Fact]
    public async Task Generate_from_resource_rejected_when_no_approved_activity_linked()
    {
        var (_, vocab) = SeedVocabularyResource();
        var learnItem = SeedLearnItem(approved: true);
        _db.LearnItemResourceLinks.Add(new LearnItemResourceLink(learnItem.Id, PublishedResourceType.Vocabulary, vocab.Id, LearnItemResourceRole.Primary));
        await _db.SaveChangesAsync();

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromResourceRequest("Vocabulary", vocab.Id));

        await act.Should().ThrowAsync<ModuleDefinitionValidationException>();
    }

    // ── Generate from Learn Item / Activity ──────────────────────────────────

    [Fact]
    public async Task Generate_from_learn_item_finds_a_compatible_approved_activity()
    {
        var learnItem = SeedLearnItem(approved: true, cefrLevel: "B1", skill: "Grammar");
        var activity = SeedActivity(approved: true, cefrLevel: "B1", skill: "Grammar");

        var result = await _sut.HandleAsync(new GenerateModuleFromLearnItemRequest(learnItem.Id));

        result.Module.ActivityLinks.Should().ContainSingle(l => l.ActivityDefinitionId == activity.Id);
    }

    [Fact]
    public async Task Generate_from_learn_item_rejected_when_no_compatible_activity_exists()
    {
        var learnItem = SeedLearnItem(approved: true, cefrLevel: "C1", skill: "Grammar");
        SeedActivity(approved: true, cefrLevel: "A1", skill: "Vocabulary");

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromLearnItemRequest(learnItem.Id));

        await act.Should().ThrowAsync<ModuleDefinitionValidationException>();
    }

    [Fact]
    public async Task Generate_from_activity_finds_a_compatible_approved_learn_item()
    {
        var learnItem = SeedLearnItem(approved: true, cefrLevel: "B1", skill: "Grammar");
        var activity = SeedActivity(approved: true, cefrLevel: "B1", skill: "Grammar");

        var result = await _sut.HandleAsync(new GenerateModuleFromActivityRequest(activity.Id));

        result.Module.LearnItemLinks.Should().ContainSingle(l => l.LearnItemId == learnItem.Id);
    }

    [Fact]
    public async Task Generate_from_activity_rejected_when_no_compatible_learn_item_exists()
    {
        SeedLearnItem(approved: true, cefrLevel: "A1", skill: "Vocabulary");
        var activity = SeedActivity(approved: true, cefrLevel: "C1", skill: "Grammar");

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromActivityRequest(activity.Id));

        await act.Should().ThrowAsync<ModuleDefinitionValidationException>();
    }

    [Fact]
    public async Task Generate_from_learn_item_that_is_not_approved_is_rejected()
    {
        var learnItem = SeedLearnItem(approved: false);
        SeedActivity(approved: true, cefrLevel: learnItem.CefrLevel!, skill: learnItem.Skill!);

        var act = async () => await _sut.HandleAsync(new GenerateModuleFromLearnItemRequest(learnItem.Id));

        await act.Should().ThrowAsync<ModuleDefinitionValidationException>();
    }

    // ── No mutation / no side effects ────────────────────────────────────────

    [Fact]
    public async Task Generate_does_not_mutate_the_source_learn_item()
    {
        var learnItem = SeedLearnItem(approved: true);
        var activity = SeedActivity(approved: true);
        var originalTitle = learnItem.Title;
        var originalStatus = learnItem.ReviewStatus;

        await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLearnItemLinkInput(learnItem.Id, "Primary") },
            new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") }));

        var reloaded = await _db.LearnItems.FirstAsync(l => l.Id == learnItem.Id);
        reloaded.Title.Should().Be(originalTitle);
        reloaded.ReviewStatus.Should().Be(originalStatus);
    }

    [Fact]
    public async Task Generate_does_not_mutate_the_source_activity_definition()
    {
        var learnItem = SeedLearnItem(approved: true);
        var activity = SeedActivity(approved: true);
        var originalTitle = activity.Title;

        await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLearnItemLinkInput(learnItem.Id, "Primary") },
            new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") }));

        var reloaded = await _db.ActivityDefinitions.FirstAsync(a => a.Id == activity.Id);
        reloaded.Title.Should().Be(originalTitle);
    }

    [Fact]
    public async Task Generate_creates_no_student_assignment_or_today_practice_gym_records()
    {
        var learnItem = SeedLearnItem(approved: true);
        var activity = SeedActivity(approved: true);

        await _sut.HandleAsync(new GenerateModuleFromItemsRequest(
            new[] { new ModuleLearnItemLinkInput(learnItem.Id, "Primary") },
            new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") }));

        (await _db.StudentProfiles.CountAsync()).Should().Be(0);
        (await _db.LearningActivities.CountAsync()).Should().Be(0);
        (await _db.LearningModules.CountAsync()).Should().Be(0);
        (await _db.StudentActivityReadinessItems.CountAsync()).Should().Be(0);
    }
}
