using System.Diagnostics;
using LinguaCoach.Application.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Ai;

public sealed class AiProviderTester : IAiProviderTester
{
    private const string TestPromptKey = "admin.test";
    private const string TestPrompt = "Reply with just the word: OK";
    private const int MaxOutputTokens = 256;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiProviderTester> _logger;

    public AiProviderTester(IServiceProvider serviceProvider, ILogger<AiProviderTester> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ModelTestOutcome>> TestAllModelsAsync(
        string providerName,
        IEnumerable<string> models,
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

        var results = new List<ModelTestOutcome>();

        foreach (var model in models)
        {
            var request = new AiRequest(TestPromptKey, TestPrompt, MaxOutputTokens,
                ModelHint: model, ApiKeyOverride: apiKeyOverride);

            var sw = Stopwatch.StartNew();
            try
            {
                await provider.CompleteAsync(request, ct);
                sw.Stop();
                _logger.LogInformation("Model test OK: {Provider}/{Model} in {Ms}ms",
                    providerName, model, sw.ElapsedMilliseconds);
                results.Add(new ModelTestOutcome(model, true, (int)sw.ElapsedMilliseconds, null));
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                sw.Stop();
                var msg = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message;
                _logger.LogWarning("Model test FAILED: {Provider}/{Model} — {Error}", providerName, model, msg);
                results.Add(new ModelTestOutcome(model, false, (int)sw.ElapsedMilliseconds, msg));
            }
        }

        return results;
    }
}
