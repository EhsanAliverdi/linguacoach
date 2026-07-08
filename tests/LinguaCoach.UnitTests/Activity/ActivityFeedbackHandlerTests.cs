using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Activity;

/// <summary>
/// Uses SQLite in-memory so DB writes/queries (upsert, provenance backfill, ownership checks)
/// are tested without a full web stack. Phase B2 — Activity feedback signals.
/// </summary>
public sealed class ActivityFeedbackHandlerTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ActivityFeedbackHandler _sut;

    public ActivityFeedbackHandlerTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ActivityFeedbackHandler(_db, NullLogger<ActivityFeedbackHandler>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private (Guid UserId, Guid ProfileId) SeedStudent()
    {
        var userId = Guid.NewGuid();
        var student = new StudentProfile(userId);
        _db.StudentProfiles.Add(student);
        _db.SaveChanges();
        return (userId, student.Id);
    }

    private Guid SeedActivity()
    {
        var activity = new LearningActivity(
            ActivityType.WritingScenario, ActivitySource.AiGenerated, "Title", "B1", "{}");
        _db.LearningActivities.Add(activity);
        _db.SaveChanges();
        return activity.Id;
    }

    private Guid SeedAttempt(Guid studentProfileId, Guid activityId)
    {
        var attempt = new ActivityAttempt(studentProfileId, activityId, "content", "{}", "prompt_key");
        _db.ActivityAttempts.Add(attempt);
        _db.SaveChanges();
        return attempt.Id;
    }

    private static SubmitActivityFeedbackCommand BuildCommand(
        Guid userId, Guid activityId, Guid? attemptId, string? comment = null) => new(
        UserId: userId,
        LearningActivityId: activityId,
        DifficultyRating: ActivityFeedbackDifficultyRating.RightLevel,
        ClarityRating: ActivityFeedbackClarityRating.Clear,
        UsefulnessRating: ActivityFeedbackUsefulnessRating.Useful,
        RepeatPreference: ActivityFeedbackRepeatPreference.MoreLikeThis,
        ActivityAttemptId: attemptId,
        OptionalComment: comment);

    [Fact]
    public async Task SubmitFeedback_ForCompletedActivity_ReturnsExpectedDto()
    {
        var (userId, profileId) = SeedStudent();
        var activityId = SeedActivity();
        var attemptId = SeedAttempt(profileId, activityId);

        var result = await _sut.HandleAsync(BuildCommand(userId, activityId, attemptId));

        result.LearningActivityId.Should().Be(activityId);
        result.ActivityAttemptId.Should().Be(attemptId);
        result.DifficultyRating.Should().Be(ActivityFeedbackDifficultyRating.RightLevel);
        result.ClarityRating.Should().Be(ActivityFeedbackClarityRating.Clear);
        result.UsefulnessRating.Should().Be(ActivityFeedbackUsefulnessRating.Useful);
        result.RepeatPreference.Should().Be(ActivityFeedbackRepeatPreference.MoreLikeThis);

        (await _db.ActivityFeedbackSignals.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SubmitFeedback_Twice_ForSameAttempt_UpdatesInsteadOfDuplicating()
    {
        var (userId, profileId) = SeedStudent();
        var activityId = SeedActivity();
        var attemptId = SeedAttempt(profileId, activityId);

        var first = await _sut.HandleAsync(BuildCommand(userId, activityId, attemptId, "first comment"));

        var second = await _sut.HandleAsync(new SubmitActivityFeedbackCommand(
            UserId: userId,
            LearningActivityId: activityId,
            DifficultyRating: ActivityFeedbackDifficultyRating.TooHard,
            ClarityRating: ActivityFeedbackClarityRating.Confusing,
            UsefulnessRating: ActivityFeedbackUsefulnessRating.NotUseful,
            RepeatPreference: ActivityFeedbackRepeatPreference.DoNotShowSimilarSoon,
            ActivityAttemptId: attemptId,
            OptionalComment: "updated comment"));

        second.Id.Should().Be(first.Id);
        second.DifficultyRating.Should().Be(ActivityFeedbackDifficultyRating.TooHard);
        second.OptionalComment.Should().Be("updated comment");

        (await _db.ActivityFeedbackSignals.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SubmitFeedback_CommentOverMaxLength_Throws()
    {
        var (userId, profileId) = SeedStudent();
        var activityId = SeedActivity();
        var attemptId = SeedAttempt(profileId, activityId);

        var overlong = new string('x', ActivityFeedbackSignal.MaxOptionalCommentLength + 1);

        var act = async () => await _sut.HandleAsync(BuildCommand(userId, activityId, attemptId, overlong));

        await act.Should().ThrowAsync<ArgumentException>();
        (await _db.ActivityFeedbackSignals.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SubmitFeedback_ForAnotherStudentsAttempt_ThrowsUnauthorized()
    {
        var (_, ownerProfileId) = SeedStudent();
        var activityId = SeedActivity();
        var attemptId = SeedAttempt(ownerProfileId, activityId);

        var (otherUserId, _) = SeedStudent();

        var act = async () => await _sut.HandleAsync(BuildCommand(otherUserId, activityId, attemptId));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await _db.ActivityFeedbackSignals.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SubmitFeedback_BackfillsProvenance_FromMatchingUsageLog()
    {
        var (userId, profileId) = SeedStudent();
        var activityId = SeedActivity();
        var attemptId = SeedAttempt(profileId, activityId);

        var usageLog = new StudentActivityUsageLog(
            studentProfileId: profileId,
            contentFingerprint: "fp-1",
            consumedAtUtc: DateTime.UtcNow,
            learningActivityId: activityId,
            patternKey: "email_reply",
            skill: "writing",
            cefrLevel: "B1",
            curriculumObjectiveKey: "obj_email_reply_1");
        _db.StudentActivityUsageLogs.Add(usageLog);
        await _db.SaveChangesAsync();

        var result = await _sut.HandleAsync(BuildCommand(userId, activityId, attemptId));

        var signal = await _db.ActivityFeedbackSignals.FirstAsync(s => s.Id == result.Id);
        signal.StudentActivityUsageLogId.Should().Be(usageLog.Id);
        signal.PatternKey.Should().Be("email_reply");
        signal.Skill.Should().Be("writing");
        signal.CefrLevel.Should().Be("B1");
        signal.CurriculumObjectiveKey.Should().Be("obj_email_reply_1");
    }
}
