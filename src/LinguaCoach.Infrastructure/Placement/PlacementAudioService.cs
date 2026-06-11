using LinguaCoach.Application.Placement;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Infrastructure.Speaking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>
/// Generates and retrieves TTS audio for the placement listening section.
/// Follows the same local-filesystem storage pattern as ListeningAudioService (Activity layer).
/// Audio is generated once per assessment and cached; repeated calls do not regenerate.
/// The frontend never receives the storage path — it uses the authenticated API endpoint.
/// See: docs/sprints/server-side-tts-placement-listening-sprint.md
/// </summary>
public sealed class PlacementAudioService
{
    private readonly TtsProviderResolver _ttsResolver;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlacementAudioService> _logger;

    public PlacementAudioService(
        TtsProviderResolver ttsResolver,
        IConfiguration configuration,
        ILogger<PlacementAudioService> logger)
    {
        _ttsResolver = ttsResolver;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Ensures TTS audio exists for the placement listening section.
    /// Returns (audioAvailable, audioUrl).
    /// audioUrl is a relative API path: /api/placement/audio/{assessmentId}/listening
    /// If TTS generation fails, returns (false, null) — placement still loads.
    /// </summary>
    public async Task<(bool AudioAvailable, string? AudioUrl)> EnsureListeningAudioAsync(
        Guid assessmentId,
        string audioScript,
        CancellationToken ct)
    {
        // Check for any existing file (wav or mp3).
        var existingPath = FindExistingAudioPath(assessmentId);
        if (existingPath is not null)
            return (true, BuildAudioUrl(assessmentId));

        try
        {
            var (tts, voice, model) = await _ttsResolver.ResolveAsync("tts.placement", ct);

            var result = await tts.GenerateSpeechAsync(
                audioScript,
                new TextToSpeechOptions(
                    TargetLanguageCode: "en-GB",
                    Voice: voice ?? "BritishEnglishMale",
                    Model: model),
                ct);

            if (!result.Success || result.AudioBytes is null || result.AudioBytes.Length == 0)
            {
                _logger.LogWarning(
                    "Placement listening TTS failed AssessmentId={AssessmentId} Provider={Provider} Reason={Reason}",
                    assessmentId, result.Provider, result.FailureReason);
                return (false, null);
            }

            var extension = result.AudioContentType == "audio/mpeg" ? ".mp3" : ".wav";
            var path = GetAudioPath(assessmentId, extension);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, result.AudioBytes, ct);

            _logger.LogInformation(
                "Placement listening audio generated AssessmentId={AssessmentId} Provider={Provider} Voice={Voice}",
                assessmentId, result.Provider, result.Voice);

            return (true, BuildAudioUrl(assessmentId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Placement listening audio generation threw AssessmentId={AssessmentId}", assessmentId);
            return (false, null);
        }
    }

    /// <summary>Returns audio bytes for streaming, or null if not found.</summary>
    public async Task<PlacementAudioFile?> GetListeningAudioAsync(Guid assessmentId, CancellationToken ct)
    {
        var path = FindExistingAudioPath(assessmentId);
        if (path is null) return null;

        var bytes = await File.ReadAllBytesAsync(path, ct);
        var contentType = path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? "audio/mpeg" : "audio/wav";
        return new PlacementAudioFile(bytes, contentType);
    }

    private string? FindExistingAudioPath(Guid assessmentId)
    {
        foreach (var ext in new[] { ".wav", ".mp3" })
        {
            var p = GetAudioPath(assessmentId, ext);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private string GetAudioPath(Guid assessmentId, string extension)
    {
        var root = _configuration["Tts:AudioStoragePath"]
            ?? Environment.GetEnvironmentVariable("TTS_AUDIO_STORAGE_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "app-data", "audio");
        return Path.GetFullPath(Path.Combine(root, "placement", assessmentId.ToString("N"), $"listening{extension}"));
    }

    private static string BuildAudioUrl(Guid assessmentId)
        => $"/api/placement/audio/{assessmentId}/listening";
}

public sealed record PlacementAudioFile(byte[] Bytes, string ContentType);
