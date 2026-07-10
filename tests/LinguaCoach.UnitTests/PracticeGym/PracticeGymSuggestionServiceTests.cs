using FluentAssertions;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.PracticeGymModules;
using LinguaCoach.Infrastructure.PracticeGym;
using LinguaCoach.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.PracticeGym;

/// <summary>
/// Unit tests for PracticeGymSuggestionService.
///
/// Phase I2A (legacy fallback deletion): SuggestedItems/ContinueItems/ReviewItems no longer
/// read the readiness pool for Practice-Gym-sourced rows — that generation path (including the
/// Phase 19C review/scaffold pilot gate and the Phase 20H dedupe logic previously covered here)
/// was removed. Those three lists are now always empty. See
/// docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.
///
/// Phase I2C: the readiness pool itself (StudentActivityReadinessItem,
/// IReadinessPoolReplenishmentService) was deleted. StartSuggestionAsync/TryMarkConsumedAsync are
/// now permanently no-ops (see class doc comment on PracticeGymSuggestionService), and
/// ReadyCount/ReviewOnlyCount/IsReplenishmentRecommended are hardcoded to 0/false. Tests below
/// were rewritten accordingly — see
/// docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
/// </summary>
public sealed class PracticeGymSuggestionServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly PracticeGymSuggestionService _sut;

    private static readonly Guid StudentId = Guid.NewGuid();
    private readonly SqliteConnection _connection;

    public PracticeGymSuggestionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.EnsureCreated();
        _sut = new PracticeGymSuggestionService(
            _db,
            new FallbackOnlyModuleSelectionService(), new NoOpModuleAssignmentRecorder(),
            NullLogger<PracticeGymSuggestionService>.Instance);
    }

    // Phase H7 — stubs so these tests are unaffected by the additive Practice Gym module
    // pipeline (always reports FallbackRequired, records nothing).
    private sealed class FallbackOnlyModuleSelectionService : IPracticeGymModuleSelectionService
    {
        public Task<PracticeGymModuleSelectionResult> SelectAsync(
            PracticeGymModuleSelectionRequest request, CancellationToken ct = default) =>
            Task.FromResult(new PracticeGymModuleSelectionResult([], true, "No modules configured for this test.", null, null, []));
    }

    private sealed class NoOpModuleAssignmentRecorder : IPracticeGymModuleAssignmentRecorder
    {
        public Task RecordAsync(Guid studentId, PracticeGymModuleSelectionResult selectionResult, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task GetSuggestions_SuggestedContinueReviewLists_AreAlwaysEmpty()
    {
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().BeEmpty();
        result.ContinueItems.Should().BeEmpty();
        result.ReviewItems.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSuggestions_ReadyReviewOnlyReservedCounts_AreAlwaysZero()
    {
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ReadyCount.Should().Be(0);
        result.ReviewOnlyCount.Should().Be(0);
        result.ReservedCount.Should().Be(0);
    }

    [Fact]
    public async Task GetSuggestions_IsReplenishmentRecommended_IsAlwaysFalse()
    {
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.IsReplenishmentRecommended.Should().BeFalse();
    }

    [Fact]
    public async Task GetSuggestions_ModuleSuggestions_IsPopulatedFromModuleSelector()
    {
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ModuleSuggestions.Should().NotBeNull();
    }

    // StartSuggestionAsync/TryMarkConsumedAsync are permanently no-ops now that the readiness
    // pool is gone — no readinessItemId a caller passes in can ever refer to a real row.

    [Fact]
    public async Task StartSuggestion_IsAlwaysNotFound()
    {
        var result = await _sut.StartSuggestionAsync(StudentId, Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("Item not found.");
    }

    [Fact]
    public async Task TryMarkConsumed_DoesNotThrow()
    {
        await _sut.TryMarkConsumedAsync(StudentId, Guid.NewGuid());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
