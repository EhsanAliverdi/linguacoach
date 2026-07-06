using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Placement;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Speaking;
using LinguaCoach.Persistence;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>
/// Generates and retrieves TTS audio for listening-skill items in the live adaptive
/// placement engine (Phase 20I-5). Mirrors ListeningAudioService's shape (Activity layer):
/// uses IFileStorageService (Minio-backed in prod, unlike the legacy PlacementAudioService's
/// hand-rolled local-filesystem storage) and logs usage directly, since TTS calls bypass
/// AiExecutionService's shared usage-logging wrapper.
/// </summary>
public sealed class AdaptivePlacementAudioService
{
    private readonly TtsProviderResolver _ttsResolver;
    private readonly IFileStorageService _storage;
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<AdaptivePlacementAudioService> _logger;

    public AdaptivePlacementAudioService(
        TtsProviderResolver ttsResolver,
        IFileStorageService storage,
        LinguaCoachDbContext db,
        ILogger<AdaptivePlacementAudioService> logger)
    {
        _ttsResolver = ttsResolver;
        _storage = storage;
        _db = db;
        _logger = logger;
    }

    /// <summary>Generates audio for the item's listening script (backend-only, sourced from the
    /// scoring rules snapshot — never rendered to the student as text) if not already generated.
    /// No-op if there's no script.</summary>
    public async Task EnsureAudioAsync(PlacementAssessmentItem item, string targetLanguageCode, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(item.AudioStorageKey)) return;

        var script = ExtractListeningScript(item.ScoringRulesJsonSnapshot);
        if (string.IsNullOrWhiteSpace(script)) return;

        ITextToSpeechService tts;
        TextToSpeechOptions ttsOptions;
        try
        {
            (tts, ttsOptions) = _ttsResolver.Resolve("tts.placement", "tts.placement", targetLanguageCode);
        }
        catch (AiConfigurationUnavailableException ex)
        {
            _logger.LogWarning(ex, "Adaptive placement TTS unavailable for item {ItemId}", item.Id);
            return;
        }

        var started = DateTime.UtcNow;
        var result = await tts.GenerateSpeechAsync(script, ttsOptions, ct);
        var durationMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;

        await LogTtsUsageAsync("tts.placement", ttsOptions.Model ?? result.Voice, result, durationMs, ct);

        if (!result.Success || result.AudioBytes is null || result.AudioBytes.Length == 0)
        {
            _logger.LogWarning(
                "Adaptive placement TTS failed ItemId={ItemId} Provider={Provider} Reason={Reason}",
                item.Id, result.Provider, result.FailureReason);
            return;
        }

        var extension = result.AudioContentType == "audio/mpeg" ? ".mp3" : ".wav";
        var storageKey = _storage.GenerateKey(item.Id.ToString("N"), "placement-audio", extension);
        await using (var ms = new MemoryStream(result.AudioBytes))
        {
            await _storage.SaveAsync(storageKey, ms, result.AudioContentType, ct);
        }

        item.RecordAudio(storageKey, result.AudioContentType);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PlacementItemAudioFile?> GetAudioAsync(PlacementAssessmentItem item, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(item.AudioStorageKey)) return null;

        if (!await _storage.ExistsAsync(item.AudioStorageKey, ct)) return null;

        await using var stream = await _storage.ReadAsync(item.AudioStorageKey, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return new PlacementItemAudioFile(ms.ToArray(), item.AudioContentType ?? "audio/wav");
    }

    private static string? ExtractListeningScript(string? scoringRulesJsonSnapshot)
    {
        if (string.IsNullOrWhiteSpace(scoringRulesJsonSnapshot)) return null;
        try
        {
            var doc = JsonSerializer.Deserialize<ScoringRulesDocument>(
                scoringRulesJsonSnapshot, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return doc?.ListeningAudioScript;
        }
        catch (JsonException)
        {
            return null;
        }
    }

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

public sealed record PlacementItemAudioFile(byte[] Bytes, string ContentType);
