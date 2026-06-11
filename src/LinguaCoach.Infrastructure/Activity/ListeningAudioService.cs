using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Speaking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

public sealed class ListeningAudioService
{
    private readonly TtsProviderResolver _ttsResolver;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ListeningAudioService> _logger;

    public ListeningAudioService(
        TtsProviderResolver ttsResolver,
        IConfiguration configuration,
        ILogger<ListeningAudioService> logger)
    {
        _ttsResolver = ttsResolver;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task EnsureAudioAsync(LearningActivity activity, string targetLanguageCode, CancellationToken ct)
    {
        if (activity.ActivityType != ActivityType.ListeningComprehension)
            return;

        var content = Parse(activity.AiGeneratedContentJson);
        if (content.Audio is { AudioAvailable: true } && !string.IsNullOrWhiteSpace(content.Audio.StorageKey))
            return;

        if (string.IsNullOrWhiteSpace(content.AudioScript))
        {
            content.Audio = ListeningAudioMetadata.Unavailable("Audio script was missing.");
            activity.UpdateContent(JsonSerializer.Serialize(content, JsonOptions()));
            return;
        }

        ITextToSpeechService tts;
        string? voice;
        string? model;
        try
        {
            (tts, voice, model) = await _ttsResolver.ResolveAsync("tts.listening", ct);
        }
        catch (AiServiceUnavailableException)
        {
            content.Audio = ListeningAudioMetadata.Unavailable("Audio service is not configured.");
            activity.UpdateContent(JsonSerializer.Serialize(content, JsonOptions()));
            return;
        }

        var result = await tts.GenerateSpeechAsync(
            content.AudioScript,
            new TextToSpeechOptions(targetLanguageCode, voice, model),
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
        var storageKey = $"{activity.Id:N}{extension}";
        var fullPath = GetAudioPath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, result.AudioBytes, ct);

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

        var fullPath = GetAudioPath(content.Audio.StorageKey);
        if (!File.Exists(fullPath))
            return null;

        var bytes = await File.ReadAllBytesAsync(fullPath, ct);
        return new ListeningAudioFile(bytes, content.Audio.ContentType ?? "audio/wav");
    }

    private string GetAudioPath(string storageKey)
    {
        var root = _configuration["Tts:AudioStoragePath"]
            ?? Environment.GetEnvironmentVariable("TTS_AUDIO_STORAGE_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "app-data", "audio");
        var safeName = Path.GetFileName(storageKey);
        return Path.GetFullPath(Path.Combine(root, safeName));
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
