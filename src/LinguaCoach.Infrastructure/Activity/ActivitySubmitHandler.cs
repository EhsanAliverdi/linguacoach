using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

public sealed class ActivitySubmitHandler : ISubmitActivityAttemptHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly IAiActivityGenerator _aiGenerator;
    private readonly ILogger<ActivitySubmitHandler> _logger;

    public ActivitySubmitHandler(
        LinguaCoachDbContext db,
        IAiActivityGenerator aiGenerator,
        ILogger<ActivitySubmitHandler> logger)
    {
        _db = db;
        _aiGenerator = aiGenerator;
        _logger = logger;
    }

    public async Task<ActivityFeedbackDto> HandleAsync(
        SubmitActivityAttemptCommand command,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.SubmittedContent))
            throw new ArgumentException("SubmittedContent is required.", nameof(command));
        if (command.SubmittedContent.Length > 3000)
            throw new ArgumentException("SubmittedContent must be at most 3000 characters.", nameof(command));

        var profile = await _db.StudentProfiles
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.SourceLanguage)
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.TargetLanguage)
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.OnboardingStatus != Domain.Enums.OnboardingStatus.Complete)
            throw new InvalidOperationException("Activity attempt requires completed onboarding.");

        var activity = await _db.LearningActivities
            .FirstOrDefaultAsync(a => a.Id == command.ActivityId && a.IsActive, ct)
            ?? throw new InvalidOperationException("Activity not found.");

        // Evaluate with AI.
        string feedbackJson;
        double? score = null;
        var promptKey = GetPromptKey(activity.ActivityType);

        try
        {
            var evalContext = new ActivityEvaluationContext(
                ActivityType: activity.ActivityType,
                ActivityContentJson: activity.AiGeneratedContentJson,
                StudentSubmission: command.SubmittedContent,
                CefrLevel: profile.CefrLevel ?? "B1",
                CareerContext: profile.CareerProfile?.Name ?? "General",
                SourceLanguageName: profile.LanguagePair?.SourceLanguage?.Name ?? "Persian",
                TargetLanguageName: profile.LanguagePair?.TargetLanguage?.Name ?? "English");

            feedbackJson = await _aiGenerator.EvaluateAttemptAsync(evalContext, ct);
            score = ExtractScore(feedbackJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AI evaluation failed for activity {ActivityId}, user {UserId}. Saving attempt with empty feedback.",
                command.ActivityId, command.UserId);
            feedbackJson = "{}";
        }

        var attempt = new ActivityAttempt(
            studentProfileId: profile.Id,
            learningActivityId: activity.Id,
            submittedContent: command.SubmittedContent,
            feedbackJson: feedbackJson,
            promptKey: promptKey,
            score: score,
            audioUrl: command.AudioUrl);

        _db.ActivityAttempts.Add(attempt);
        await _db.SaveChangesAsync(ct);

        return ParseFeedback(attempt.Id, feedbackJson, score);
    }

    private static string GetPromptKey(ActivityType type) => type switch
    {
        ActivityType.WritingScenario => "activity_evaluate_writing",
        _ => $"activity_evaluate_{type.ToString().ToLowerInvariant()}"
    };

    private static double? ExtractScore(string feedbackJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(feedbackJson);
            if (doc.RootElement.TryGetProperty("overallScore", out var s)
                && s.ValueKind == JsonValueKind.Number
                && s.TryGetDouble(out var val)
                && val is >= 0 and <= 100)
                return val;
        }
        catch { /* ignore */ }
        return null;
    }

    private static ActivityFeedbackDto ParseFeedback(Guid attemptId, string feedbackJson, double? score)
    {
        ActivityFeedbackPayload? payload = null;
        try
        {
            payload = JsonSerializer.Deserialize<ActivityFeedbackPayload>(
                feedbackJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { /* return safe defaults */ }

        return new ActivityFeedbackDto(
            AttemptId: attemptId,
            Score: score ?? payload?.OverallScore,
            CorrectedText: payload?.CorrectedEmail,
            WhatYouDidWell: payload?.WhatYouDidWell ?? [],
            MainMistakes: payload?.MainMistakes ?? [],
            GrammarExplanation: payload?.GrammarExplanation,
            ToneExplanation: payload?.ToneExplanation,
            VocabularyToRemember: payload?.VocabularyToRemember ?? [],
            RewriteChallenge: payload?.RewriteChallenge,
            NextPracticeSuggestion: payload?.NextPracticeSuggestion,
            FeedbackInSourceLanguage: payload?.FeedbackInSourceLanguage);
    }
}

internal sealed class ActivityFeedbackPayload
{
    [JsonPropertyName("overallScore")] public double? OverallScore { get; set; }
    [JsonPropertyName("correctedEmail")] public string? CorrectedEmail { get; set; }
    [JsonPropertyName("feedbackInSourceLanguage")] public string? FeedbackInSourceLanguage { get; set; }
    [JsonPropertyName("whatYouDidWell")] public List<string>? WhatYouDidWell { get; set; }
    [JsonPropertyName("mainMistakes")] public List<string>? MainMistakes { get; set; }
    [JsonPropertyName("grammarExplanation")] public string? GrammarExplanation { get; set; }
    [JsonPropertyName("toneExplanation")] public string? ToneExplanation { get; set; }
    [JsonPropertyName("vocabularyToRemember")] public List<string>? VocabularyToRemember { get; set; }
    [JsonPropertyName("rewriteChallenge")] public string? RewriteChallenge { get; set; }
    [JsonPropertyName("nextPracticeSuggestion")] public string? NextPracticeSuggestion { get; set; }
}
