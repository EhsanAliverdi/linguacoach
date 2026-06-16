using LinguaCoach.Application.Memory;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Memory;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.IntegrationTests.Persistence;

/// <summary>
/// Integration tests for StudentLearningLedgerService.
/// Verifies that learning events are written, queried, and ordered correctly.
/// Uses SQLite in-memory to verify actual DB writes without a full web stack.
/// </summary>
public sealed class StudentLearningLedgerServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly IStudentLearningLedger _sut;
    private readonly Guid _studentProfileId;

    public StudentLearningLedgerServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new StudentLearningLedgerService(_db, NullLogger<StudentLearningLedgerService>.Instance);

        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        _db.SaveChanges();
        _studentProfileId = student.Id;
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task RecordAsync_WritesEventToDatabase()
    {
        var evt = new StudentLearningEvent(
            studentProfileId: _studentProfileId,
            source: LearningEventSource.PracticeGym,
            outcome: LearningEventOutcome.Practised,
            patternKey: "email_reply",
            exerciseType: "WritingScenario",
            primarySkill: "writing",
            score: 72.0,
            normalizedScore: 0.72);

        await _sut.RecordAsync(evt);

        var saved = await _db.StudentLearningEvents
            .FirstOrDefaultAsync(e => e.StudentProfileId == _studentProfileId);

        Assert.NotNull(saved);
        Assert.Equal(LearningEventSource.PracticeGym, saved.Source);
        Assert.Equal(LearningEventOutcome.Practised, saved.Outcome);
        Assert.Equal("email_reply", saved.PatternKey);
        Assert.Equal("writing", saved.PrimarySkill);
        Assert.Equal(72.0, saved.Score);
        Assert.Equal(0.72, saved.NormalizedScore);
    }

    [Fact]
    public async Task RecordAsync_NullOptionalFields_DoesNotFail()
    {
        var evt = new StudentLearningEvent(
            studentProfileId: _studentProfileId,
            source: LearningEventSource.PracticeGym,
            outcome: LearningEventOutcome.Skipped);

        await _sut.RecordAsync(evt);

        var saved = await _db.StudentLearningEvents
            .FirstOrDefaultAsync(e => e.StudentProfileId == _studentProfileId);

        Assert.NotNull(saved);
        Assert.Null(saved.PatternKey);
        Assert.Null(saved.PrimarySkill);
        Assert.Null(saved.Score);
        Assert.Null(saved.CefrLevelAtEvent);
        Assert.Null(saved.MistakeTagsJson);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestFirst()
    {
        for (int i = 0; i < 3; i++)
        {
            var evt = new StudentLearningEvent(
                studentProfileId: _studentProfileId,
                source: LearningEventSource.PracticeGym,
                outcome: LearningEventOutcome.Practised,
                patternKey: $"pattern_{i}");
            await _sut.RecordAsync(evt);
            await Task.Delay(10); // ensure distinct timestamps
        }

        var results = await _sut.GetRecentAsync(_studentProfileId, limit: 10);

        Assert.Equal(3, results.Count);
        // newest first
        Assert.Equal("pattern_2", results[0].PatternKey);
        Assert.Equal("pattern_0", results[2].PatternKey);
    }

    [Fact]
    public async Task GetRecentAsync_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
        {
            var evt = new StudentLearningEvent(
                studentProfileId: _studentProfileId,
                source: LearningEventSource.PracticeGym,
                outcome: LearningEventOutcome.Practised);
            await _sut.RecordAsync(evt);
        }

        var results = await _sut.GetRecentAsync(_studentProfileId, limit: 3);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task GetRecentPatternKeysAsync_ReturnsDistinctKeysNewestFirst()
    {
        foreach (var key in new[] { "gap_fill", "email_reply", "gap_fill", "phrase_match" })
        {
            var evt = new StudentLearningEvent(
                studentProfileId: _studentProfileId,
                source: LearningEventSource.PracticeGym,
                outcome: LearningEventOutcome.Practised,
                patternKey: key);
            await _sut.RecordAsync(evt);
            await Task.Delay(10);
        }

        var keys = await _sut.GetRecentPatternKeysAsync(_studentProfileId, limit: 10);

        // gap_fill appears twice but should deduplicate
        Assert.Contains("gap_fill", keys);
        Assert.Contains("email_reply", keys);
        Assert.Contains("phrase_match", keys);
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public async Task GetWeakEventsAsync_ReturnsOnlyFailedAndNeedsReview()
    {
        var outcomes = new[]
        {
            LearningEventOutcome.Practised,
            LearningEventOutcome.NeedsReview,
            LearningEventOutcome.Failed,
            LearningEventOutcome.Mastered
        };

        foreach (var outcome in outcomes)
        {
            var evt = new StudentLearningEvent(
                studentProfileId: _studentProfileId,
                source: LearningEventSource.PracticeGym,
                outcome: outcome);
            await _sut.RecordAsync(evt);
        }

        var weak = await _sut.GetWeakEventsAsync(_studentProfileId);

        Assert.Equal(2, weak.Count);
        Assert.All(weak, e => Assert.True(
            e.Outcome == LearningEventOutcome.NeedsReview ||
            e.Outcome == LearningEventOutcome.Failed));
    }

    [Fact]
    public async Task GetRecentByPatternKeysAsync_FiltersToGivenKeys()
    {
        foreach (var key in new[] { "email_reply", "phrase_match", "teams_chat_simulation" })
        {
            var evt = new StudentLearningEvent(
                studentProfileId: _studentProfileId,
                source: LearningEventSource.PracticeGym,
                outcome: LearningEventOutcome.Practised,
                patternKey: key);
            await _sut.RecordAsync(evt);
        }

        var results = await _sut.GetRecentByPatternKeysAsync(
            _studentProfileId,
            new[] { "email_reply", "teams_chat_simulation" });

        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.NotEqual("phrase_match", e.PatternKey));
    }

    [Fact]
    public async Task GetRecentAsync_IsolatesEventsByStudentProfile()
    {
        var otherStudent = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(otherStudent);
        await _db.SaveChangesAsync();

        await _sut.RecordAsync(new StudentLearningEvent(
            studentProfileId: _studentProfileId,
            source: LearningEventSource.PracticeGym,
            outcome: LearningEventOutcome.Practised,
            patternKey: "email_reply"));

        await _sut.RecordAsync(new StudentLearningEvent(
            studentProfileId: otherStudent.Id,
            source: LearningEventSource.PracticeGym,
            outcome: LearningEventOutcome.Practised,
            patternKey: "phrase_match"));

        var results = await _sut.GetRecentAsync(_studentProfileId);

        Assert.Single(results);
        Assert.Equal("email_reply", results[0].PatternKey);
    }

    [Fact]
    public async Task RecordAsync_TodayLessonSource_StoresSourceCorrectly()
    {
        var sessionId = Guid.NewGuid();
        var evt = new StudentLearningEvent(
            studentProfileId: _studentProfileId,
            source: LearningEventSource.TodayLesson,
            outcome: LearningEventOutcome.Practised,
            sessionId: sessionId,
            patternKey: "listen_and_answer");

        await _sut.RecordAsync(evt);

        var saved = await _db.StudentLearningEvents.FirstAsync(
            e => e.StudentProfileId == _studentProfileId);

        Assert.Equal(LearningEventSource.TodayLesson, saved.Source);
        Assert.Equal(sessionId, saved.SessionId);
    }

    [Fact]
    public async Task RecordAsync_NoWorkplaceDefaultForcedWhenContextNull()
    {
        var evt = new StudentLearningEvent(
            studentProfileId: _studentProfileId,
            source: LearningEventSource.PracticeGym,
            outcome: LearningEventOutcome.Practised,
            learningGoalContext: null);

        await _sut.RecordAsync(evt);

        var saved = await _db.StudentLearningEvents.FirstAsync(
            e => e.StudentProfileId == _studentProfileId);

        Assert.Null(saved.LearningGoalContext);
    }
}
