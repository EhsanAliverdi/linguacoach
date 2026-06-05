using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.ValueObjects;

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

    [Fact]
    public void ApplyDelta_AddsAndDeduplicatesMemoryFields()
    {
        var summary = new UserLearningSummary(Guid.NewGuid());

        summary.ApplyDelta(new MemoryUpdateDelta(
            "Practised polite follow-up messages.",
            ["Clear request", "clear request"],
            ["Too direct tone"],
            ["Missing greeting", "missing greeting"],
            ["Follow-up email to manager", "follow-up email to manager"],
            ["formal_tone"],
            ["workplace_vocabulary"],
            ["Softening requests", "softening requests"]));

        JsonStringList.Read(summary.StrongSkillsJson).Should().ContainSingle().Which.Should().Be("Clear request");
        JsonStringList.Read(summary.WeakSkillsJson).Should().ContainSingle().Which.Should().Be("Too direct tone");
        JsonStringList.Read(summary.RecurringMistakesJson).Should().ContainSingle().Which.Should().Be("Missing greeting");
        JsonStringList.Read(summary.CoveredScenariosJson).Should().ContainSingle().Which.Should().Be("Follow-up email to manager");
        JsonStringList.Read(summary.NextFocusJson).Should().ContainSingle().Which.Should().Be("Softening requests");
    }

    [Fact]
    public void ApplyDelta_CapsMemoryLists()
    {
        var summary = new UserLearningSummary(Guid.NewGuid());
        var many = Enumerable.Range(1, 25).Select(i => $"item {i}").ToList();

        summary.ApplyDelta(new MemoryUpdateDelta("delta", many, many, many, many, [], [], many));

        JsonStringList.Read(summary.StrongSkillsJson).Should().HaveCount(10);
        JsonStringList.Read(summary.WeakSkillsJson).Should().HaveCount(10);
        JsonStringList.Read(summary.RecurringMistakesJson).Should().HaveCount(10);
        JsonStringList.Read(summary.CoveredScenariosJson).Should().HaveCount(20);
        JsonStringList.Read(summary.NextFocusJson).Should().HaveCount(5);
    }
}
