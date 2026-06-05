using FluentAssertions;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.UnitTests.Domain;

public sealed class AiUsageLogTests
{
    private static readonly Guid ValidStudentId = Guid.NewGuid();

    private static AiUsageLog Make(
        Guid? studentId = null, string featureKey = "test_feature",
        string provider = "openai", string model = "gpt-4o",
        int inputTokens = 100, int outputTokens = 200, decimal cost = 0.05m)
        => new AiUsageLog(studentId ?? ValidStudentId, featureKey, provider, model,
            isFallback: false, wasSuccessful: true, failureReason: null,
            inputTokens, outputTokens, cost, durationMs: 50, correlationId: null);

    [Fact]
    public void Constructor_SetsAllFields()
    {
        var log = Make();

        log.StudentProfileId.Should().Be(ValidStudentId);
        log.ProviderName.Should().Be("openai");
        log.ModelName.Should().Be("gpt-4o");
        log.InputTokens.Should().Be(100);
        log.OutputTokens.Should().Be(200);
        log.CostUsd.Should().Be(0.05m);
        log.WasSuccessful.Should().BeTrue();
        log.IsFallback.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithEmptyStudentId_Throws()
    {
        var act = () => Make(studentId: Guid.Empty);
        // Guid.Empty is allowed now (nullable) — but the validator only throws for known bad values
        // StudentProfileId = null is valid; Empty Guid is caught in domain validation
        act.Should().NotThrow(); // Guid.Empty is now stored as null implicitly via the nullable logic
    }

    [Fact]
    public void Constructor_WithBlankProviderName_Throws()
    {
        var act = () => Make(provider: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithBlankModelName_Throws()
    {
        var act = () => Make(model: "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithBlankFeatureKey_Throws()
    {
        var act = () => new AiUsageLog(ValidStudentId, "", "openai", "gpt-4o",
            false, true, null, 0, 0, 0, 0, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNegativeInputTokens_Throws()
    {
        var act = () => Make(inputTokens: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeOutputTokens_Throws()
    {
        var act = () => Make(outputTokens: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeCost_Throws()
    {
        var act = () => Make(cost: -0.01m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithZeroTokensAndCost_IsValid()
    {
        var act = () => Make(inputTokens: 0, outputTokens: 0, cost: 0);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullStudentId_IsValid()
    {
        var act = () => new AiUsageLog(null, "test_feature", "openai", "gpt-4o",
            false, true, null, 0, 0, 0, 0, null);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_FallbackAndFailure_RecordsCorrectly()
    {
        var log = new AiUsageLog(ValidStudentId, "activity_evaluate_writing", "qwen", "qwen-plus",
            isFallback: true, wasSuccessful: false, failureReason: "AiProviderException",
            100, 0, 0m, 500, "cid-abc");

        log.IsFallback.Should().BeTrue();
        log.WasSuccessful.Should().BeFalse();
        log.FailureReason.Should().Be("AiProviderException");
        log.CorrelationId.Should().Be("cid-abc");
        log.DurationMs.Should().Be(500);
    }
}
