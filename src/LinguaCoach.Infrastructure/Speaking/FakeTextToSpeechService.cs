using System.Diagnostics;
using LinguaCoach.Application.Speaking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Speaking;

internal sealed class FakeTextToSpeechService : ITextToSpeechService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FakeTextToSpeechService> _logger;

    public FakeTextToSpeechService(
        IConfiguration configuration,
        ILogger<FakeTextToSpeechService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<TtsResult> GenerateSpeechAsync(
        string text,
        TextToSpeechOptions options,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var enabledValue = Environment.GetEnvironmentVariable("TTS_ENABLED");
        var enabled = bool.TryParse(enabledValue, out var envEnabled)
            ? envEnabled
            : (_configuration.GetValue<bool?>("Tts:Enabled") ?? true);
        var voice = options.Voice
            ?? Environment.GetEnvironmentVariable("TTS_DEFAULT_VOICE")
            ?? _configuration["Tts:DefaultVoice"]
            ?? "fake-workplace-voice";

        if (!enabled)
        {
            return Task.FromResult(new TtsResult(
                Success: false,
                AudioBytes: null,
                AudioContentType: "audio/wav",
                Provider: "Fake",
                Voice: voice,
                DurationMs: sw.ElapsedMilliseconds,
                FailureReason: "TTS is disabled."));
        }

        var bytes = BuildSilentWav(durationSeconds: EstimateDurationSeconds(text));
        sw.Stop();
        _logger.LogInformation(
            "Fake TTS generated audio Provider={Provider} Voice={Voice} DurationMs={DurationMs}",
            "Fake", voice, sw.ElapsedMilliseconds);

        return Task.FromResult(new TtsResult(
            Success: true,
            AudioBytes: bytes,
            AudioContentType: "audio/wav",
            Provider: "Fake",
            Voice: voice,
            DurationMs: sw.ElapsedMilliseconds));
    }

    private static int EstimateDurationSeconds(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Clamp((int)Math.Ceiling(words / 2.6), 2, 30);
    }

    private static byte[] BuildSilentWav(int durationSeconds)
    {
        const int sampleRate = 8000;
        const short bitsPerSample = 16;
        const short channels = 1;
        var samples = sampleRate * durationSeconds;
        var dataLength = samples * channels * bitsPerSample / 8;
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write(bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
        return ms.ToArray();
    }
}
