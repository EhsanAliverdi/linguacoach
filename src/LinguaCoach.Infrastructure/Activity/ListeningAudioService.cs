using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Speaking;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

public sealed class ListeningAudioService
{
    private readonly TtsProviderResolver _ttsResolver;
    private readonly IFileStorageService _storage;
    private readonly ILogger<ListeningAudioService> _logger;

    public ListeningAudioService(
        TtsProviderResolver ttsResolver,
        IFileStorageService storage,
        ILogger<ListeningAudioService> logger)
    {
        _ttsResolver = ttsResolver;
        _storage = storage;
        _logger = logger;
    }

    private static readonly HashSet<string> ListeningPatternKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        Domain.ExercisePatternKey.ListenAndAnswer,
        Domain.ExercisePatternKey.ListenAndGapFill,
        Domain.ExercisePatternKey.ListeningMultipleChoiceSingle,
        Domain.ExercisePatternKey.ListeningMultipleChoiceMulti,
        Domain.ExercisePatternKey.ListeningFillInBlanks,
        Domain.ExercisePatternKey.SelectMissingWord,
        Domain.ExercisePatternKey.HighlightCorrectSummary,
        Domain.ExercisePatternKey.HighlightIncorrectWords,
        Domain.ExercisePatternKey.WriteFromDictation,
        Domain.ExercisePatternKey.SummarizeSpokenText,
    };

    public async Task EnsureAudioAsync(LearningActivity activity, string targetLanguageCode, CancellationToken ct)
    {
        // Skip non-listening legacy types and non-listening pattern-keyed activities.
        var isLegacyListening = activity.ActivityType == ActivityType.ListeningComprehension
            && string.IsNullOrWhiteSpace(activity.ExercisePatternKey);
        var isListeningPatternKeyed = !string.IsNullOrWhiteSpace(activity.ExercisePatternKey)
            && ListeningPatternKeys.Contains(activity.ExercisePatternKey);
        if (!isLegacyListening && !isListeningPatternKeyed)
            return;

        var content = Parse(activity.AiGeneratedContentJson);
        if (content.Audio is { AudioAvailable: true } && !string.IsNullOrWhiteSpace(content.Audio.StorageKey))
            return;

        if (string.IsNullOrWhiteSpace(content.EffectiveAudioScript))
        {
            content.Audio = ListeningAudioMetadata.Unavailable("Audio script was missing.");
            activity.UpdateContent(JsonSerializer.Serialize(content, JsonOptions()));
            return;
        }

        ITextToSpeechService tts;
        TextToSpeechOptions ttsOptions;
        try
        {
            (tts, ttsOptions) = _ttsResolver.Resolve("tts.listening", "tts.listening", targetLanguageCode);
        }
        catch (AiConfigurationUnavailableException ex)
        {
            content.Audio = ListeningAudioMetadata.Unavailable(ex.Message);
            activity.UpdateContent(JsonSerializer.Serialize(content, JsonOptions()));
            return;
        }

        var result = await tts.GenerateSpeechAsync(
            content.EffectiveAudioScript!,
            ttsOptions,
            ct);

        if (!result.Success || result.AudioBytes is null || result.AudioBytes.Length == 0)
        {
            _logger.LogWarning(
                "Listening TTS failed ActivityId={ActivityId} Provider={Provider} FailureReason={FailureReason}",
                activity.Id, result.Provider, result.FailureReason);
            content.Audio = ListeningAudioMetadata.Unavailable(result.FailureReason ?? "Audio generation failed.");
            activity.UpdateContent(JsonSerializer.Serialize(content, JsonOptions()));
            return;
        }

        var extension = ContentTypeToExtension(result.AudioContentType);
        var storageKey = _storage.GenerateKey(activity.Id.ToString("N"), "tts-audio", extension);
        await using (var ms = new MemoryStream(result.AudioBytes))
        {
            await _storage.SaveAsync(storageKey, ms, result.AudioContentType, ct);
        }

        content.Audio = new ListeningAudioMetadata
        {
            AudioAvailable = true,
            StorageKey = storageKey,
            ContentType = result.AudioContentType,
            Provider = result.Provider,
            Voice = result.Voice,
            DurationMs = result.DurationMs
        };
        activity.UpdateContent(JsonSerializer.Serialize(content, JsonOptions()));
    }

    public async Task<ListeningAudioFile?> GetAudioAsync(LearningActivity activity, CancellationToken ct)
    {
        var content = Parse(activity.AiGeneratedContentJson);
        if (content.Audio is not { AudioAvailable: true } || string.IsNullOrWhiteSpace(content.Audio.StorageKey))
            return null;

        try
        {
            if (!await _storage.ExistsAsync(content.Audio.StorageKey, ct))
                return null;

            await using var stream = await _storage.ReadAsync(content.Audio.StorageKey, ct);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            return new ListeningAudioFile(ms.ToArray(), content.Audio.ContentType ?? "audio/wav");
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    /// <summary>The storage key for this activity's listening audio, or null if no audio is available.</summary>
    public string? GetStorageKey(LearningActivity activity)
    {
        var content = Parse(activity.AiGeneratedContentJson);
        return content.Audio is { AudioAvailable: true } && !string.IsNullOrWhiteSpace(content.Audio.StorageKey)
            ? content.Audio.StorageKey
            : null;
    }

    private static string ContentTypeToExtension(string contentType) => contentType switch
    {
        "audio/mpeg" or "audio/mp3" => ".mp3",
        _ => ".wav"
    };

    internal static ListeningContent Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ListeningContent>(json, JsonOptions()) ?? new ListeningContent();
        }
        catch
        {
            return new ListeningContent();
        }
    }

    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web);
}

public sealed record ListeningAudioFile(byte[] Bytes, string ContentType);

public sealed class ListeningContent
{
    public string? ActivityType { get; set; }
    public string? Title { get; set; }
    public string? Scenario { get; set; }
    public string? Instructions { get; set; }
    public string? SpeakerRole { get; set; }
    public string? ListenerRole { get; set; }
    public string? Difficulty { get; set; }
    public string? AudioScript { get; set; }
    public bool? TranscriptAvailableAfterSubmit { get; set; }
    public List<ListeningQuestionContent>? Questions { get; set; }
    public ListeningResponseTaskContent? ResponseTask { get; set; }
    public ListeningAudioMetadata? Audio { get; set; }

    /// <summary>Other top-level properties (schemaVersion, learnContent, practiceContent, feedbackPlan) preserved on round-trip.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    /// <summary>
    /// AudioScript for staged (module_stage_v1 / legacy_adapted_v1) content lives under
    /// practiceContent.exerciseData.audioScript; for raw legacy content it is at the root.
    /// </summary>
    [JsonIgnore]
    public string? EffectiveAudioScript
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(AudioScript))
                return AudioScript;

            if (ExtensionData is not null
                && ExtensionData.TryGetValue("schemaVersion", out var sv)
                && sv.GetString() is ModuleStageSchema.Version or ModuleStageSchema.LegacyAdaptedVersion
                && ExtensionData.TryGetValue("practiceContent", out var pc)
                && pc.TryGetProperty("exerciseData", out var ed)
                && ed.TryGetProperty("audioScript", out var script))
            {
                return script.GetString();
            }

            return null;
        }
    }
}

public sealed class ListeningQuestionContent
{
    public string? Id { get; set; }
    public string? Question { get; set; }
    public string? Type { get; set; }
    public string? ExpectedAnswer { get; set; }
}

public sealed class ListeningResponseTaskContent
{
    public string? Prompt { get; set; }
    public string? ExpectedFocus { get; set; }
}

public sealed class ListeningAudioMetadata
{
    public bool AudioAvailable { get; set; }
    public string? StorageKey { get; set; }
    public string? ContentType { get; set; }
    public string? Provider { get; set; }
    public string? Voice { get; set; }
    public long? DurationMs { get; set; }
    public string? UnavailableMessage { get; set; }

    public static ListeningAudioMetadata Unavailable(string reason) => new()
    {
        AudioAvailable = false,
        UnavailableMessage = "Audio is not available. You can still complete this text-based listening practice.",
    };
}
