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
/// Evaluates AI-structured patterns (listen_and_answer, email_reply, teams_chat_simulation)
/// using pattern-specific prompts. Parses and normalises the AI response into PatternEvaluationResult.
/// </summary>
public sealed class AiStructuredEvaluator : IPatternEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly ILogger<AiStructuredEvaluator> _logger;

    public AiStructuredEvaluator(
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        ILogger<AiStructuredEvaluator> logger)
    {
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _logger = logger;
    }

    public MarkingMode MarkingMode => MarkingMode.AiStructured;

    public async Task<PatternEvaluationResult> EvaluateAsync(
        PatternEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var promptKey = ResolvePromptKey(request.ExercisePatternKey);

        var variables = new Dictionary<string, string>
        {
            ["activityContent"] = CompactContent(request.ContentJson),
            ["studentSubmission"] = request.SubmittedAnswerJson,
            ["cefrLevel"] = request.CefrLevel ?? "B1",
            ["careerContext"] = request.DomainComplexity ?? "General",
            ["sourceLanguageName"] = "Persian",
            ["targetLanguageName"] = "English",
            ["exercisePatternKey"] = request.ExercisePatternKey ?? string.Empty,
            ["studentSkillContext"] = request.StudentSkillContext ?? "No specific skill history available yet.",
        };

        _logger.LogInformation(
            "AiStructuredEvaluator calling AI ActivityId={ActivityId} PatternKey={PatternKey} PromptKey={PromptKey}",
            request.ActivityId, request.ExercisePatternKey, promptKey);

        string responseJson;
        try
        {
            var aiRequest = await _contextBuilder.BuildAsync(promptKey, variables, cancellationToken);
            responseJson = await _aiExecution.ExecuteAsync(
                promptKey, aiRequest, request.StudentProfileId, correlationId: null, cancellationToken);
        }
        catch (AiUnavailableException ex)
        {
            _logger.LogWarning(ex,
                "All AI providers failed for AiStructuredEvaluator PatternKey={PatternKey} ActivityId={ActivityId}",
                request.ExercisePatternKey, request.ActivityId);

            return PatternEvaluationResult.Create(
                score: 0, maxScore: 0, passed: false, completed: false,
                coachSummary: "AI evaluation is temporarily unavailable. Please try again later.");
        }

        return ParseAndNormalise(responseJson, request.ExercisePatternKey);
    }

    // ── prompt routing ─────────────────────────────────────────────────────────

    private static string ResolvePromptKey(string? patternKey) => patternKey switch
    {
        ExercisePatternKey.ListenAndAnswer      => "activity_evaluate_listen_and_answer",
        ExercisePatternKey.EmailReply           => "activity_evaluate_email_reply",
        ExercisePatternKey.TeamsChatSimulation  => "activity_evaluate_teams_chat_simulation",
        _                                       => "activity_evaluate_writing", // safe fallback
    };

    // ── response parsing and normalisation ────────────────────────────────────

    internal static PatternEvaluationResult ParseAndNormalise(string responseJson, string? patternKey)
    {
        var cleaned = CleanJson(responseJson);

        AiStructuredPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<AiStructuredPayload>(cleaned, JsonOptions);
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

        // Clamp score to 0–100
        var rawScore = Math.Clamp(payload.OverallScore ?? 60, 0, 100);
        var percentage = rawScore;
        var passed = percentage >= 60;

        // Map AI changes → PatternEvaluationCorrection (cap at 5)
        var corrections = (payload.Changes ?? [])
            .Take(5)
            .Select(c => new PatternEvaluationCorrection(
                Category: c.Category ?? "general",
                Original: c.Original,
                Suggestion: c.Suggested ?? string.Empty,
                Explanation: c.Reason ?? string.Empty))
            .ToList();

        // Map question-level feedback (listen_and_answer) → item results
        var itemResults = BuildItemResults(payload, patternKey);

        return PatternEvaluationResult.Create(
            score: rawScore,
            maxScore: 100,
            passed: passed,
            completed: true,
            itemResults: itemResults,
            coachSummary: payload.CoachSummary,
            corrections: corrections,
            suggestedImprovedAnswer: payload.ImprovedVersion ?? payload.CorrectedText);
    }

    private static IReadOnlyList<PatternEvaluationItemResult> BuildItemResults(
        AiStructuredPayload payload, string? patternKey)
    {
        // listen_and_answer returns question-level feedback
        if (patternKey == ExercisePatternKey.ListenAndAnswer && payload.QuestionFeedback is { Count: > 0 })
        {
            return payload.QuestionFeedback.Select(q => new PatternEvaluationItemResult(
                ItemKey: q.QuestionId ?? string.Empty,
                StudentAnswer: q.StudentAnswer,
                CorrectAnswer: q.ExpectedAnswerSummary,
                AcceptedAnswers: [],
                IsCorrect: q.IsCorrect,
                Score: q.Score,
                MaxScore: 1,
                Feedback: q.Feedback)).ToList();
        }

        return [];
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

    // Truncate activity content to protect token budget — include only fields needed for marking.
    private static string CompactContent(string contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson) || contentJson == "{}") return "{}";
        // Limit to 2000 chars; AI prompts are written to use whatever is provided
        return contentJson.Length > 2000 ? contentJson[..2000] : contentJson;
    }
}

// ── Internal payload DTOs for AI response parsing ─────────────────────────────

internal sealed class AiStructuredPayload
{
    [JsonPropertyName("overallScore")] public double? OverallScore { get; set; }
    [JsonPropertyName("coachSummary")] public string? CoachSummary { get; set; }
    [JsonPropertyName("focusFirst")] public bool? FocusFirst { get; set; }
    [JsonPropertyName("changes")] public List<AiStructuredChangePayload>? Changes { get; set; }
    [JsonPropertyName("improvedVersion")] public string? ImprovedVersion { get; set; }
    [JsonPropertyName("correctedText")] public string? CorrectedText { get; set; }
    [JsonPropertyName("whatYouDidWell")] public List<string>? WhatYouDidWell { get; set; }
    [JsonPropertyName("mainMistakes")] public List<string>? MainMistakes { get; set; }
    [JsonPropertyName("grammarIssues")] public List<string>? GrammarIssues { get; set; }
    [JsonPropertyName("vocabularyIssues")] public List<string>? VocabularyIssues { get; set; }
    [JsonPropertyName("toneIssues")] public List<string>? ToneIssues { get; set; }
    [JsonPropertyName("miniLesson")] public string? MiniLesson { get; set; }
    [JsonPropertyName("questionFeedback")] public List<AiQuestionFeedbackPayload>? QuestionFeedback { get; set; }
}

internal sealed class AiStructuredChangePayload
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("original")] public string? Original { get; set; }
    [JsonPropertyName("suggested")] public string? Suggested { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("severity")] public string? Severity { get; set; }
}

internal sealed class AiQuestionFeedbackPayload
{
    [JsonPropertyName("questionId")] public string? QuestionId { get; set; }
    [JsonPropertyName("question")] public string? Question { get; set; }
    [JsonPropertyName("studentAnswer")] public string? StudentAnswer { get; set; }
    [JsonPropertyName("expectedAnswerSummary")] public string? ExpectedAnswerSummary { get; set; }
    [JsonPropertyName("isCorrect")] public bool IsCorrect { get; set; }
    [JsonPropertyName("score")] public double Score { get; set; }
    [JsonPropertyName("feedback")] public string? Feedback { get; set; }
}
