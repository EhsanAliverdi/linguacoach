using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.GoalVector;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.GoalVector;

public sealed class StudentGoalVectorServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly StudentGoalVectorService _sut;

    public StudentGoalVectorServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new StudentGoalVectorService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    /// <summary>StudentGoalWeight has a real FK to student_profiles — seed one per test so
    /// SaveChangesAsync doesn't hit a foreign-key violation.</summary>
    private async Task<Guid> SeedStudentAsync()
    {
        var profile = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return profile.Id;
    }

    [Fact]
    public async Task GetGoalsAsync_NoGoalsSet_ReturnsEmpty()
    {
        var studentId = await SeedStudentAsync();
        var goals = await _sut.GetGoalsAsync(studentId);
        Assert.Empty(goals);
    }

    [Fact]
    public async Task SetExplicitWeightAsync_CreatesNewRow()
    {
        var studentId = await SeedStudentAsync();
        await _sut.SetExplicitWeightAsync(studentId, CurriculumContextTagConstants.Travel, 0.7);

        var goals = await _sut.GetGoalsAsync(studentId);
        var goal = Assert.Single(goals);
        Assert.Equal(CurriculumContextTagConstants.Travel, goal.GoalTag);
        Assert.Equal(0.7, goal.Weight);
        Assert.Equal("Explicit", goal.Source);
    }

    [Fact]
    public async Task SetExplicitWeightAsync_UpdatesExistingRow()
    {
        var studentId = await SeedStudentAsync();
        await _sut.SetExplicitWeightAsync(studentId, CurriculumContextTagConstants.Travel, 0.3);
        await _sut.SetExplicitWeightAsync(studentId, CurriculumContextTagConstants.Travel, 0.9);

        var goals = await _sut.GetGoalsAsync(studentId);
        Assert.Single(goals);
        Assert.Equal(0.9, goals.Single().Weight);
    }

    [Fact]
    public async Task SetExplicitWeightAsync_NonGoalTag_Throws()
    {
        var studentId = await SeedStudentAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.SetExplicitWeightAsync(studentId, CurriculumContextTagConstants.Pronunciation, 0.5));
    }

    [Fact]
    public async Task RecordImplicitEngagementAsync_NewGoalTag_CreatesRowStartingFromZero()
    {
        var studentId = await SeedStudentAsync();
        await _sut.RecordImplicitEngagementAsync(studentId, [CurriculumContextTagConstants.Travel]);

        var goals = await _sut.GetGoalsAsync(studentId);
        var goal = Assert.Single(goals);
        Assert.Equal(StudentGoalVectorService.ImplicitEngagementAlpha, goal.Weight, precision: 10);
        Assert.Equal("Implicit", goal.Source);
    }

    [Fact]
    public async Task RecordImplicitEngagementAsync_ExistingGoalTag_NudgesWeightUp()
    {
        var studentId = await SeedStudentAsync();
        await _sut.SetExplicitWeightAsync(studentId, CurriculumContextTagConstants.Travel, 0.5);
        await _sut.RecordImplicitEngagementAsync(studentId, [CurriculumContextTagConstants.Travel]);

        var goal = (await _sut.GetGoalsAsync(studentId)).Single();
        Assert.True(goal.Weight > 0.5);
        Assert.Equal("Implicit", goal.Source);
    }

    [Fact]
    public async Task RecordImplicitEngagementAsync_NonGoalTagsAreIgnoredNotErrored()
    {
        var studentId = await SeedStudentAsync();
        await _sut.RecordImplicitEngagementAsync(studentId, [CurriculumContextTagConstants.Pronunciation, "unknown_tag"]);

        var goals = await _sut.GetGoalsAsync(studentId);
        Assert.Empty(goals);
    }

    [Fact]
    public async Task RecordImplicitEngagementAsync_MixOfGoalAndNonGoalTags_OnlyGoalTagsPersist()
    {
        var studentId = await SeedStudentAsync();
        await _sut.RecordImplicitEngagementAsync(studentId,
            [CurriculumContextTagConstants.Travel, CurriculumContextTagConstants.Pronunciation]);

        var goals = await _sut.GetGoalsAsync(studentId);
        var goal = Assert.Single(goals);
        Assert.Equal(CurriculumContextTagConstants.Travel, goal.GoalTag);
    }

    [Fact]
    public async Task RecordImplicitEngagementAsync_EmptyList_DoesNothing()
    {
        var studentId = await SeedStudentAsync();
        await _sut.RecordImplicitEngagementAsync(studentId, []);

        var goals = await _sut.GetGoalsAsync(studentId);
        Assert.Empty(goals);
    }

    [Theory]
    [InlineData("""["travel", "day_to_day"]""", 2)]
    [InlineData("[]", 0)]
    [InlineData(null, 0)]
    [InlineData("not json", 0)]
    public void ParseContextTags_HandlesValidInvalidAndEmptyInput(string? json, int expectedCount)
    {
        var result = StudentGoalVectorService.ParseContextTags(json);
        Assert.Equal(expectedCount, result.Count);
    }
}
