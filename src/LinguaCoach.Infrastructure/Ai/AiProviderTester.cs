using System.Diagnostics;
using LinguaCoach.Application.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Ai;

public sealed class AiProviderTester : IAiProviderTester
{
    private const string TestPromptKey = "admin.test";
    private const string TestPrompt = "Reply with the single word: OK";
    private const int MaxOutputTokens = 10;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiProviderTester> _logger;

    public AiProviderTester(IServiceProvider serviceProvider, ILogger<AiProviderTester> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<(bool Ok, int LatencyMs, string? Error)> TestAsync(
        string providerName,
        string? apiKeyOverride,
        CancellationToken ct = default)
    {
        IAiProvider provider = providerName.ToLowerInvariant() switch
        {
            "openai" => _serviceProvider.GetRequiredService<OpenAiProvider>(),
            "gemini" => _serviceProvider.GetRequiredService<GeminiProvider>(),
            "anthropic" => _serviceProvider.GetRequiredService<AnthropicProvider>(),
            _ => throw new ArgumentException($"Unknown provider '{providerName}'.")
        };

        var request = new AiRequest(TestPromptKey, TestPrompt, MaxOutputTokens, ApiKeyOverride: apiKeyOverride);

        var sw = Stopwatch.StartNew();
        try
        {
            await provider.CompleteAsync(request, ct);
            sw.Stop();
            _logger.LogInformation("Provider test OK: {Provider} in {Ms}ms", providerName, sw.ElapsedMilliseconds);
            return (true, (int)sw.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var msg = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message;
            _logger.LogWarning("Provider test FAILED: {Provider} — {Error}", providerName, msg);
            return (false, (int)sw.ElapsedMilliseconds, msg);
        }
    }
}
