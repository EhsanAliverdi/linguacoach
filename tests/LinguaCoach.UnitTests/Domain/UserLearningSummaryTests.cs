using FluentAssertions;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class UserLearningSummaryTests
{
    [Fact]
    public void Constructor_WithEmptyStudentId_Throws()
    {
        var act = () => new UserLearningSummary(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NewSummary_HasEmptyFields()
    {
        var summary = new UserLearningSummary(Guid.NewGuid());
        summary.RecentWeaknesses.Should().BeEmpty();
        summary.RecentProgress.Should().BeEmpty();
    }

    [Fact]
    public void Update_SetsFields()
    {
        var summary = new UserLearningSummary(Guid.NewGuid());
        summary.Update("overuses please", "email closings improved");
        summary.RecentWeaknesses.Should().Be("overuses please");
        summary.RecentProgress.Should().Be("email closings improved");
    }

    [Fact]
    public void Update_WeaknessesExceeds200Chars_Throws()
    {
        var summary = new UserLearningSummary(Guid.NewGuid());
        var tooLong = new string('x', 201);
        var act = () => summary.Update(tooLong, "progress");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_ProgressExceeds200Chars_Throws()
    {
        var summary = new UserLearningSummary(Guid.NewGuid());
        var tooLong = new string('x', 201);
        var act = () => summary.Update("weak", tooLong);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_Exactly200Chars_IsAllowed()
    {
        var summary = new UserLearningSummary(Guid.NewGuid());
        var exactly200 = new string('x', 200);
        var act = () => summary.Update(exactly200, exactly200);
        act.Should().NotThrow();
    }
}
