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

    [Theory]
    [InlineData("gpt-4o",      0.0025,  0.01)]
    [InlineData("gpt-4o-mini", 0.00015, 0.0006)]
    [InlineData("gpt-4.1",     0.002,   0.008)]
    public void Pricing_DefaultAppsettingsValues_BindCorrectlyForOpenAi(
        string model, decimal expectedInput, decimal expectedOutput)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"OpenAI:Pricing:{model}:InputPer1KTokens"]  = expectedInput.ToString(),
                [$"OpenAI:Pricing:{model}:OutputPer1KTokens"] = expectedOutput.ToString(),
            })
            .Build();

        var pricing = AiPricingOptions.GetOpenAiPricing(configuration, model);

        pricing.Should().NotBeNull();
        pricing!.InputPer1KTokens.Should().Be(expectedInput);
        pricing.OutputPer1KTokens.Should().Be(expectedOutput);
    }

    [Theory]
    [InlineData("gemini-2.0-flash",      0.0001,   0.0004)]
    [InlineData("gemini-1.5-pro",        0.00125,  0.005)]
    public void Pricing_DefaultAppsettingsValues_BindCorrectlyForGemini(
        string model, decimal expectedInput, decimal expectedOutput)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"Gemini:Pricing:{model}:InputPer1KTokens"]  = expectedInput.ToString(),
                [$"Gemini:Pricing:{model}:OutputPer1KTokens"] = expectedOutput.ToString(),
            })
            .Build();

        var pricing = AiPricingOptions.GetGeminiPricing(configuration, model);

        pricing.Should().NotBeNull();
        pricing!.InputPer1KTokens.Should().Be(expectedInput);
        pricing.OutputPer1KTokens.Should().Be(expectedOutput);
    }

    [Theory]
    [InlineData("claude-sonnet-4-6",          0.003,  0.015)]
    [InlineData("claude-haiku-4-5-20251001",  0.0008, 0.004)]
    public void Pricing_DefaultAppsettingsValues_BindCorrectlyForAnthropic(
        string model, decimal expectedInput, decimal expectedOutput)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"Anthropic:Pricing:{model}:InputPer1KTokens"]  = expectedInput.ToString(),
                [$"Anthropic:Pricing:{model}:OutputPer1KTokens"] = expectedOutput.ToString(),
            })
            .Build();

        var pricing = AiPricingOptions.GetProviderPricing(configuration, "Anthropic", model);

        pricing.Should().NotBeNull();
        pricing!.InputPer1KTokens.Should().Be(expectedInput);
        pricing.OutputPer1KTokens.Should().Be(expectedOutput);
    }

    [Fact]
    public void Pricing_NonZeroConfig_ProducesNonZeroCost()
    {
        var pricing = new AiModelPricing(InputPer1KTokens: 0.003m, OutputPer1KTokens: 0.015m);

        var cost = AiPricingOptions.EstimateCostUsd(1000, 500, pricing);

        cost.Should().BeGreaterThan(0);
        cost.Should().Be(0.003m + 0.0075m);
    }
}
