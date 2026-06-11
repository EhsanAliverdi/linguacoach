using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Speaking;

/// <summary>
/// Calls the Qwen/DashScope CosyVoice TTS API.
/// Endpoint: POST {endpoint}/services/aigc/text2audiov2/generation
/// Endpoint priority: DB credential ApiEndpoint → Qwen:DashScope config → global DashScope fallback.
/// On any failure returns TtsResult with Success=false — never throws.
/// </summary>
internal sealed class QwenTextToSpeechService : ITextToSpeechService
{
    private const string DefaultDashScopeBase = "https://dashscope.aliyuncs.com/api/v1";
    private const string DefaultModel = "cosyvoice-v2";
    private const string DefaultVoice = "longxiaochun_v2";

    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _services;
    private readonly ILogger<QwenTextToSpeechService> _logger;

    public QwenTextToSpeechService(
        IConfiguration configuration,
        IServiceProvider services,
        ILogger<QwenTextToSpeechService> logger)
    {
        _configuration = configuration;
        _services = services;
        _logger = logger;
    }

    public async Task<TtsResult> GenerateSpeechAsync(
        string text,
        TextToSpeechOptions options,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var apiKey = _configuration["Qwen:ApiKey"]
            ?? Environment.GetEnvironmentVariable("QWEN_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Qwen TTS: no API key configured — returning failure result");
            return Fail(options, sw, "Qwen API key not configured.");
        }

        // Endpoint priority: DB credential ApiEndpoint (workspace-specific) → config → global default
        var storedEndpoint = GetStoredEndpoint();
        var dashScopeBase = storedEndpoint
            ?? _configuration["Qwen:DashScope"]
            ?? DefaultDashScopeBase;
        var model = string.IsNullOrWhiteSpace(options.Model) ? DefaultModel : options.Model;
        var voice = options.Voice ?? DefaultVoice;

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            // DashScope requires X-DashScope-Async header for streaming; use sync mode
            http.DefaultRequestHeaders.Add("X-DashScope-DataInspection", "enable");

            var requestBody = new
            {
                model,
                input = new { text },
                parameters = new { voice, format = "wav", sample_rate = 22050 }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var url = $"{dashScopeBase.TrimEnd('/')}/services/aigc/text2audiov2/generation";
            using var response = await http.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Qwen TTS failed Status={Status} Body={Body}",
                    (int)response.StatusCode, errorBody);
                return Fail(options, sw, $"Qwen TTS API returned {(int)response.StatusCode}.");
            }

            // DashScope TTS returns audio bytes directly when format=wav
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (contentType.Contains("audio") || contentType.Contains("octet-stream"))
            {
                var audioBytes = await response.Content.ReadAsByteArrayAsync(ct);
                sw.Stop();
                _logger.LogInformation(
                    "Qwen TTS generated audio Voice={Voice} DurationMs={DurationMs} Bytes={Bytes}",
                    voice, sw.ElapsedMilliseconds, audioBytes.Length);

                return new TtsResult(
                    Success: true,
                    AudioBytes: audioBytes,
                    AudioContentType: "audio/wav",
                    Provider: "qwen",
                    Voice: voice,
                    DurationMs: sw.ElapsedMilliseconds);
            }

            // JSON response — may contain audio_url or base64 audio
            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("output", out var output) &&
                output.TryGetProperty("audio", out var audioEl))
            {
                var base64Audio = audioEl.GetString();
                if (!string.IsNullOrWhiteSpace(base64Audio))
                {
                    var audioBytes = Convert.FromBase64String(base64Audio);
                    sw.Stop();
                    return new TtsResult(
                        Success: true,
                        AudioBytes: audioBytes,
                        AudioContentType: "audio/wav",
                        Provider: "qwen",
                        Voice: voice,
                        DurationMs: sw.ElapsedMilliseconds);
                }
            }

            return Fail(options, sw, "Qwen TTS: unexpected response format.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Qwen TTS threw an exception Voice={Voice}", voice);
            return Fail(options, sw, ex.Message);
        }
    }

    private string? GetStoredEndpoint()
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var cred = db.AiProviderCredentials.AsNoTracking()
                .FirstOrDefault(c => c.ProviderName == "qwen");
            return cred?.ApiEndpoint;
        }
        catch
        {
            return null;
        }
    }

    private static TtsResult Fail(TextToSpeechOptions options, Stopwatch sw, string reason) =>
        new(Success: false, AudioBytes: null, AudioContentType: "audio/wav",
            Provider: "qwen", Voice: options.Voice ?? DefaultVoice,
            DurationMs: sw.ElapsedMilliseconds, FailureReason: reason);
}
