using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using LinguaCoach.Application.Speaking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Speaking;

/// <summary>
/// Calls the OpenAI TTS API (POST /v1/audio/speech).
/// Returns audio/mpeg. On any failure returns TtsResult with Success=false — never throws.
/// Activated only when TtsProviderResolver selects the "openai" provider.
/// </summary>
internal sealed class OpenAiTextToSpeechService : ITextToSpeechService
{
    private const string DefaultVoice = "onyx";
    private const string DefaultModel = "tts-1";
    private const string ApiUrl = "https://api.openai.com/v1/audio/speech";

    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiTextToSpeechService> _logger;

    public OpenAiTextToSpeechService(
        IConfiguration configuration,
        ILogger<OpenAiTextToSpeechService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TtsResult> GenerateSpeechAsync(
        string text,
        TextToSpeechOptions options,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var apiKey = _configuration["OpenAi:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("OpenAI TTS: no API key configured — returning failure result");
            return new TtsResult(
                Success: false,
                AudioBytes: null,
                AudioContentType: "audio/mpeg",
                Provider: "openai",
                Voice: options.Voice ?? DefaultVoice,
                DurationMs: sw.ElapsedMilliseconds,
                FailureReason: "OpenAI API key not configured.");
        }

        var voice = options.Voice ?? DefaultVoice;

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var body = JsonSerializer.Serialize(new
            {
                model = DefaultModel,
                input = text,
                voice = voice,
                response_format = "mp3"
            });

            using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(ApiUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "OpenAI TTS failed Status={Status} Body={Body}",
                    (int)response.StatusCode, errorBody);
                return new TtsResult(
                    Success: false,
                    AudioBytes: null,
                    AudioContentType: "audio/mpeg",
                    Provider: "openai",
                    Voice: voice,
                    DurationMs: sw.ElapsedMilliseconds,
                    FailureReason: $"OpenAI TTS API returned {(int)response.StatusCode}.");
            }

            var audioBytes = await response.Content.ReadAsByteArrayAsync(ct);
            sw.Stop();

            _logger.LogInformation(
                "OpenAI TTS generated audio Voice={Voice} DurationMs={DurationMs} Bytes={Bytes}",
                voice, sw.ElapsedMilliseconds, audioBytes.Length);

            return new TtsResult(
                Success: true,
                AudioBytes: audioBytes,
                AudioContentType: "audio/mpeg",
                Provider: "openai",
                Voice: voice,
                DurationMs: sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI TTS threw an exception Voice={Voice}", voice);
            return new TtsResult(
                Success: false,
                AudioBytes: null,
                AudioContentType: "audio/mpeg",
                Provider: "openai",
                Voice: voice,
                DurationMs: sw.ElapsedMilliseconds,
                FailureReason: ex.Message);
        }
    }
}
