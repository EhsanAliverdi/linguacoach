using LinguaCoach.Application.Admin;
using FluentAssertions;

namespace LinguaCoach.UnitTests.Admin;

public sealed class AiUsageSummaryTests
{
    [Fact]
    public void AiUsageSummaryDto_TokenTotals_SumCorrectly()
    {
        var dto = new AiUsageSummaryDto(
            TotalCalls: 3,
            SuccessfulCalls: 3,
            FailedCalls: 0,
            FallbackCalls: 0,
            TotalCostUsd: 0.05m,
            TotalInputTokens: 1200,
            TotalOutputTokens: 800,
            TotalTokens: 2000,
            ByProvider: [],
            ByFeature: []);

        dto.TotalInputTokens.Should().Be(1200);
        dto.TotalOutputTokens.Should().Be(800);
        dto.TotalTokens.Should().Be(dto.TotalInputTokens + dto.TotalOutputTokens);
    }

    [Fact]
    public void AiUsageSummaryDto_WhenNoLogs_TokenTotalsAreZero()
    {
        var dto = new AiUsageSummaryDto(
            TotalCalls: 0,
            SuccessfulCalls: 0,
            FailedCalls: 0,
            FallbackCalls: 0,
            TotalCostUsd: 0m,
            TotalInputTokens: 0,
            TotalOutputTokens: 0,
            TotalTokens: 0,
            ByProvider: [],
            ByFeature: []);

        dto.TotalInputTokens.Should().Be(0);
        dto.TotalOutputTokens.Should().Be(0);
        dto.TotalTokens.Should().Be(0);
    }

    [Fact]
    public void AiUsageSummaryDto_TotalTokens_EqualsInputPlusOutput()
    {
        long input = 5_000_000;
        long output = 1_500_000;

        var dto = new AiUsageSummaryDto(
            TotalCalls: 100,
            SuccessfulCalls: 98,
            FailedCalls: 2,
            FallbackCalls: 5,
            TotalCostUsd: 12.34m,
            TotalInputTokens: input,
            TotalOutputTokens: output,
            TotalTokens: input + output,
            ByProvider: [],
            ByFeature: []);

        dto.TotalTokens.Should().Be(6_500_000);
    }
}
