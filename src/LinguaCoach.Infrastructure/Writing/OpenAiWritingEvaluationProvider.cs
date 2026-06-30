using System.ClientModel;
using System.Text.Json;
using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace LinguaCoach.Infrastructure.Writing;

/// <summary>
/// OpenAI-backed writing evaluation provider.
/// Single-step pipeline: GPT rubric evaluation of submitted text — no audio, no transcription.
/// Uses OpenAI:ApiKey / OPENAI_API_KEY from config.
/// All learner-facing feedback includes an AI-assisted disclaimer.
/// Handles malformed/partial responses safely — every score is nullable.
/// </summary>
public sealed class OpenAiWritingEvaluationProvider : IWritingEvaluationProvider
{
    public string ProviderName => "openai";
    public bool IsSupported => _apiKey is not null;
    public WritingEvaluationProviderCapabilities Capabilities => WritingEvaluationProviderCapabilities.OpenAiGpt;

    private readonly string? _apiKey;
    private readonly WritingEvaluationOptions _options;
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<OpenAiWritingEvaluationProvider> _logger;

    private const string EvaluationSystemPrompt =
        "You are an AI English language coach evaluating a learner's written response. " +
        "Rate the response on the dimensions below. Return ONLY valid JSON — no markdown, no explanation. " +
        "This feedback is AI-assisted and approximate. Do not make clinical or official CEFR claims. " +
        "Do not use 'native-like' language. Keep feedback supportive and learner-friendly.";

    public OpenAiWritingEvaluationProvider(
        IConfiguration configuration,
        IOptions<WritingEvaluationOptions> options,
        LinguaCoachDbContext db,
        ILogger<OpenAiWritingEvaluationProvider> logger)
    {
        _options = options.Value;
        _db = db;
        _logger = logger;

        _apiKey = configuration["OpenAI:ApiKey"]
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning(
                "OpenAiWritingEvaluationProvider: OpenAI API key not configured. " +
                "IsSupported=false. Set OpenAI:ApiKey or OPENAI_API_KEY.");
            _apiKey = null;
        }
    }

    public async Task<WritingEvaluationProviderResult> EvaluateAsync(
        WritingEvaluationRequest request,
        CancellationToken ct = default)
    {
        if (!IsSupported)
            return Fail("OpenAI API key is not configured.");

        if (string.IsNullOrWhiteSpace(request.WrittenText))
            return Fail("No written text on this attempt.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        var linked = cts.Token;

        var credential = new ApiKeyCredential(_apiKey!);

        EvaluationResult? evalResult;
        int inputTokens = 0, outputTokens = 0;
        decimal costUsd = 0m;
        long durationMs = 0;
        try
        {
            var started = DateTime.UtcNow;
            (evalResult, inputTokens, outputTokens, costUsd) =
                await EvaluateTextAsync(request, credential, linked);
            durationMs = (long)(DateTime.UtcNow - started).TotalMilliseconds;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fail("Evaluation timed out during rubric scoring.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "OpenAiWritingEvaluationProvider: GPT evaluation failed AttemptId={AttemptId}", request.AttemptId);
            return Fail($"Evaluation scoring failed: {ex.GetType().Name}");
        }

        if (evalResult is null)
        {
            await LogUsageAsync("writing.evaluate", "openai", _options.Model,
                false, "malformed_response", 0, 0, 0m, durationMs, request.CorrelationId, linked);
            return Fail("Provider returned a malformed evaluation response.");
        }

        await LogUsageAsync("writing.evaluate", "openai", _options.Model,
            true, null, inputTokens, outputTokens, costUsd, durationMs, request.CorrelationId, linked);

        _logger.LogInformation(
            "OpenAiWritingEvaluationProvider: complete AttemptId={AttemptId} Score={Score}",
            request.AttemptId, evalResult.OverallScore);

        return new WritingEvaluationProviderResult(
            Success: true,
            OverallScore: evalResult.OverallScore,
            GrammarScore: evalResult.GrammarScore,
            VocabularyScore: evalResult.VocabularyScore,
            CoherenceScore: evalResult.CoherenceScore,
            TaskCompletionScore: evalResult.TaskCompletionScore,
            FeedbackText: evalResult.FeedbackText,
            SuggestedImprovement: evalResult.SuggestedImprovement,
            CorrectedText: evalResult.CorrectedText,
            FailureReason: null,
            ModelName: _options.Model,
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            CostUsd: costUsd);
    }

    private async Task<(EvaluationResult? result, int inputTokens, int outputTokens, decimal cost)>
        EvaluateTextAsync(
            WritingEvaluationRequest request,
            ApiKeyCredential credential, CancellationToken ct)
    {
        var userPrompt = BuildEvaluationPrompt(request);
        var chatClient = new ChatClient(_options.Model, credential);
        var messages = new ChatMessage[]
        {
            new SystemChatMessage(EvaluationSystemPrompt),
            new UserChatMessage(userPrompt),
        };
        var opts = new ChatCompletionOptions { MaxOutputTokenCount = 800 };
        var completion = await chatClient.CompleteChatAsync(messages, opts, ct);
        var raw = completion.Value.Content[0].Text;
        var inputTokens = completion.Value.Usage.InputTokenCount;
        var outputTokens = completion.Value.Usage.OutputTokenCount;
        var cost = EstimateCost(_options.Model, inputTokens, outputTokens);
        return (TryParseEvaluation(raw), inputTokens, outputTokens, cost);
    }

    private static string BuildEvaluationPrompt(WritingEvaluationRequest request)
    {
        var activityLine = string.IsNullOrWhiteSpace(request.ActivityTitle)
            ? "Writing activity"
            : $"Activity: {request.ActivityTitle}";
        var levelLine = string.IsNullOrWhiteSpace(request.CefrLevel)
            ? "Learner level: not specified"
            : $"Learner CEFR level: {request.CefrLevel}";
        var promptLine = string.IsNullOrWhiteSpace(request.ActivityPrompt)
            ? string.Empty
            : $"\nActivity context (first 500 chars): {(request.ActivityPrompt.Length > 500 ? request.ActivityPrompt[..500] : request.ActivityPrompt)}";

        var text = request.WrittenText ?? string.Empty;
        if (text.Length > 3000) text = text[..3000];

        return $$"""
{{activityLine}}
{{levelLine}}{{promptLine}}

Learner's written response:
"{{text}}"

Return ONLY this JSON object (all integer scores 0-100):
{
  "overallScore": <integer>,
  "grammarScore": <integer>,
  "vocabularyScore": <integer>,
  "coherenceScore": <integer>,
  "taskCompletionScore": <integer>,
  "feedbackText": "<1-3 sentences of supportive learner-friendly feedback. Include: 'This feedback is AI-assisted and may be approximate.'>",
  "suggestedImprovement": "<one clear actionable next step>",
  "correctedText": "<an improved version of the learner's response, or empty string if no changes needed>"
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

            var corrected = Str("correctedText");
            if (string.IsNullOrWhiteSpace(corrected)) corrected = null;

            return new EvaluationResult(
                Score("overallScore"), Score("grammarScore"),
                Score("vocabularyScore"), Score("coherenceScore"),
                Score("taskCompletionScore"),
                Str("feedbackText"), Str("suggestedImprovement"), corrected);
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
                "OpenAiWritingEvaluationProvider: usage log failed FeatureKey={Key}", featureKey);
        }
    }

    private static WritingEvaluationProviderResult Fail(string reason) =>
        new(false, null, null, null, null, null, null, null, null, reason, null);

    private sealed record EvaluationResult(
        double? OverallScore, double? GrammarScore, double? VocabularyScore,
        double? CoherenceScore, double? TaskCompletionScore,
        string? FeedbackText, string? SuggestedImprovement, string? CorrectedText);
}
