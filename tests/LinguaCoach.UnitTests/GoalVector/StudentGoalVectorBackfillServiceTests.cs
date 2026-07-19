using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.GoalVector;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.GoalVector;

public sealed class StudentGoalVectorBackfillServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly StudentGoalVectorBackfillService _sut;

    public StudentGoalVectorBackfillServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new StudentGoalVectorBackfillService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private static void SetLearningGoals(StudentProfile profile, List<string> learningGoals) =>
        profile.UpdateLearningPreferences(
            preferredName: null,
            supportLanguageCode: null,
            supportLanguageName: null,
            translationHelpPreference: null,
            learningGoals: learningGoals,
            customLearningGoal: null,
            focusAreas: null,
            customFocusArea: null,
            difficultyPreference: null,
            preferredSessionDurationMinutes: null);

    [Fact]
    public async Task BackfillFromLearningGoalsAsync_NoProfiles_ReturnsZeroResult()
    {
        var result = await _sut.BackfillFromLearningGoalsAsync();

        Assert.Equal(0, result.StudentsScanned);
        Assert.Equal(0, result.StudentsWithAtLeastOneMappedGoal);
        Assert.Equal(0, result.WeightsCreated);
    }

    [Fact]
    public async Task BackfillFromLearningGoalsAsync_MappedKeys_CreateGoalWeights()
    {
        var profile = new StudentProfile(Guid.NewGuid());
        SetLearningGoals(profile, ["day_to_day", "work", "pronunciation"]);
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var result = await _sut.BackfillFromLearningGoalsAsync();

        Assert.Equal(1, result.StudentsScanned);
        Assert.Equal(1, result.StudentsWithAtLeastOneMappedGoal);
        // "day_to_day" -> DayToDay, "work" -> Workplace; "pronunciation" has no goal-tag mapping.
        Assert.Equal(2, result.WeightsCreated);

        var weights = await _db.StudentGoalWeights.Where(g => g.StudentId == profile.Id).ToListAsync();
        Assert.Equal(2, weights.Count);
        Assert.Contains(weights, w => w.GoalTag == CurriculumContextTagConstants.DayToDay);
        Assert.Contains(weights, w => w.GoalTag == CurriculumContextTagConstants.Workplace);
    }

    [Fact]
    public async Task BackfillFromLearningGoalsAsync_OnlyUnmappableKeys_ScansButCreatesNothing()
    {
        var profile = new StudentProfile(Guid.NewGuid());
        SetLearningGoals(profile, ["pronunciation", "exam_inspired_practice"]);
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var result = await _sut.BackfillFromLearningGoalsAsync();

        Assert.Equal(1, result.StudentsScanned);
        Assert.Equal(0, result.StudentsWithAtLeastOneMappedGoal);
        Assert.Equal(0, result.WeightsCreated);
    }

    [Fact]
    public async Task BackfillFromLearningGoalsAsync_IsIdempotent_SecondRunSkipsExisting()
    {
        var profile = new StudentProfile(Guid.NewGuid());
        SetLearningGoals(profile, ["travel"]);
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var first = await _sut.BackfillFromLearningGoalsAsync();
        Assert.Equal(1, first.WeightsCreated);

        var second = await _sut.BackfillFromLearningGoalsAsync();
        Assert.Equal(0, second.WeightsCreated);
        Assert.Equal(1, second.WeightsSkippedAlreadySet);

        var weights = await _db.StudentGoalWeights.Where(g => g.StudentId == profile.Id).ToListAsync();
        Assert.Single(weights); // still only one row, not duplicated
    }

    [Fact]
    public async Task BackfillFromLearningGoalsAsync_DoesNotOverwriteAnAlreadyAdjustedExplicitWeight()
    {
        var profile = new StudentProfile(Guid.NewGuid());
        SetLearningGoals(profile, ["travel"]);
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        // Student already explicitly set a different weight before the backfill ran.
        _db.StudentGoalWeights.Add(new StudentGoalWeight(
            profile.Id, CurriculumContextTagConstants.Travel, 0.1, StudentGoalWeightSource.Explicit));
        await _db.SaveChangesAsync();

        var result = await _sut.BackfillFromLearningGoalsAsync();

        Assert.Equal(0, result.WeightsCreated);
        var weight = await _db.StudentGoalWeights.SingleAsync(g => g.StudentId == profile.Id);
        Assert.Equal(0.1, weight.Weight); // untouched
    }
}
