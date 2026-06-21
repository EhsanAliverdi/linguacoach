using LinguaCoach.Application.Admin;
using FluentAssertions;

namespace LinguaCoach.UnitTests.Admin;

public sealed class AiUsageSummaryTests
{
    private static AiUsageSummaryDto MakeDto(
        int totalCalls = 3,
        int successfulCalls = 3,
        int failedCalls = 0,
        int fallbackCalls = 0,
        decimal totalCostUsd = 0.05m,
        long totalInputTokens = 1200,
        long totalOutputTokens = 800,
        int zeroCostCallCount = 0,
        long zeroCostTotalTokens = 0) =>
        new(
            TotalCalls: totalCalls,
            SuccessfulCalls: successfulCalls,
            FailedCalls: failedCalls,
            FallbackCalls: fallbackCalls,
            TotalCostUsd: totalCostUsd,
            TotalInputTokens: totalInputTokens,
            TotalOutputTokens: totalOutputTokens,
            TotalTokens: totalInputTokens + totalOutputTokens,
            ByProvider: [],
            ByFeature: [],
            ZeroCostCallCount: zeroCostCallCount,
            ZeroCostTotalTokens: zeroCostTotalTokens);

    [Fact]
    public void AiUsageSummaryDto_TokenTotals_SumCorrectly()
    {
        var dto = MakeDto(totalInputTokens: 1200, totalOutputTokens: 800);

        dto.TotalInputTokens.Should().Be(1200);
        dto.TotalOutputTokens.Should().Be(800);
        dto.TotalTokens.Should().Be(dto.TotalInputTokens + dto.TotalOutputTokens);
    }

    [Fact]
    public void AiUsageSummaryDto_WhenNoLogs_TokenTotalsAreZero()
    {
        var dto = MakeDto(totalCalls: 0, successfulCalls: 0, totalCostUsd: 0m,
            totalInputTokens: 0, totalOutputTokens: 0);

        dto.TotalInputTokens.Should().Be(0);
        dto.TotalOutputTokens.Should().Be(0);
        dto.TotalTokens.Should().Be(0);
    }

    [Fact]
    public void AiUsageSummaryDto_TotalTokens_EqualsInputPlusOutput()
    {
        var dto = MakeDto(totalCalls: 100, successfulCalls: 98, failedCalls: 2,
            fallbackCalls: 5, totalCostUsd: 12.34m,
            totalInputTokens: 5_000_000, totalOutputTokens: 1_500_000);

        dto.TotalTokens.Should().Be(6_500_000);
    }

    [Fact]
    public void AiUsageSummaryDto_ZeroCostFields_DefaultToZero()
    {
        var dto = MakeDto();

        dto.ZeroCostCallCount.Should().Be(0);
        dto.ZeroCostTotalTokens.Should().Be(0);
    }

    [Fact]
    public void AiUsageSummaryDto_ZeroCostFields_AreReadable()
    {
        var dto = MakeDto(zeroCostCallCount: 3, zeroCostTotalTokens: 750);

        dto.ZeroCostCallCount.Should().Be(3);
        dto.ZeroCostTotalTokens.Should().Be(750);
    }
}
