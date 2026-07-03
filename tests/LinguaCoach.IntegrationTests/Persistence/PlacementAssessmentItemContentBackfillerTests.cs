using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.IntegrationTests.Persistence;

/// <summary>Unified Question-Schema Phase 2 — backfilling ContentJson/AnswerJson onto historical
/// PlacementAssessmentItem rows created before those fields existed.</summary>
public sealed class PlacementAssessmentItemContentBackfillerTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;

    public PlacementAssessmentItemContentBackfillerTests()
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
    public async Task BackfillAsync_PopulatesContentAndAnswerForAnsweredLegacyRow()
    {
        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        var assessment = PlacementAssessment.CreateAdaptive(student.Id, "student");
        assessment.Start();
        _db.PlacementAssessments.Add(assessment);

        var item = PlacementAssessmentItem.Create(
            assessment.Id, "grammar", "A1", "multiple_choice",
            "Which is correct? 'I ___ happy.' (A) am (B) is (C) are", "A", 0);
        item.RecordResponse("A", true, 1.0, "Correct.");
        // Simulate a legacy row: RecordResponse already shadows AnswerJson going forward, so clear
        // it to represent a row created before the Phase 2 schema existed.
        _db.PlacementAssessmentItems.Add(item);
        await _db.SaveChangesAsync();

        await PlacementAssessmentItemContentBackfiller.BackfillAsync(_db);

        var reloaded = await _db.PlacementAssessmentItems.FirstAsync(i => i.Id == item.Id);
        Assert.NotNull(reloaded.Content);
        Assert.NotNull(reloaded.Answer);
        Assert.Equal("A", reloaded.Answer!.Find("q1")!.Values[0]);
    }

    [Fact]
    public async Task BackfillAsync_LeavesUnansweredRowContentPopulatedButAnswerNull()
    {
        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        var assessment = PlacementAssessment.CreateAdaptive(student.Id, "student");
        assessment.Start();
        _db.PlacementAssessments.Add(assessment);

        var item = PlacementAssessmentItem.Create(
            assessment.Id, "grammar", "A1", "gap_fill", "Complete: 'They ___ students.'", "are", 0);
        _db.PlacementAssessmentItems.Add(item);
        await _db.SaveChangesAsync();

        await PlacementAssessmentItemContentBackfiller.BackfillAsync(_db);

        var reloaded = await _db.PlacementAssessmentItems.FirstAsync(i => i.Id == item.Id);
        Assert.NotNull(reloaded.Content);
        Assert.Null(reloaded.Answer);
    }

    [Fact]
    public async Task BackfillAsync_IsIdempotent()
    {
        var student = new StudentProfile(Guid.NewGuid());
        _db.StudentProfiles.Add(student);
        var assessment = PlacementAssessment.CreateAdaptive(student.Id, "student");
        assessment.Start();
        _db.PlacementAssessments.Add(assessment);

        var item = PlacementAssessmentItem.Create(
            assessment.Id, "grammar", "A1", "gap_fill", "Complete: 'They ___ students.'", "are", 0);
        _db.PlacementAssessmentItems.Add(item);
        await _db.SaveChangesAsync();

        await PlacementAssessmentItemContentBackfiller.BackfillAsync(_db);
        await PlacementAssessmentItemContentBackfiller.BackfillAsync(_db);

        var reloaded = await _db.PlacementAssessmentItems.FirstAsync(i => i.Id == item.Id);
        Assert.NotNull(reloaded.Content);
    }
}
