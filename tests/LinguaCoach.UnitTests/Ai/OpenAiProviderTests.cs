using LinguaCoach.Application.Ai;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;

namespace LinguaCoach.UnitTests.Ai;

public sealed class OpenAiProviderTests
{
    [Fact]
    public async Task CompleteAsync_WithoutApiKey_ReturnsControlledConfigurationException()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = ""
            })
            .Build();
        var provider = new OpenAiProvider(configuration, NullLogger<OpenAiProvider>.Instance);
        var request = new AiRequest("test.prompt", "Hello", 100);

        var exception = await Assert.ThrowsAsync<AiConfigurationUnavailableException>(
            () => provider.CompleteAsync(request));

        Assert.Contains("API key is not configured", exception.Message);
    }

    [Fact]
    public void Pricing_WhenModelPricingConfigured_EstimatesCost()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:Pricing:gpt-test:InputPer1KTokens"] = "0.002",
                ["OpenAI:Pricing:gpt-test:OutputPer1KTokens"] = "0.006"
            })
            .Build();

        var pricing = AiPricingOptions.GetOpenAiPricing(configuration, "gpt-test");
        pricing.Should().NotBeNull();

        var cost = AiPricingOptions.EstimateCostUsd(1500, 250, pricing!);

        cost.Should().Be(0.0045m);
    }

    [Fact]
    public void Pricing_WhenModelPricingMissing_ReturnsNull()
    {
        var configuration = new ConfigurationBuilder().Build();

        var pricing = AiPricingOptions.GetOpenAiPricing(configuration, "gpt-4o");

        pricing.Should().BeNull();
    }

    [Fact]
    public void Pricing_WhenOnlyOneSideConfigured_ReturnsNull()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:Pricing:gpt-test:InputPer1KTokens"] = "0.002"
            })
            .Build();

        var pricing = AiPricingOptions.GetOpenAiPricing(configuration, "gpt-test");

        pricing.Should().BeNull();
    }
}
