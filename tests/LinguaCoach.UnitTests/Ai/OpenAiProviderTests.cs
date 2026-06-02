using LinguaCoach.Application.Ai;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Ai;

public sealed class OpenAiProviderTests
{
    [Fact]
    public async Task CompleteAsync_WithoutApiKey_ReturnsControlledProviderException()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = ""
            })
            .Build();
        var provider = new OpenAiProvider(configuration, NullLogger<OpenAiProvider>.Instance);
        var request = new AiRequest("test.prompt", "Hello", 100);

        var exception = await Assert.ThrowsAsync<AiProviderException>(
            () => provider.CompleteAsync(request));

        Assert.Contains("API key is not configured", exception.Message);
    }
}
