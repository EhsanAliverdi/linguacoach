using System.ClientModel;
using System.Text.Json;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Audio;
using OpenAI.Chat;

namespace LinguaCoach.Infrastructure.Speaking;

/// <summary>
/// OpenAI-backed speaking evaluation provider.
/// Two-step pipeline: Whisper transcription → GPT rubric evaluation.
/// Uses OpenAI:ApiKey / OPENAI_API_KEY from config.
/// Pronunciation scoring is NOT claimed — Whisper provides transcription only.
/// All learner-facing feedback includes an AI-assisted disclaimer.
/// </summary>
public sealed class OpenAiSpeakingEvaluationProvider : ISpeakingEvaluationProvider
{
    public string ProviderName => "openai";
    public bool IsSupported => _apiKey is not null;
    public SpeakingEvaluationProviderCapabilities Capabilities => SpeakingEvaluationProviderCapabilities.OpenAiWhisperGpt;

    private readonly string? _apiKey;
    private readonly SpeakingEvaluationOptions _options;
    private readonly IFileStorageService _storage;
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<OpenAiSpeakingEvaluationProvider> _logger;

    private const string EvaluationSystemPrompt =
        "You are an AI English language coach evaluating a learner's spoken response. " +
        "Rate the response on the dimensions below. Return ONLY valid JSON — no markdown, no explanation. " +
        "This feedback is AI-assisted and approximate. Do not make clinical or official CEFR claims. " +
        "Do not use 'native-like' language. Keep feedback supportive and learner-friendly.";

    public OpenAiSpeakingEvaluationProvider(
        IConfiguration configuration,
        IOptions<SpeakingEvaluationOptions> options,
        IFileStorageService storage,
        LinguaCoachDbContext db,
        ILogger<OpenAiSpeakingEvaluationProvider> logger)
    {
        _options = options.Value;
        _storage = storage;
        _db = db;
        _logger = logger;

        _apiKey = configuration["OpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning(
                "OpenAiSpeakingEvaluationProvider: OpenAI API key not configured. " +
                "IsSupported=false. Set OpenAI:ApiKey or OPENAI_API_KEY.");
            _apiKey = null;
        }
    }

    public async Task<SpeakingEvaluationProviderResult> EvaluateAsync(
        SpeakingEvaluationRequest request,
        CancellationToken ct = default)
    {
        if (!IsSupported)
            return Fail("OpenAI API key is not configured.");

        if (string.IsNullOrWhiteSpace(request.AudioStorageKey))
            return Fail("No audio storage key on this attempt.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var linked = cts.Token;

        var credential = new ApiKeyCredential(_apiKey!);

        // Step 1: transcribe via Whisper
        string transcript;
        try
        {
            transcript = await TranscribeAsync(request.AudioStorageKey, credential, linked);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fail("Evaluation timed out during transcription.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "OpenAiSpeakingEvaluationProvider: transcription failed AttemptId={AttemptId}", request.AttemptId);
            return Fail($"Transcription failed: {ex.GetType().Name}");
        }

        if (string.IsNullOrWhiteSpace(transcript))
            return Fail("Transcription returned empty text.");

        await LogUsageAsync("speaking.transcribe", "openai", _options.TranscriptionModel,
            true, null, 0, 0, 0m, 0, request.CorrelationId, linked);

        // Step 2: evaluate transcript with GPT rubric
        EvaluationResult? evalResult;
        int inputTokens = 0, outputTokens = 0;
        decimal costUsd = 0m;
        long durationMs = 0;
        try
        {
            var started = DateTime.UtcNow;
            (evalResult, inputTokens, outputTokens, costUsd) =
                await EvaluateTranscriptAsync(transcript, request, credential, linked);
            durationMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fail("Evaluation timed out during rubric scoring.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "OpenAiSpeakingEvaluationProvider: GPT evaluation failed AttemptId={AttemptId}", request.AttemptId);
            return Fail($"Evaluation scoring failed: {ex.GetType().Name}");
        }

        if (evalResult is null)
        {
            await LogUsageAsync("speaking.evaluate", "openai", _options.Model,
                false, "malformed_response", 0, 0, 0m, durationMs, request.CorrelationId, linked);
            return Fail("Provider returned a malformed evaluation response.");
        }

        await LogUsageAsync("speaking.evaluate", "openai", _options.Model,
            true, null, inputTokens, outputTokens, costUsd, durationMs, request.CorrelationId, linked);

        _logger.LogInformation(
            "OpenAiSpeakingEvaluationProvider: complete AttemptId={AttemptId} Score={Score}",
            request.AttemptId, evalResult.OverallScore);

        return new SpeakingEvaluationProviderResult(
            Success: true,
            Transcript: transcript,
            OverallScore: evalResult.OverallScore,
            FluencyScore: evalResult.FluencyScore,
            PronunciationScore: null,
            CompletenessScore: evalResult.CompletenessScore,
            RelevanceScore: evalResult.RelevanceScore,
            FeedbackText: evalResult.FeedbackText,
            SuggestedImprovement: evalResult.SuggestedImprovement,
            FailureReason: null,
            ModelName: _options.Model,
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            CostUsd: costUsd);
    }

    private async Task<string> TranscribeAsync(
        string storageKey, ApiKeyCredential credential, CancellationToken ct)
    {
        await using var audioStream = await _storage.ReadAsync(storageKey, ct);

        using var buffer = new MemoryStream();
        await audioStream.CopyToAsync(buffer, ct);

        if (buffer.Length > _options.MaxAudioSizeBytes)
            throw new InvalidOperationException(
                $"Audio size {buffer.Length:N0} bytes exceeds limit {_options.MaxAudioSizeBytes:N0} bytes.");

        buffer.Seek(0, SeekOrigin.Begin);

        var audioClient = new AudioClient(_options.TranscriptionModel, credential);
        var filename = Path.GetFileName(storageKey) is { Length: > 0 } fn ? fn : "recording.webm";

        var result = await audioClient.TranscribeAudioAsync(buffer, filename, cancellationToken: ct);
        return result.Value.Text ?? string.Empty;
    }

    private async Task<(EvaluationResult? result, int inputTokens, int outputTokens, decimal cost)>
        EvaluateTranscriptAsync(
            string transcript, SpeakingEvaluationRequest request,
            ApiKeyCredential credential, CancellationToken ct)
    {
        var userPrompt = BuildEvaluationPrompt(transcript, request);
        var chatClient = new ChatClient(_options.Model, credential);
        var messages = new ChatMessage[]
        {
            new SystemChatMessage(EvaluationSystemPrompt),
            new UserChatMessage(userPrompt),
        };
        var opts = new ChatCompletionOptions { MaxOutputTokenCount = 512 };
        var completion = await chatClient.CompleteChatAsync(messages, opts, ct);
        var raw = completion.Value.Content[0].Text;
        var inputTokens = completion.Value.Usage.InputTokenCount;
        var outputTokens = completion.Value.Usage.OutputTokenCount;
        var cost = EstimateCost(_options.Model, inputTokens, outputTokens);
        return (TryParseEvaluation(raw), inputTokens, outputTokens, cost);
    }

    private static string BuildEvaluationPrompt(string transcript, SpeakingEvaluationRequest request)
    {
        var activityLine = string.IsNullOrWhiteSpace(request.ActivityTitle)
            ? "Speaking activity"
            : $"Activity: {request.ActivityTitle}";
        var levelLine = string.IsNullOrWhiteSpace(request.CefrLevel)
            ? "Learner level: not specified"
            : $"Learner CEFR level: {request.CefrLevel}";
        var promptLine = string.IsNullOrWhiteSpace(request.ActivityPrompt)
            ? string.Empty
            : $"\nActivity context (first 500 chars): {(request.ActivityPrompt.Length > 500 ? request.ActivityPrompt[..500] : request.ActivityPrompt)}";

        return $$"""
{{activityLine}}
{{levelLine}}{{promptLine}}

Transcript of learner's spoken response:
"{{transcript}}"

Return ONLY this JSON object (all integer scores 0-100):
{
  "overallScore": <integer>,
  "completenessScore": <integer>,
  "relevanceScore": <integer>,
  "fluencyScore": <integer>,
  "feedbackText": "<1-3 sentences of supportive learner-friendly feedback. Include: 'This feedback is AI-assisted and may be approximate.'>",
  "suggestedImprovement": "<one clear actionable next step>"
}
""";
    }

    private static EvaluationResult? TryParseEvaluation(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var json = raw.Trim();
        if (json.StartsWith("```"))
        {
            var nl = json.IndexOf('\n');
            var last = json.LastIndexOf("```");
            if (nl > 0 && last > nl) json = json[(nl + 1)..last].Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            double? Score(string key) =>
                root.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number
                    ? Math.Clamp(p.GetDouble(), 0, 100) : null;

            string? Str(string key) =>
                root.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String
                    ? p.GetString() : null;

            return new EvaluationResult(
                Score("overallScore"), Score("completenessScore"),
                Score("relevanceScore"), Score("fluencyScore"),
                Str("feedbackText"), Str("suggestedImprovement"));
        }
        catch { return null; }
    }

    private static decimal EstimateCost(string model, int input, int output)
    {
        var (inPer1K, outPer1K) = model.Contains("gpt-4o-mini", StringComparison.OrdinalIgnoreCase)
            ? (0.00015m, 0.00060m)
            : (0.005m, 0.015m);
        return (input / 1000m) * inPer1K + (output / 1000m) * outPer1K;
    }

    private async Task LogUsageAsync(
        string featureKey, string provider, string model,
        bool wasSuccessful, string? failureReason,
        int inputTokens, int outputTokens, decimal cost, long durationMs,
        string correlationId, CancellationToken ct)
    {
        try
        {
            _db.AiUsageLogs.Add(new AiUsageLog(
                null,
                featureKey,
                provider,
                model,
                false,
                wasSuccessful,
                failureReason,
                inputTokens,
                outputTokens,
                cost,
                durationMs,
                correlationId));
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "OpenAiSpeakingEvaluationProvider: usage log failed FeatureKey={Key}", featureKey);
        }
    }

    private static SpeakingEvaluationProviderResult Fail(string reason) =>
        new(false, null, null, null, null, null, null, null, null, reason, null);

    private sealed record EvaluationResult(
        double? OverallScore, double? CompletenessScore, double? RelevanceScore,
        double? FluencyScore, string? FeedbackText, string? SuggestedImprovement);
}
