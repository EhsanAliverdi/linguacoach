using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.IntegrationTests.Persistence;

public sealed class PatternEvaluationAttemptMappingTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public PatternEvaluationAttemptMappingTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task ActivityAttempt_CanPersistPatternEvaluationFields()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var activity = new LearningActivity(
            ActivityType.VocabularyPractice,
            ActivitySource.AiGenerated,
            "Match phrases",
            "B1",
            """{"patternKey":"phrase_match"}""",
            exercisePatternKey: "phrase_match");

        _db.StudentProfiles.Add(student);
        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync();

        var attempt = new ActivityAttempt(
            studentProfileId: student.Id,
            learningActivityId: activity.Id,
            submittedContent: """{"pairs":[{"left":"l1","right":"r1"}]}""",
            feedbackJson: """{"overallScore":50}""",
            promptKey: "pattern_evaluation_foundation_test",
            score: 1,
            submittedAnswerJson: """{"pairs":[{"left":"l1","right":"r1"}]}""",
            evaluationResultJson: """{"score":1,"maxScore":2,"percentage":50,"passed":false,"completed":true}""",
            maxScore: 2,
            percentage: 50,
            passed: false,
            completed: true,
            markingMode: MarkingMode.KeyedSelection);

        _db.ActivityAttempts.Add(attempt);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.ActivityAttempts.SingleAsync(a => a.Id == attempt.Id);

        Assert.Contains("\"pairs\"", loaded.SubmittedAnswerJson);
        Assert.Contains("\"percentage\":50", loaded.EvaluationResultJson);
        Assert.Equal(1, loaded.Score);
        Assert.Equal(2, loaded.MaxScore);
        Assert.Equal(50, loaded.Percentage);
        Assert.False(loaded.Passed);
        Assert.True(loaded.Completed);
        Assert.Equal(MarkingMode.KeyedSelection, loaded.MarkingMode);
    }

    [Fact]
    public async Task LegacyActivityAttempt_WithoutPatternEvaluationFields_LoadsWithNulls()
    {
        var student = new StudentProfile(Guid.NewGuid());
        var activity = new LearningActivity(
            ActivityType.WritingScenario,
            ActivitySource.AiGenerated,
            "Legacy writing",
            "B1",
            """{"title":"Legacy writing"}""");

        _db.StudentProfiles.Add(student);
        _db.LearningActivities.Add(activity);
        await _db.SaveChangesAsync();

        var attempt = new ActivityAttempt(
            studentProfileId: student.Id,
            learningActivityId: activity.Id,
            submittedContent: "I will send it today.",
            feedbackJson: """{"overallScore":80}""",
            promptKey: "activity_evaluate_writing",
            score: 80);

        _db.ActivityAttempts.Add(attempt);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.ActivityAttempts.SingleAsync(a => a.Id == attempt.Id);

        Assert.Equal(80, loaded.Score);
        Assert.Null(loaded.SubmittedAnswerJson);
        Assert.Null(loaded.EvaluationResultJson);
        Assert.Null(loaded.MaxScore);
        Assert.Null(loaded.Percentage);
        Assert.Null(loaded.Passed);
        Assert.Null(loaded.Completed);
        Assert.Null(loaded.MarkingMode);
    }
}
