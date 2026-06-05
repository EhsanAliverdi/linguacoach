using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Memory;
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
    private readonly IStudentMemoryService _memoryService;
    private readonly ILogger<ActivitySubmitHandler> _logger;

    public ActivitySubmitHandler(
        LinguaCoachDbContext db,
        IAiActivityGenerator aiGenerator,
        IStudentMemoryService memoryService,
        ILogger<ActivitySubmitHandler> logger)
    {
        _db = db;
        _aiGenerator = aiGenerator;
        _memoryService = memoryService;
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
        var module = activity.LearningModuleId.HasValue
            ? await _db.LearningModules.FirstOrDefaultAsync(m => m.Id == activity.LearningModuleId.Value, ct)
            : null;

        _logger.LogInformation(
            "Activity attempt submission received ActivityId={ActivityId} UserId={UserId} ContentLength={Length}",
            command.ActivityId, command.UserId, command.SubmittedContent.Length);

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

            _logger.LogInformation("AI evaluation started ActivityId={ActivityId} PromptKey={PromptKey}",
                command.ActivityId, promptKey);
            feedbackJson = await _aiGenerator.EvaluateAttemptAsync(evalContext, ct);
            score = ExtractScore(feedbackJson);
            _logger.LogInformation("AI evaluation succeeded ActivityId={ActivityId} Score={Score}",
                command.ActivityId, score);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AI evaluation failed ActivityId={ActivityId} UserId={UserId} ExceptionType={ExType} — saving attempt with empty feedback",
                command.ActivityId, command.UserId, ex.GetType().Name);
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

        _logger.LogInformation("Attempt saved AttemptId={AttemptId} ActivityId={ActivityId} Score={Score}",
            attempt.Id, activity.Id, score);

        await _memoryService.UpdateMemoryAsync(new ActivityMemoryUpdateRequest(
            profile,
            activity,
            module,
            attempt,
            feedbackJson,
            score,
            CorrelationId: null), ct);

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

        var changes = payload?.Changes?
            .Select(c => new FeedbackChangeDto(
                Type: c.Type ?? "replace",
                Original: c.Original,
                Suggested: c.Suggested,
                Reason: c.Reason,
                Category: c.Category,
                Severity: c.Severity))
            .ToList()
            ?? (IReadOnlyList<FeedbackChangeDto>)[];

        return new ActivityFeedbackDto(
            AttemptId: attemptId,
            Score: score ?? payload?.OverallScore,
            CoachSummary: payload?.CoachSummary,
            FocusFirst: payload?.FocusFirst ?? false,
            Changes: changes,
            // improvedVersion is the primary improved text; fall back to correctedEmail for legacy prompts
            CorrectedText: payload?.ImprovedVersion ?? payload?.CorrectedEmail,
            WhatYouDidWell: payload?.WhatYouDidWell ?? [],
            MainMistakes: payload?.MainMistakes ?? [],
            GrammarIssues: payload?.GrammarIssues ?? [],
            VocabularyIssues: payload?.VocabularyIssues ?? [],
            ToneIssues: payload?.ToneIssues ?? [],
            ClarityIssues: payload?.ClarityIssues ?? [],
            GrammarExplanation: payload?.GrammarExplanation,
            ToneExplanation: payload?.ToneExplanation,
            VocabularyToRemember: payload?.VocabularyToRemember ?? [],
            MiniLesson: payload?.MiniLesson,
            NextImprovementStep: payload?.NextImprovementStep,
            RewriteChallenge: payload?.RewriteChallenge,
            NextPracticeSuggestion: payload?.NextPracticeSuggestion,
            FeedbackInSourceLanguage: payload?.FeedbackInSourceLanguage);
    }
}

internal sealed class ActivityFeedbackChangePayload
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("original")] public string? Original { get; set; }
    [JsonPropertyName("suggested")] public string? Suggested { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("severity")] public string? Severity { get; set; }
}

internal sealed class ActivityFeedbackPayload
{
    [JsonPropertyName("overallScore")] public double? OverallScore { get; set; }
    [JsonPropertyName("coachSummary")] public string? CoachSummary { get; set; }
    [JsonPropertyName("focusFirst")] public bool? FocusFirst { get; set; }
    [JsonPropertyName("changes")] public List<ActivityFeedbackChangePayload>? Changes { get; set; }
    // Legacy field — kept for old prompt responses
    [JsonPropertyName("correctedEmail")] public string? CorrectedEmail { get; set; }
    // New field — preferred improved version label
    [JsonPropertyName("improvedVersion")] public string? ImprovedVersion { get; set; }
    [JsonPropertyName("feedbackInSourceLanguage")] public string? FeedbackInSourceLanguage { get; set; }
    [JsonPropertyName("whatYouDidWell")] public List<string>? WhatYouDidWell { get; set; }
    [JsonPropertyName("mainMistakes")] public List<string>? MainMistakes { get; set; }
    [JsonPropertyName("grammarIssues")] public List<string>? GrammarIssues { get; set; }
    [JsonPropertyName("vocabularyIssues")] public List<string>? VocabularyIssues { get; set; }
    [JsonPropertyName("toneIssues")] public List<string>? ToneIssues { get; set; }
    [JsonPropertyName("clarityIssues")] public List<string>? ClarityIssues { get; set; }
    [JsonPropertyName("grammarExplanation")] public string? GrammarExplanation { get; set; }
    [JsonPropertyName("toneExplanation")] public string? ToneExplanation { get; set; }
    [JsonPropertyName("vocabularyToRemember")] public List<string>? VocabularyToRemember { get; set; }
    [JsonPropertyName("miniLesson")] public string? MiniLesson { get; set; }
    [JsonPropertyName("nextImprovementStep")] public string? NextImprovementStep { get; set; }
    [JsonPropertyName("rewriteChallenge")] public string? RewriteChallenge { get; set; }
    [JsonPropertyName("nextPracticeSuggestion")] public string? NextPracticeSuggestion { get; set; }
}
