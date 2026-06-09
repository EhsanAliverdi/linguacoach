using LinguaCoach.Application.Placement;
using LinguaCoach.Application.Speaking;
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
    private readonly ITextToSpeechService _tts;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlacementAudioService> _logger;

    public PlacementAudioService(
        ITextToSpeechService tts,
        IConfiguration configuration,
        ILogger<PlacementAudioService> logger)
    {
        _tts = tts;
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
        var path = GetAudioPath(assessmentId);

        if (File.Exists(path))
            return (true, BuildAudioUrl(assessmentId));

        try
        {
            var result = await _tts.GenerateSpeechAsync(
                audioScript,
                new TextToSpeechOptions(
                    TargetLanguageCode: "en-GB",
                    Voice: "BritishEnglishMale"),
                ct);

            if (!result.Success || result.AudioBytes is null || result.AudioBytes.Length == 0)
            {
                _logger.LogWarning(
                    "Placement listening TTS failed AssessmentId={AssessmentId} Provider={Provider} Reason={Reason}",
                    assessmentId, result.Provider, result.FailureReason);
                return (false, null);
            }

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
        var path = GetAudioPath(assessmentId);
        if (!File.Exists(path)) return null;

        var bytes = await File.ReadAllBytesAsync(path, ct);
        return new PlacementAudioFile(bytes, "audio/wav");
    }

    private string GetAudioPath(Guid assessmentId)
    {
        var root = _configuration["Tts:AudioStoragePath"]
            ?? Environment.GetEnvironmentVariable("TTS_AUDIO_STORAGE_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "app-data", "audio");
        return Path.GetFullPath(Path.Combine(root, "placement", assessmentId.ToString("N"), "listening.wav"));
    }

    private static string BuildAudioUrl(Guid assessmentId)
        => $"/api/placement/audio/{assessmentId}/listening";
}

public sealed record PlacementAudioFile(byte[] Bytes, string ContentType);
