using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

public sealed class StudentLearningEventTests
{
    private static readonly Guid ValidProfileId = Guid.NewGuid();

    [Fact]
    public void Constructor_WithMinimumFields_Succeeds()
    {
        var evt = new StudentLearningEvent(
            studentProfileId: ValidProfileId,
            source: LearningEventSource.PracticeGym,
            outcome: LearningEventOutcome.Practised);

        Assert.Equal(ValidProfileId, evt.StudentProfileId);
        Assert.Equal(LearningEventSource.PracticeGym, evt.Source);
        Assert.Equal(LearningEventOutcome.Practised, evt.Outcome);
        Assert.Null(evt.PatternKey);
        Assert.Null(evt.PrimarySkill);
        Assert.Null(evt.Score);
        Assert.Null(evt.MistakeTagsJson);
    }

    [Fact]
    public void Constructor_WithAllFields_StoresCorrectly()
    {
        var activityId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();

        var evt = new StudentLearningEvent(
            studentProfileId: ValidProfileId,
            source: LearningEventSource.TodayLesson,
            outcome: LearningEventOutcome.Mastered,
            activityId: activityId,
            sessionId: sessionId,
            activityAttemptId: attemptId,
            exerciseType: "WritingScenario",
            patternKey: "email_reply",
            primarySkill: "writing",
            cefrLevelAtEvent: "B2",
            score: 87.5,
            normalizedScore: 0.875,
            mistakeTagsJson: "[\"grammar\"]");

        Assert.Equal(activityId, evt.ActivityId);
        Assert.Equal(sessionId, evt.SessionId);
        Assert.Equal(attemptId, evt.ActivityAttemptId);
        Assert.Equal("WritingScenario", evt.ExerciseType);
        Assert.Equal("email_reply", evt.PatternKey);
        Assert.Equal("writing", evt.PrimarySkill);
        Assert.Equal("B2", evt.CefrLevelAtEvent);
        Assert.Equal(87.5, evt.Score);
        Assert.Equal(0.875, evt.NormalizedScore);
        Assert.Equal("[\"grammar\"]", evt.MistakeTagsJson);
    }

    [Fact]
    public void Constructor_EmptyStudentProfileId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StudentLearningEvent(
                studentProfileId: Guid.Empty,
                source: LearningEventSource.PracticeGym,
                outcome: LearningEventOutcome.Practised));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(101.0)]
    public void Constructor_InvalidScore_Throws(double score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StudentLearningEvent(
                studentProfileId: ValidProfileId,
                source: LearningEventSource.PracticeGym,
                outcome: LearningEventOutcome.Practised,
                score: score));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Constructor_InvalidNormalizedScore_Throws(double normalizedScore)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StudentLearningEvent(
                studentProfileId: ValidProfileId,
                source: LearningEventSource.PracticeGym,
                outcome: LearningEventOutcome.Practised,
                normalizedScore: normalizedScore));
    }

    [Fact]
    public void Constructor_NullOptionalFields_DoesNotThrow()
    {
        var evt = new StudentLearningEvent(
            studentProfileId: ValidProfileId,
            source: LearningEventSource.PracticeGym,
            outcome: LearningEventOutcome.Skipped,
            patternKey: null,
            primarySkill: null,
            learningGoalContext: null,
            cefrLevelAtEvent: null,
            mistakeTagsJson: null);

        Assert.Null(evt.PatternKey);
        Assert.Null(evt.LearningGoalContext);
    }

    [Fact]
    public void OccurredAtUtc_SetToCurrentUtcTime()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var evt = new StudentLearningEvent(
            studentProfileId: ValidProfileId,
            source: LearningEventSource.PracticeGym,
            outcome: LearningEventOutcome.Practised);

        Assert.True(evt.OccurredAtUtc >= before);
        Assert.True(evt.OccurredAtUtc <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void LearningGoalContext_NoWorkplaceDefault_WhenContextNull()
    {
        var evt = new StudentLearningEvent(
            studentProfileId: ValidProfileId,
            source: LearningEventSource.PracticeGym,
            outcome: LearningEventOutcome.Practised,
            learningGoalContext: null);

        Assert.Null(evt.LearningGoalContext);
    }

    // ── Subskill ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SubskillMatchingPrimarySkill_Accepted()
    {
        var evt = new StudentLearningEvent(
            studentProfileId: ValidProfileId,
            source: LearningEventSource.TodayLesson,
            outcome: LearningEventOutcome.Practised,
            primarySkill: "writing",
            subskill: CurriculumSubskillConstants.WritingEmailMessage);

        Assert.Equal(CurriculumSubskillConstants.WritingEmailMessage, evt.Subskill);
    }

    [Fact]
    public void Constructor_SubskillNotMatchingPrimarySkill_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StudentLearningEvent(
                studentProfileId: ValidProfileId,
                source: LearningEventSource.TodayLesson,
                outcome: LearningEventOutcome.Practised,
                primarySkill: "writing",
                subskill: CurriculumSubskillConstants.SpeakingRoleplay));
    }

    [Fact]
    public void Constructor_SubskillWithoutPrimarySkill_ValidatesAgainstFullTaxonomy()
    {
        var evt = new StudentLearningEvent(
            studentProfileId: ValidProfileId,
            source: LearningEventSource.TodayLesson,
            outcome: LearningEventOutcome.Practised,
            subskill: CurriculumSubskillConstants.ListeningGist);

        Assert.Equal(CurriculumSubskillConstants.ListeningGist, evt.Subskill);
    }

    [Fact]
    public void Constructor_UnknownSubskillWithoutPrimarySkill_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StudentLearningEvent(
                studentProfileId: ValidProfileId,
                source: LearningEventSource.TodayLesson,
                outcome: LearningEventOutcome.Practised,
                subskill: "not_a_real_subskill"));
    }

    // ── CurriculumObjectiveKey (Phase 8) ──────────────────────────────────────

    [Fact]
    public void Constructor_CurriculumObjectiveKey_IsStoredAndTrimmed()
    {
        var evt = new StudentLearningEvent(
            studentProfileId: ValidProfileId,
            source: LearningEventSource.PracticeGym,
            outcome: LearningEventOutcome.Practised,
            curriculumObjectiveKey: "  b1.speaking.roleplay_ordering  ");

        Assert.Equal("b1.speaking.roleplay_ordering", evt.CurriculumObjectiveKey);
    }

    [Fact]
    public void Constructor_NullCurriculumObjectiveKey_DoesNotThrow()
    {
        var evt = new StudentLearningEvent(
            studentProfileId: ValidProfileId,
            source: LearningEventSource.PracticeGym,
            outcome: LearningEventOutcome.Practised);

        Assert.Null(evt.CurriculumObjectiveKey);
    }
}
