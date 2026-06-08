using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Ai;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Evaluates a speaking role-play transcript as workplace English using AI.
/// Does not score pronunciation — evaluates clarity, tone, structure, vocabulary,
/// and workplace appropriateness only.
/// </summary>
public sealed class SpeakingRolePlayEvaluator
{
    public const string EvaluatePromptKey = "activity_evaluate_speaking_roleplay";

    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<SpeakingRolePlayEvaluator> _logger;

    public SpeakingRolePlayEvaluator(
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        ILogger<SpeakingRolePlayEvaluator> logger)
    {
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public async Task<(string FeedbackJson, double Score)> EvaluateAsync(
        string transcript,
        string activityContentJson,
        string cefrLevel,
        string careerContext,
        string sourceLanguageName,
        string targetLanguageName,
        CancellationToken ct)
    {
        var variables = new Dictionary<string, string>
        {
            ["transcript"] = transcript,
            ["activityContent"] = activityContentJson,
            ["cefrLevel"] = cefrLevel,
            ["careerContext"] = careerContext,
            ["sourceLanguageName"] = sourceLanguageName,
            ["targetLanguageName"] = targetLanguageName,
        };

        var aiRequest = await _contextBuilder.BuildAsync(EvaluatePromptKey, variables, ct);

        var response = await _aiExecution.ExecuteWithFallbackAsync(
            EvaluatePromptKey, aiRequest, studentProfileId: null, correlationId: null, ct);

        var cleaned = CleanJson(response);
        var score = ExtractScore(cleaned);

        _logger.LogInformation(
            "SpeakingRolePlay evaluation completed Score={Score}", score);

        return (cleaned, score);
    }

    public static ActivityFeedbackDto ParseFeedback(Guid attemptId, string feedbackJson, double score)
    {
        SpeakingFeedbackPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<SpeakingFeedbackPayload>(feedbackJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { /* safe defaults */ }

        return new ActivityFeedbackDto(
            AttemptId: attemptId,
            Score: score,
            CoachSummary: payload?.CoachSummary,
            FocusFirst: false,
            Changes: [],
            CorrectedText: null,
            WhatYouDidWell: [],
            MainMistakes: [],
            GrammarIssues: [],
            VocabularyIssues: [],
            ToneIssues: [],
            ClarityIssues: [],
            GrammarExplanation: null,
            ToneExplanation: null,
            VocabularyToRemember: [],
            MiniLesson: payload?.MiniLesson,
            NextImprovementStep: payload?.NextImprovementStep,
            RewriteChallenge: null,
            NextPracticeSuggestion: null,
            FeedbackInSourceLanguage: null,
            Transcript: payload?.Transcript,
            SpeakingStrengths: payload?.Strengths ?? [],
            SpeakingImprovements: payload?.Improvements ?? [],
            MissingExpectedPoints: payload?.MissingExpectedPoints ?? [],
            SuggestedImprovedResponse: payload?.SuggestedImprovedResponse);
    }

    private static string CleanJson(string raw)
    {
        var cleaned = raw.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                cleaned = cleaned[(firstNewline + 1)..lastFence].Trim();
        }
        return cleaned;
    }

    private static double ExtractScore(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("score", out var s)
                && s.ValueKind == JsonValueKind.Number
                && s.TryGetDouble(out var val)
                && val is >= 0 and <= 100)
                return val;
        }
        catch { /* ignore */ }
        return 60; // reasonable default if AI omits score
    }
}

internal sealed class SpeakingFeedbackPayload
{
    [JsonPropertyName("score")] public double? Score { get; set; }
    [JsonPropertyName("transcript")] public string? Transcript { get; set; }
    [JsonPropertyName("coachSummary")] public string? CoachSummary { get; set; }
    [JsonPropertyName("strengths")] public List<string>? Strengths { get; set; }
    [JsonPropertyName("improvements")] public List<string>? Improvements { get; set; }
    [JsonPropertyName("missingExpectedPoints")] public List<string>? MissingExpectedPoints { get; set; }
    [JsonPropertyName("suggestedImprovedResponse")] public string? SuggestedImprovedResponse { get; set; }
    [JsonPropertyName("miniLesson")] public string? MiniLesson { get; set; }
    [JsonPropertyName("nextImprovementStep")] public string? NextImprovementStep { get; set; }
}
