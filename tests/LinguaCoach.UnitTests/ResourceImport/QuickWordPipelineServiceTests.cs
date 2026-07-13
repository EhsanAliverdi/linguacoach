using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Exercises;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Infrastructure.Modules;
using LinguaCoach.Infrastructure.Onboarding;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase K3 — "one word in, one Resource Bank item + one Lesson + one Exercise + one Module out."
/// Wires the real generation/approve handlers directly (no DI container) against SQLite
/// in-memory, matching this test suite's convention elsewhere (see ModuleGenerationServiceTests).
/// </summary>
public sealed class QuickWordPipelineServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly QuickWordPipelineService _sut;

    public QuickWordPipelineServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new QuickWordPipelineService(
            _db,
            new LessonGenerationService(_db),
            new AdminApproveLessonHandler(_db),
            new ActivityGenerationService(_db, new FormIoSchemaValidationService()),
            new AdminApproveExerciseHandler(_db),
            new ModuleGenerationService(_db));
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task Running_the_pipeline_for_one_word_creates_exactly_one_of_each_artifact()
    {
        var result = await _sut.RunAsync(new QuickWordPipelineRequest("serendipity", "B2", "noun", "a fortunate discovery by chance"));

        result.ResourceBankItemId.Should().NotBeEmpty();
        result.LessonId.Should().NotBeEmpty();
        result.ExerciseId.Should().NotBeEmpty();
        result.ModuleId.Should().NotBeEmpty();

        (await _db.ResourceBankItems.CountAsync(x => x.Type == PublishedResourceType.Vocabulary)).Should().Be(1);
        (await _db.Lessons.CountAsync()).Should().Be(1);
        (await _db.Exercises.CountAsync()).Should().Be(1);
        (await _db.Modules.CountAsync()).Should().Be(1);

        var lesson = await _db.Lessons.FirstAsync(l => l.Id == result.LessonId);
        lesson.ReviewStatus.Should().Be(AdminReviewStatus.Approved);

        var exercise = await _db.Exercises.FirstAsync(e => e.Id == result.ExerciseId);
        exercise.ReviewStatus.Should().Be(AdminReviewStatus.Approved);

        // The Module itself is left PendingReview — the pipeline generates it, it doesn't
        // rubber-stamp the final artifact for the admin.
        var module = await _db.Modules.FirstAsync(m => m.Id == result.ModuleId);
        module.ReviewStatus.Should().Be(AdminReviewStatus.PendingReview);

        var resourceItem = await _db.ResourceBankItems.FirstAsync(r => r.Id == result.ResourceBankItemId);
        resourceItem.CefrLevel.Should().Be("B2");
    }

    [Fact]
    public async Task Invalid_cefr_level_is_rejected_before_anything_is_created()
    {
        Func<Task> act = async () => await _sut.RunAsync(new QuickWordPipelineRequest("word", "NotALevel"));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
        (await _db.ResourceBankItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Blank_word_is_rejected()
    {
        Func<Task> act = async () => await _sut.RunAsync(new QuickWordPipelineRequest("   ", "A1"));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }

    [Fact]
    public async Task Running_the_pipeline_twice_reuses_the_same_quick_word_source_and_creates_two_independent_cascades()
    {
        await _sut.RunAsync(new QuickWordPipelineRequest("alpha", "A1"));
        await _sut.RunAsync(new QuickWordPipelineRequest("beta", "A2"));

        (await _db.CefrResourceSources.CountAsync(s => s.Name == "Admin Quick Word Pipeline")).Should().Be(1);
        (await _db.ResourceBankItems.CountAsync()).Should().Be(2);
        (await _db.Lessons.CountAsync()).Should().Be(2);
        (await _db.Exercises.CountAsync()).Should().Be(2);
        (await _db.Modules.CountAsync()).Should().Be(2);
    }
}
