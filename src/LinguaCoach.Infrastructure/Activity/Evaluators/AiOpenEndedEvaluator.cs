using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Ai;
using LinguaCoach.Domain;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity.Evaluators;

/// <summary>
/// Evaluates open-ended spoken response patterns (spoken_response_from_prompt) using AI.
/// Evaluates fake STT text as a spoken-response proxy.
/// Does NOT score pronunciation or accent.
/// </summary>
public sealed class AiOpenEndedEvaluator : IPatternEvaluator
{
    private const string SpokenResponsePromptKey = "activity_evaluate_spoken_response_from_prompt";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<AiOpenEndedEvaluator> _logger;

    public AiOpenEndedEvaluator(
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        ILogger<AiOpenEndedEvaluator> logger)
    {
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public MarkingMode MarkingMode => MarkingMode.AiOpenEnded;

    public async Task<PatternEvaluationResult> EvaluateAsync(
        PatternEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, string>
        {
            ["activityContent"] = CompactContent(request.ContentJson),
            ["studentSubmission"] = request.SubmittedAnswerJson,
            ["cefrLevel"] = request.CefrLevel ?? "B1",
            ["careerContext"] = request.DomainComplexity ?? "General",
            ["sourceLanguageName"] = "Persian",
            ["targetLanguageName"] = "English",
            ["exercisePatternKey"] = request.ExercisePatternKey ?? string.Empty,
        };

        _logger.LogInformation(
            "AiOpenEndedEvaluator calling AI ActivityId={ActivityId} PatternKey={PatternKey}",
            request.ActivityId, request.ExercisePatternKey);

        string responseJson;
        try
        {
            var aiRequest = await _contextBuilder.BuildAsync(SpokenResponsePromptKey, variables, cancellationToken);
            responseJson = await _aiExecution.ExecuteWithFallbackAsync(
                SpokenResponsePromptKey, aiRequest, request.StudentProfileId, correlationId: null, cancellationToken);
        }
        catch (AiUnavailableException ex)
        {
            _logger.LogWarning(ex,
                "All AI providers failed for AiOpenEndedEvaluator PatternKey={PatternKey} ActivityId={ActivityId}",
                request.ExercisePatternKey, request.ActivityId);

            return PatternEvaluationResult.Create(
                score: 0, maxScore: 0, passed: false, completed: false,
                coachSummary: "AI evaluation is temporarily unavailable. Please try again later.");
        }

        return ParseAndNormalise(responseJson);
    }

    // ── response parsing and normalisation ────────────────────────────────────

    internal static PatternEvaluationResult ParseAndNormalise(string responseJson)
    {
        var cleaned = CleanJson(responseJson);

        AiOpenEndedPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<AiOpenEndedPayload>(cleaned, JsonOptions);
        }
        catch (JsonException)
        {
            return PatternEvaluationResult.Create(
                score: 0, maxScore: 0, passed: false, completed: false,
                coachSummary: "AI returned an unexpected response. Please try again later.");
        }

        if (payload is null)
        {
            return PatternEvaluationResult.Create(
                score: 0, maxScore: 0, passed: false, completed: false,
                coachSummary: "AI evaluation could not be parsed. Please try again later.");
        }

        var rawScore = Math.Clamp(payload.OverallScore ?? payload.Score ?? 60, 0, 100);
        var passed = rawScore >= 60;

        // Map speaking-specific feedback as corrections (cap at 5)
        var corrections = BuildCorrections(payload);

        return PatternEvaluationResult.Create(
            score: rawScore,
            maxScore: 100,
            passed: passed,
            completed: true,
            coachSummary: payload.CoachSummary,
            corrections: corrections,
            suggestedImprovedAnswer: payload.SuggestedImprovedResponse);
    }

    private static IReadOnlyList<PatternEvaluationCorrection> BuildCorrections(AiOpenEndedPayload payload)
    {
        var list = new List<PatternEvaluationCorrection>();
        foreach (var improvement in payload.Improvements ?? [])
        {
            if (list.Count >= 5) break;
            list.Add(new PatternEvaluationCorrection("speaking", null, improvement, improvement));
        }
        foreach (var missing in payload.MissingExpectedPoints ?? [])
        {
            if (list.Count >= 5) break;
            list.Add(new PatternEvaluationCorrection("missing_point", null, missing, $"Include: {missing}"));
        }
        return list;
    }

    private static string CleanJson(string raw)
    {
        var s = raw.Trim();
        // Strip markdown code fence before looking for JSON object
        if (s.StartsWith("```"))
        {
            var nl = s.IndexOf('\n');
            var end = s.LastIndexOf("```");
            if (nl > 0 && end > nl) s = s[(nl + 1)..end].Trim();
        }
        if (!s.StartsWith("{"))
        {
            var start = s.IndexOf('{');
            if (start >= 0) s = s[start..];
        }
        return s;
    }

    private static string CompactContent(string contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson) || contentJson == "{}") return "{}";
        return contentJson.Length > 2000 ? contentJson[..2000] : contentJson;
    }
}

// ── Internal payload for spoken response AI response ─────────────────────────

internal sealed class AiOpenEndedPayload
{
    // spoken_response prompt uses "score"; writing prompt uses "overallScore"
    [JsonPropertyName("score")] public double? Score { get; set; }
    [JsonPropertyName("overallScore")] public double? OverallScore { get; set; }
    [JsonPropertyName("coachSummary")] public string? CoachSummary { get; set; }
    [JsonPropertyName("strengths")] public List<string>? Strengths { get; set; }
    [JsonPropertyName("speakingStrengths")] public List<string>? SpeakingStrengths { get; set; }
    [JsonPropertyName("improvements")] public List<string>? Improvements { get; set; }
    [JsonPropertyName("speakingImprovements")] public List<string>? SpeakingImprovements { get; set; }
    [JsonPropertyName("missingExpectedPoints")] public List<string>? MissingExpectedPoints { get; set; }
    [JsonPropertyName("suggestedImprovedResponse")] public string? SuggestedImprovedResponse { get; set; }
    [JsonPropertyName("miniLesson")] public string? MiniLesson { get; set; }
    [JsonPropertyName("nextImprovementStep")] public string? NextImprovementStep { get; set; }
}
