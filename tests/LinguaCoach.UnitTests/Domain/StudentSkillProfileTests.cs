using FluentAssertions;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class StudentSkillProfileTests
{
    [Fact]
    public void Constructor_WithValidArgs_SetsProperties()
    {
        var studentId = Guid.NewGuid();

        var profile = new StudentSkillProfile(studentId, "Formal Tone", "Formal tone", isWeak: true);

        profile.StudentProfileId.Should().Be(studentId);
        profile.SkillKey.Should().Be("formal_tone");
        profile.SkillLabel.Should().Be("Formal tone");
        profile.IsWeak.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithEmptySkillKey_Throws()
    {
        var act = () => new StudentSkillProfile(Guid.NewGuid(), " ", "Formal tone", true);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkWeak_UpdatesWeakFlag()
    {
        var profile = new StudentSkillProfile(Guid.NewGuid(), "formal_tone", "Formal tone", true);

        profile.MarkWeak(false);

        profile.IsWeak.Should().BeFalse();
    }

    [Fact]
    public void ApplyScoreDelta_ClampsToZeroAndHundred()
    {
        var profile = new StudentSkillProfile(Guid.NewGuid(), "formal_tone", "Formal tone", scorePercent: 5);

        profile.ApplyScoreDelta(-10);
        profile.ScorePercent.Should().Be(0);

        profile.ApplyScoreDelta(200);
        profile.ScorePercent.Should().Be(100);
    }

    [Fact]
    public void IsWeak_DerivedFromScorePercent()
    {
        var profile = new StudentSkillProfile(Guid.NewGuid(), "formal_tone", "Formal tone", scorePercent: 49);
        profile.IsWeak.Should().BeTrue();

        profile.ApplyScoreDelta(1);
        profile.IsWeak.Should().BeFalse();
    }
}
