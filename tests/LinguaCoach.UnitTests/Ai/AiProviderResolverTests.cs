using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Ai;

public sealed class AiProviderResolverTests
{
    [Fact]
    public void ResolveWritingFeedbackProvider_WhenOpenAiSelected_ReturnsOpenAiProvider()
    {
        var resolver = BuildResolver(new Dictionary<string, string?>
        {
            ["AI:WritingFeedback:Provider"] = "OpenAI",
            ["AI:WritingFeedback:Model"] = "gpt-4o-mini",
            ["OpenAI:ApiKey"] = "test-openai-key"
        });

        var selection = resolver.ResolveWritingFeedbackProvider();

        selection.ProviderName.Should().Be("openai");
        selection.ModelName.Should().Be("gpt-4o-mini");
        selection.Provider.Should().BeOfType<OpenAiProvider>();
    }

    [Fact]
    public void ResolveWritingFeedbackProvider_WhenGeminiSelected_ReturnsGeminiProvider()
    {
        var resolver = BuildResolver(new Dictionary<string, string?>
        {
            ["AI:WritingFeedback:Provider"] = "Gemini",
            ["AI:WritingFeedback:Model"] = "gemini-2.0-flash",
            ["Gemini:ApiKey"] = "test-gemini-key"
        });

        var selection = resolver.ResolveWritingFeedbackProvider();

        selection.ProviderName.Should().Be("gemini");
        selection.ModelName.Should().Be("gemini-2.0-flash");
        selection.Provider.Should().BeOfType<GeminiProvider>();
    }

    [Fact]
    public void ResolveWritingFeedbackProvider_WhenProviderConfigMissingInProduction_ThrowsUnavailable()
    {
        var resolver = BuildResolver(new Dictionary<string, string?>(), environmentName: Environments.Production);

        var act = () => resolver.ResolveWritingFeedbackProvider();

        act.Should().Throw<AiConfigurationUnavailableException>()
            .WithMessage("*provider and model must be configured*");
    }

    [Fact]
    public void ResolveWritingFeedbackProvider_WhenSelectedApiKeyMissing_ThrowsUnavailable()
    {
        var resolver = BuildResolver(new Dictionary<string, string?>
        {
            ["AI:WritingFeedback:Provider"] = "Gemini",
            ["AI:WritingFeedback:Model"] = "gemini-2.0-flash"
        });

        var act = () => resolver.ResolveWritingFeedbackProvider();

        act.Should().Throw<AiConfigurationUnavailableException>()
            .WithMessage("*API key is not configured*");
    }

    private static AiProviderResolver BuildResolver(
        Dictionary<string, string?> settings,
        string environmentName = "Production")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var environment = new FakeHostEnvironment(environmentName);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddSingleton(NullLogger<OpenAiProvider>.Instance);
        services.AddSingleton(NullLogger<GeminiProvider>.Instance);
        services.AddSingleton(NullLogger<AnthropicProvider>.Instance);
        services.AddSingleton<OpenAiProvider>();
        services.AddHttpClient<GeminiProvider>();
        services.AddSingleton<AnthropicProvider>();
        var provider = services.BuildServiceProvider();

        return new AiProviderResolver(
            configuration,
            environment,
            provider,
            NullLogger<AiProviderResolver>.Instance);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
