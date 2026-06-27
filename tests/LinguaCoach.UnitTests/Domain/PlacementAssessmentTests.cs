using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.UnitTests.Domain;

/// <summary>
/// Unit tests for PlacementAssessment entity — Phase 13A lifecycle methods.
/// </summary>
public sealed class PlacementAssessmentTests
{
    // ── CreateAdaptive factory ────────────────────────────────────────────────

    [Fact]
    public void CreateAdaptive_ValidArgs_SetsFields()
    {
        var studentId = Guid.NewGuid();
        var assessment = PlacementAssessment.CreateAdaptive(studentId, "admin");

        Assert.Equal(studentId, assessment.StudentProfileId);
        Assert.Equal(PlacementStatus.NotStarted, assessment.Status);
        Assert.True(assessment.IsAdaptive);
        Assert.Equal("admin", assessment.Source);
        Assert.Equal("adaptive", assessment.CurrentSectionKey);
    }

    [Fact]
    public void CreateAdaptive_EmptyStudentId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PlacementAssessment.CreateAdaptive(Guid.Empty, "admin"));
    }

    // ── Start ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Start_SetsInProgressAndStartedAt()
    {
        var assessment = PlacementAssessment.CreateAdaptive(Guid.NewGuid(), "admin");
        assessment.Start();

        Assert.Equal(PlacementStatus.InProgress, assessment.Status);
        Assert.NotNull(assessment.StartedAtUtc);
    }

    [Fact]
    public void Start_AlreadyCompleted_Throws()
    {
        var assessment = PlacementAssessment.CreateAdaptive(Guid.NewGuid(), "admin");
        assessment.Start();
        assessment.CompleteAdaptive("B1", 0.8, "done", false);

        Assert.Throws<InvalidOperationException>(() => assessment.Start());
    }

    // ── Abandon ───────────────────────────────────────────────────────────────

    [Fact]
    public void Abandon_WhileInProgress_SetsAbandoned()
    {
        var assessment = PlacementAssessment.CreateAdaptive(Guid.NewGuid(), "admin");
        assessment.Start();
        assessment.Abandon();

        Assert.Equal(PlacementStatus.Abandoned, assessment.Status);
        Assert.NotNull(assessment.AbandonedAtUtc);
    }

    [Fact]
    public void Abandon_NotInProgress_Throws()
    {
        var assessment = PlacementAssessment.CreateAdaptive(Guid.NewGuid(), "admin");
        // Status is NotStarted

        Assert.Throws<InvalidOperationException>(() => assessment.Abandon());
    }

    // ── Expire ────────────────────────────────────────────────────────────────

    [Fact]
    public void Expire_WhileInProgress_SetsExpired()
    {
        var assessment = PlacementAssessment.CreateAdaptive(Guid.NewGuid(), "admin");
        assessment.Start();
        assessment.Expire();

        Assert.Equal(PlacementStatus.Expired, assessment.Status);
        Assert.NotNull(assessment.ExpiredAtUtc);
    }

    [Fact]
    public void Expire_AlreadyCompleted_Throws()
    {
        var assessment = PlacementAssessment.CreateAdaptive(Guid.NewGuid(), "admin");
        assessment.Start();
        assessment.CompleteAdaptive("A2", 0.5, "summary", true);

        Assert.Throws<InvalidOperationException>(() => assessment.Expire());
    }

    [Fact]
    public void Expire_AlreadyAbandoned_Throws()
    {
        var assessment = PlacementAssessment.CreateAdaptive(Guid.NewGuid(), "admin");
        assessment.Start();
        assessment.Abandon();

        Assert.Throws<InvalidOperationException>(() => assessment.Expire());
    }

    // ── CompleteAdaptive ──────────────────────────────────────────────────────

    [Fact]
    public void CompleteAdaptive_SetsAllFields()
    {
        var assessment = PlacementAssessment.CreateAdaptive(Guid.NewGuid(), "admin");
        assessment.Start();
        assessment.CompleteAdaptive("B1", 0.82, "Estimated B1", true);

        Assert.Equal(PlacementStatus.Completed, assessment.Status);
        Assert.Equal("B1", assessment.OverallEstimatedLevel);
        Assert.Equal(0.82, assessment.OverallConfidence);
        Assert.Equal("Estimated B1", assessment.ResultSummary);
        Assert.True(assessment.IsProvisional);
        Assert.NotNull(assessment.CompletedAtUtc);
    }

    [Fact]
    public void CompleteAdaptive_AlreadyCompleted_Throws()
    {
        var assessment = PlacementAssessment.CreateAdaptive(Guid.NewGuid(), "admin");
        assessment.Start();
        assessment.CompleteAdaptive("A2", 0.6, "done", false);

        Assert.Throws<InvalidOperationException>(() =>
            assessment.CompleteAdaptive("B1", 0.9, "second", false));
    }

    [Fact]
    public void CompleteAdaptive_WhileAbandoned_Throws()
    {
        var assessment = PlacementAssessment.CreateAdaptive(Guid.NewGuid(), "admin");
        assessment.Start();
        assessment.Abandon();

        Assert.Throws<InvalidOperationException>(() =>
            assessment.CompleteAdaptive("A2", 0.5, "summary", true));
    }
}
