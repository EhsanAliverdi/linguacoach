using FluentAssertions;
using LinguaCoach.Infrastructure.Assessment;

namespace LinguaCoach.UnitTests.Assessment;

public sealed class CefrAssessmentParserTests
{
    [Fact]
    public void ParseResponse_ValidJson_ReturnsAllFields()
    {
        var json = """
            {
              "level": "B2",
              "rationale": "نوشته شما نشان‌دهنده سطح خوبی است.",
              "strengths": ["Good use of formal vocabulary", "Clear sentence structure"],
              "areasForImprovement": ["Article usage", "Passive voice"]
            }
            """;

        var result = CefrAssessmentHandler.ParseResponse(json);

        result.Level.Should().Be("B2");
        result.Rationale.Should().Contain("نوشته");
        result.Strengths.Should().HaveCount(2);
        result.AreasForImprovement.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("A1")] [InlineData("A2")] [InlineData("B1")]
    [InlineData("B2")] [InlineData("C1")] [InlineData("C2")]
    public void ParseResponse_AllValidLevels_Accepted(string level)
    {
        var json = $"{{\"level\": \"{level}\"}}";
        var result = CefrAssessmentHandler.ParseResponse(json);
        result.Level.Should().Be(level);
    }

    [Theory]
    [InlineData("b2")]   // lowercase → normalised
    [InlineData("B2 ")] // trailing space
    public void ParseResponse_LevelNormalisedToUppercase(string rawLevel)
    {
        var json = $"{{\"level\": \"{rawLevel.Trim()}\"}}";
        var result = CefrAssessmentHandler.ParseResponse(json);
        result.Level.Should().Be("B2");
    }

    [Fact]
    public void ParseResponse_MarkdownFenceStripped()
    {
        var json = "```json\n{\"level\": \"B1\"}\n```";
        var result = CefrAssessmentHandler.ParseResponse(json);
        result.Level.Should().Be("B1");
    }

    [Fact]
    public void ParseResponse_InvalidLevel_Throws()
    {
        var json = "{\"level\": \"D1\"}";
        var act = () => CefrAssessmentHandler.ParseResponse(json);
        act.Should().Throw<InvalidOperationException>().WithMessage("*D1*");
    }

    [Fact]
    public void ParseResponse_MissingLevel_Throws()
    {
        var json = "{\"rationale\": \"Good\"}";
        var act = () => CefrAssessmentHandler.ParseResponse(json);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ParseResponse_InvalidJson_Throws()
    {
        var act = () => CefrAssessmentHandler.ParseResponse("not json");
        act.Should().Throw<InvalidOperationException>().WithMessage("*not valid JSON*");
    }

    [Fact]
    public void ParseResponse_EmptyArrays_AreAccepted()
    {
        var json = "{\"level\": \"A2\", \"strengths\": [], \"areasForImprovement\": []}";
        var result = CefrAssessmentHandler.ParseResponse(json);
        result.Level.Should().Be("A2");
        result.Strengths.Should().BeEmpty();
        result.AreasForImprovement.Should().BeEmpty();
    }
}
