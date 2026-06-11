using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using LinguaCoach.Application.Speaking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Speaking;

/// <summary>
/// Calls the Gemini TTS API (POST /v1beta/models/{model}:generateContent with audio response).
/// Returns audio/wav. On any failure returns TtsResult with Success=false — never throws.
/// Activated only when TtsProviderResolver selects the "gemini" provider.
/// </summary>
internal sealed class GeminiTextToSpeechService : ITextToSpeechService
{
    private const string DefaultModel = "gemini-2.5-flash-preview-tts";
    private const string DefaultVoice = "Kore";
    private const string BaseUrl = "https://generativelanguage.googleapis.com";

    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiTextToSpeechService> _logger;

    public GeminiTextToSpeechService(
        IConfiguration configuration,
        ILogger<GeminiTextToSpeechService> logger)
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
        var apiKey = options.ApiKeyOverride
            ?? _configuration["Gemini:ApiKey"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Gemini TTS: no API key configured — returning failure result");
            return Fail(options, sw, "Gemini API key not configured.");
        }

        var model = string.IsNullOrWhiteSpace(options.Model) ? DefaultModel : options.Model;
        var voice = options.Voice ?? DefaultVoice;

        try
        {
            using var http = new HttpClient();
            var url = $"{BaseUrl}/v1beta/models/{model}:generateContent?key={apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text } } }
                },
                generationConfig = new
                {
                    responseModalities = new[] { "AUDIO" },
                    speechConfig = new
                    {
                        voiceConfig = new
                        {
                            prebuiltVoiceConfig = new { voiceName = voice }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gemini TTS failed Status={Status} Body={Body}",
                    (int)response.StatusCode, errorBody);
                return Fail(options, sw, $"Gemini TTS API returned {(int)response.StatusCode}.");
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            // Gemini returns: candidates[0].content.parts[0].inlineData.data (base64 PCM)
            var base64Audio = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("inlineData")
                .GetProperty("data")
                .GetString();

            if (string.IsNullOrWhiteSpace(base64Audio))
                return Fail(options, sw, "Gemini TTS: empty audio data in response.");

            var pcmBytes = Convert.FromBase64String(base64Audio);
            var wavBytes = PcmToWav(pcmBytes, sampleRate: 24000, channels: 1, bitsPerSample: 16);

            sw.Stop();
            _logger.LogInformation(
                "Gemini TTS generated audio Voice={Voice} DurationMs={DurationMs} Bytes={Bytes}",
                voice, sw.ElapsedMilliseconds, wavBytes.Length);

            return new TtsResult(
                Success: true,
                AudioBytes: wavBytes,
                AudioContentType: "audio/wav",
                Provider: "gemini",
                Voice: voice,
                DurationMs: sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini TTS threw an exception Voice={Voice}", voice);
            return Fail(options, sw, ex.Message);
        }
    }

    private static TtsResult Fail(TextToSpeechOptions options, Stopwatch sw, string reason) =>
        new(Success: false, AudioBytes: null, AudioContentType: "audio/wav",
            Provider: "gemini", Voice: options.Voice ?? DefaultVoice,
            DurationMs: sw.ElapsedMilliseconds, FailureReason: reason);

    private static byte[] PcmToWav(byte[] pcm, int sampleRate, int channels, int bitsPerSample)
    {
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = channels * bitsPerSample / 8;
        using var ms = new System.IO.MemoryStream();
        using var bw = new System.IO.BinaryWriter(ms);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + pcm.Length);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);          // chunk size
        bw.Write((short)1);    // PCM
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)blockAlign);
        bw.Write((short)bitsPerSample);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(pcm.Length);
        bw.Write(pcm);
        return ms.ToArray();
    }
}
