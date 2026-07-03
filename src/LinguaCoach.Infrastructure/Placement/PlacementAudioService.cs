using LinguaCoach.Application.Placement;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Speaking;
using LinguaCoach.Persistence;
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
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<PlacementAudioService> _logger;

    public PlacementAudioService(
        TtsProviderResolver ttsResolver,
        IConfiguration configuration,
        LinguaCoachDbContext db,
        ILogger<PlacementAudioService> logger)
    {
        _ttsResolver = ttsResolver;
        _configuration = configuration;
        _db = db;
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
            var (tts, options) = _ttsResolver.Resolve("tts.placement", "tts.placement", "en-GB");

            var started = DateTime.UtcNow;
            var result = await tts.GenerateSpeechAsync(
                audioScript,
                options,
                ct);
            var durationMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;

            // Same gap as ListeningAudioService (found live 2026-07-03): TTS calls bypass
            // AiExecutionService's usage logging entirely, so nothing here ever wrote a
            // ai_usage_logs row despite the TTS provider call succeeding.
            await LogTtsUsageAsync("tts.placement", options.Model, result, durationMs, ct);

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

    private async Task LogTtsUsageAsync(
        string featureKey, string? model, TtsResult result, long durationMs, CancellationToken ct)
    {
        try
        {
            _db.AiUsageLogs.Add(new AiUsageLog(
                studentProfileId: null,
                featureKey,
                string.IsNullOrWhiteSpace(result.Provider) ? "unknown" : result.Provider,
                string.IsNullOrWhiteSpace(model) ? "unknown" : model,
                isFallback: false,
                wasSuccessful: result.Success,
                failureReason: result.FailureReason,
                inputTokens: 0,
                outputTokens: 0,
                costUsd: 0m,
                durationMs,
                correlationId: null));

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write TTS usage log for {FeatureKey}", featureKey);
        }
    }
}

public sealed record PlacementAudioFile(byte[] Bytes, string ContentType);
