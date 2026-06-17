using FluentAssertions;
using LinguaCoach.Application.PracticeGym;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.PracticeGym;
using LinguaCoach.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.PracticeGym;

/// <summary>
/// Unit tests for PracticeGymSuggestionService — Phase 10O.
/// Uses an in-memory database and a stub replenishment service.
/// </summary>
public sealed class PracticeGymSuggestionServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly StubReplenishmentService _replenishment;
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
        _replenishment = new StubReplenishmentService { TargetCount = 10, ReadyCount = 0 };
        _sut = new PracticeGymSuggestionService(
            _db, _replenishment, NullLogger<PracticeGymSuggestionService>.Instance);
    }

    // 1. Consumed items excluded from all sections.
    [Fact]
    public async Task GetSuggestions_ExcludesConsumedItems()
    {
        SeedItem(status: ReadinessPoolStatus.Consumed);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().BeEmpty();
        result.ContinueItems.Should().BeEmpty();
        result.ReviewItems.Should().BeEmpty();
    }

    // 2. Expired items excluded.
    [Fact]
    public async Task GetSuggestions_ExcludesExpiredItems()
    {
        SeedItem(status: ReadinessPoolStatus.Expired);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 3. Failed items excluded.
    [Fact]
    public async Task GetSuggestions_ExcludesFailedItems()
    {
        SeedItem(status: ReadinessPoolStatus.Failed);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 4. Stale items excluded.
    [Fact]
    public async Task GetSuggestions_ExcludesStaleItems()
    {
        SeedItem(status: ReadinessPoolStatus.Stale);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 5. ReviewOnly status item appears in Review section only, not Suggested.
    [Fact]
    public async Task GetSuggestions_ReviewOnlyAppearsInReviewSectionOnly()
    {
        SeedItem(status: ReadinessPoolStatus.ReviewOnly, routingReason: RoutingReason.Review, isLower: true);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ReviewItems.Should().HaveCount(1);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 6. Lower-level Ready+Review items appear in Review, not Suggested.
    [Fact]
    public async Task GetSuggestions_LowerLevelReadyReviewAppearsInReviewOnly()
    {
        SeedItem(status: ReadinessPoolStatus.Ready, routingReason: RoutingReason.Review, isLower: true);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ReviewItems.Should().HaveCount(1);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 7. Normal Ready item appears in Suggested section.
    [Fact]
    public async Task GetSuggestions_NormalReadyItemAppearsInSuggested()
    {
        SeedItem(status: ReadinessPoolStatus.Ready, routingReason: RoutingReason.Normal, isLower: false);
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.SuggestedItems.Should().HaveCount(1);
        result.ReviewItems.Should().BeEmpty();
    }

    // 8. Reserved valid item appears in Continue section.
    [Fact]
    public async Task GetSuggestions_ReservedValidItemInContinueSection()
    {
        SeedItem(status: ReadinessPoolStatus.Reserved, expiresAt: DateTime.UtcNow.AddHours(2));
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ContinueItems.Should().HaveCount(1);
        result.SuggestedItems.Should().BeEmpty();
    }

    // 9. Reserved item with past ExpiresAt NOT in Continue.
    [Fact]
    public async Task GetSuggestions_ExpiredReservedItemNotInContinue()
    {
        SeedItem(status: ReadinessPoolStatus.Reserved, expiresAt: DateTime.UtcNow.AddHours(-1));
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.ContinueItems.Should().BeEmpty();
    }

    // 10. Empty pool → IsReplenishmentRecommended = true.
    [Fact]
    public async Task GetSuggestions_EmptyPool_ReplenishmentRecommended()
    {
        var result = await _sut.GetSuggestionsForStudentAsync(StudentId);
        result.IsReplenishmentRecommended.Should().BeTrue();
        result.SuggestedItems.Should().BeEmpty();
    }

    // 11. StartSuggestion reserves a Ready item and returns linked activity id.
    [Fact]
    public async Task StartSuggestion_ReservesReadyItem()
    {
        var activityId = Guid.NewGuid();
        var item = SeedItem(status: ReadinessPoolStatus.Ready, learningActivityId: activityId);

        var result = await _sut.StartSuggestionAsync(StudentId, item.Id);

        result.Success.Should().BeTrue();
        result.AlreadyReserved.Should().BeFalse();
        result.LearningActivityId.Should().Be(activityId);

        var dbItem = await _db.StudentActivityReadinessItems.FindAsync(item.Id);
        dbItem!.Status.Should().Be(ReadinessPoolStatus.Reserved);
    }

    // 12. StartSuggestion is idempotent for already-reserved items.
    [Fact]
    public async Task StartSuggestion_AlreadyReserved_ReturnsAlreadyReserved()
    {
        var activityId = Guid.NewGuid();
        var item = SeedItem(status: ReadinessPoolStatus.Reserved, learningActivityId: activityId);

        var result = await _sut.StartSuggestionAsync(StudentId, item.Id);

        result.Success.Should().BeTrue();
        result.AlreadyReserved.Should().BeTrue();
        result.LearningActivityId.Should().Be(activityId);
    }

    // 13. StartSuggestion returns failure for consumed item (not available).
    [Fact]
    public async Task StartSuggestion_ConsumedItem_ReturnsFailure()
    {
        var item = SeedItem(status: ReadinessPoolStatus.Consumed);

        var result = await _sut.StartSuggestionAsync(StudentId, item.Id);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().NotBeNullOrEmpty();
    }

    // 14. TryMarkConsumed transitions Reserved → Consumed.
    [Fact]
    public async Task TryMarkConsumed_ReservedItem_BecomesConsumed()
    {
        var item = SeedItem(status: ReadinessPoolStatus.Reserved);

        await _sut.TryMarkConsumedAsync(StudentId, item.Id);

        var dbItem = await _db.StudentActivityReadinessItems.FindAsync(item.Id);
        dbItem!.Status.Should().Be(ReadinessPoolStatus.Consumed);
    }

    // --- helpers ---

    private StudentActivityReadinessItem SeedItem(
        ReadinessPoolStatus status = ReadinessPoolStatus.Ready,
        RoutingReason routingReason = RoutingReason.Normal,
        bool isLower = false,
        DateTime? expiresAt = null,
        Guid? learningActivityId = null)
    {
        var item = new StudentActivityReadinessItem(
            studentId: StudentId,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B2",
            routingReason: routingReason,
            isLowerLevelContent: isLower,
            expiresAt: expiresAt);

        ForceStatus(item, status);
        if (learningActivityId.HasValue) ForceLinkedActivity(item, learningActivityId.Value);

        _db.StudentActivityReadinessItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    private static void ForceStatus(StudentActivityReadinessItem item, ReadinessPoolStatus status) =>
        typeof(StudentActivityReadinessItem)
            .GetProperty(nameof(StudentActivityReadinessItem.Status))!
            .SetValue(item, status);

    private static void ForceLinkedActivity(StudentActivityReadinessItem item, Guid activityId) =>
        typeof(StudentActivityReadinessItem)
            .GetProperty(nameof(StudentActivityReadinessItem.LearningActivityId))!
            .SetValue(item, activityId);

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}

/// <summary>Stub replenishment service for unit tests — returns configurable health.</summary>
internal sealed class StubReplenishmentService : IReadinessPoolReplenishmentService
{
    public int TargetCount { get; set; } = 10;
    public int ReadyCount { get; set; }

    public Task<ReplenishmentRunSummary> RunAsync(CancellationToken ct = default) =>
        Task.FromResult(new ReplenishmentRunSummary());

    public Task<PoolHealthSummary> GetHealthAsync(
        Guid studentId, ReadinessPoolSource source, CancellationToken ct = default) =>
        Task.FromResult(new PoolHealthSummary
        {
            StudentId   = studentId,
            Source      = source,
            ReadyCount  = ReadyCount,
            TargetCount = TargetCount
        });
}
