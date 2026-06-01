using FluentAssertions;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class AiUsageLogTests
{
    private static readonly Guid ValidStudentId = Guid.NewGuid();

    [Fact]
    public void Constructor_SetsAllFields()
    {
        var log = new AiUsageLog(ValidStudentId, "openai", "gpt-4o", 100, 200, 0.05m);

        log.StudentProfileId.Should().Be(ValidStudentId);
        log.ProviderName.Should().Be("openai");
        log.ModelName.Should().Be("gpt-4o");
        log.InputTokens.Should().Be(100);
        log.OutputTokens.Should().Be(200);
        log.CostUsd.Should().Be(0.05m);
    }

    [Fact]
    public void Constructor_WithEmptyStudentId_Throws()
    {
        var act = () => new AiUsageLog(Guid.Empty, "openai", "gpt-4o", 0, 0, 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithBlankProviderName_Throws()
    {
        var act = () => new AiUsageLog(ValidStudentId, "", "gpt-4o", 0, 0, 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithBlankModelName_Throws()
    {
        var act = () => new AiUsageLog(ValidStudentId, "openai", "", 0, 0, 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNegativeInputTokens_Throws()
    {
        var act = () => new AiUsageLog(ValidStudentId, "openai", "gpt-4o", -1, 0, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeOutputTokens_Throws()
    {
        var act = () => new AiUsageLog(ValidStudentId, "openai", "gpt-4o", 0, -1, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeCost_Throws()
    {
        var act = () => new AiUsageLog(ValidStudentId, "openai", "gpt-4o", 0, 0, -0.01m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithZeroTokensAndCost_IsValid()
    {
        var act = () => new AiUsageLog(ValidStudentId, "openai", "gpt-4o", 0, 0, 0);
        act.Should().NotThrow();
    }
}
