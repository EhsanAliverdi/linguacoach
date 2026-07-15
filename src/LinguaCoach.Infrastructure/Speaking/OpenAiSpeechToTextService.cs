using System.ClientModel;
using System.Diagnostics;
using LinguaCoach.Application.Speaking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Audio;

namespace LinguaCoach.Infrastructure.Speaking;

/// <summary>
/// Phase 4 (2026-07-15), task #32 — real OpenAI Whisper transcription as a standalone,
/// reusable <see cref="ISpeechToTextService"/>. The only prior real Whisper call in this codebase
/// lived inline/private inside <see cref="OpenAiSpeakingEvaluationProvider"/> (speaking-turn
/// scoring); this extracts the same <c>AudioClient.TranscribeAudioAsync</c> call into a
/// standalone service so the Import Package background job (and any future caller) can use it
/// without depending on the speaking-evaluation feature. Uses OpenAI:ApiKey / OPENAI_API_KEY —
/// same config precedent as every other OpenAI-backed provider in this codebase.
/// </summary>
public sealed class OpenAiSpeechToTextService : ISpeechToTextService
{
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly long _maxAudioSizeBytes;
    private readonly ILogger<OpenAiSpeechToTextService> _logger;

    public OpenAiSpeechToTextService(IConfiguration configuration, ILogger<OpenAiSpeechToTextService> logger)
    {
        _logger = logger;
        _apiKey = configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        _model = configuration["OpenAI:TranscriptionModel"] ?? "whisper-1";
        _maxAudioSizeBytes = configuration.GetValue<long?>("OpenAI:MaxTranscriptionAudioBytes") ?? 25_000_000; // Whisper API's own 25MB cap
    }

    public bool IsSupported => _apiKey is not null;

    public async Task<SpeechToTextResult> TranscribeAsync(
        Stream audioStream, SpeechToTextOptions options, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        if (!IsSupported)
            return new SpeechToTextResult(false, null, "openai", sw.ElapsedMilliseconds, "OpenAI API key is not configured.");

        try
        {
            using var buffer = new MemoryStream();
            await audioStream.CopyToAsync(buffer, ct);

            if (buffer.Length > _maxAudioSizeBytes)
            {
                return new SpeechToTextResult(false, null, "openai", sw.ElapsedMilliseconds,
                    $"Audio size {buffer.Length:N0} bytes exceeds the {_maxAudioSizeBytes:N0}-byte transcription limit.");
            }
            buffer.Seek(0, SeekOrigin.Begin);

            var credential = new ApiKeyCredential(_apiKey!);
            var audioClient = new AudioClient(_model, credential);
            var filename = "audio" + MimeTypeToExtension(options.AudioMimeType);

            var transcriptionOptions = new AudioTranscriptionOptions();
            var result = await audioClient.TranscribeAudioAsync(buffer, filename, transcriptionOptions, ct);
            var text = result.Value.Text ?? string.Empty;

            return new SpeechToTextResult(true, text, "openai", sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAiSpeechToTextService: transcription failed (non-blocking).");
            return new SpeechToTextResult(false, null, "openai", sw.ElapsedMilliseconds, $"Transcription failed: {ex.GetType().Name}");
        }
    }

    private static string MimeTypeToExtension(string mimeType) => mimeType.Split(';')[0].Trim().ToLowerInvariant() switch
    {
        "audio/mpeg" or "audio/mp3" => ".mp3",
        "audio/wav" or "audio/x-wav" => ".wav",
        "audio/mp4" or "audio/x-m4a" => ".m4a",
        "audio/ogg" => ".ogg",
        "audio/webm" => ".webm",
        _ => ".mp3",
    };
}
