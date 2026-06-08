using System.Diagnostics;
using LinguaCoach.Application.Speaking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Speaking;

/// <summary>
/// Fake STT provider for development and tests.
/// Returns a deterministic workplace English placeholder transcript.
/// Replace with a real provider (Whisper, Azure) in a later sprint.
/// </summary>
public sealed class FakeSpeechToTextService : ISpeechToTextService
{
    public const string PlaceholderTranscript =
        "I wanted to update you about the delay. The supplier is late, and I will send the revised timeline today.";

    private readonly IConfiguration _configuration;
    private readonly ILogger<FakeSpeechToTextService> _logger;

    public FakeSpeechToTextService(IConfiguration configuration, ILogger<FakeSpeechToTextService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<SpeechToTextResult> TranscribeAsync(
        Stream audioStream,
        SpeechToTextOptions options,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var enabledRaw = Environment.GetEnvironmentVariable("STT_ENABLED");
        var enabled = bool.TryParse(enabledRaw, out var envEnabled)
            ? envEnabled
            : (_configuration.GetValue<bool?>("Stt:Enabled") ?? true);

        if (!enabled)
        {
            return Task.FromResult(new SpeechToTextResult(
                Success: false,
                Transcript: null,
                Provider: "Fake",
                DurationMs: sw.ElapsedMilliseconds,
                FailureReason: "STT is disabled."));
        }

        sw.Stop();
        _logger.LogInformation(
            "Fake STT transcribed audio Provider=Fake MimeType={MimeType} DurationMs={DurationMs}",
            options.AudioMimeType, sw.ElapsedMilliseconds);

        return Task.FromResult(new SpeechToTextResult(
            Success: true,
            Transcript: PlaceholderTranscript,
            Provider: "Fake",
            DurationMs: sw.ElapsedMilliseconds));
    }
}
