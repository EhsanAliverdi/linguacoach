using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Writing;

public sealed class WritingExerciseHandler : IGetWritingExerciseHandler, ISubmitWritingDraftHandler
{
    // The single seeded scenario for MVP. Hardened in T9 when LearningPlanner is implemented.
    private const string ScenarioTitle = "Follow-up email for a pending document approval";
    private const string PromptKey = "writing.exercise.v1";

    // Target vocabulary and phrases seeded for Document Controller context.
    private static readonly string[] TargetPhrases =
    [
        "I wanted to follow up on",
        "I would appreciate it if",
        "Please let me know",
        "As previously discussed",
        "I look forward to your response"
    ];

    private static readonly string[] TargetVocabulary =
    [
        "approval", "submittal", "revision", "pending", "outstanding",
        "document controller", "RFI", "transmittal", "compliance"
    ];

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<WritingExerciseHandler> _logger;

    public WritingExerciseHandler(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        IAiProvider aiProvider,
        ILogger<WritingExerciseHandler> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    // ── Get exercise description (no AI call) ─────────────────────────────────

    public async Task<WritingExerciseDto> HandleAsync(GetWritingExerciseQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.OnboardingStatus != Domain.Enums.OnboardingStatus.Complete)
            throw new InvalidOperationException("Writing exercise requires completed onboarding.");

        return new WritingExerciseDto(
            ScenarioTitle,
            "You need to send a professional follow-up email to a project manager who has not yet approved " +
            "a document you submitted 5 working days ago. The document is critical for the next construction phase.",
            "لطفاً یک ایمیل رسمی و مودبانه به مدیر پروژه بنویسید که سند ارسالی شما را هنوز تأیید نکرده است.",
            TargetPhrases,
            TargetVocabulary);
    }

    // ── Submit draft → AI feedback ────────────────────────────────────────────

    public async Task<WritingFeedbackDto> HandleAsync(SubmitWritingDraftCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.DraftText))
            throw new ArgumentException("Draft text is required.", nameof(command));
        if (command.DraftText.Length > 3000)
            throw new ArgumentException("Draft text must be at most 3000 characters.", nameof(command));

        var profile = await _db.StudentProfiles
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.SourceLanguage)
            .Include(p => p.LanguagePair)
                .ThenInclude(lp => lp!.TargetLanguage)
            .Include(p => p.CareerProfile)
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        if (profile.OnboardingStatus != Domain.Enums.OnboardingStatus.Complete)
            throw new InvalidOperationException("Writing exercise requires completed onboarding.");

        // Build the small context packet — backend controls what AI sees.
        var variables = new Dictionary<string, string>
        {
            ["sourceLanguageName"] = profile.LanguagePair?.SourceLanguage?.Name ?? "Persian",
            ["targetLanguageName"] = profile.LanguagePair?.TargetLanguage?.Name ?? "English",
            ["userLevel"] = "A2-B1",  // placeholder until T10 CEFR assessment
            ["careerProfile"] = profile.CareerProfile?.Name ?? "Document Controller",
            ["scenario"] = ScenarioTitle,
            ["targetVocabulary"] = string.Join(", ", TargetVocabulary),
            ["targetPhrases"] = string.Join(", ", TargetPhrases),
            ["userDraft"] = command.DraftText,
        };

        var aiRequest = await _contextBuilder.BuildAsync(PromptKey, variables, ct);
        var aiResponse = await _aiProvider.CompleteAsync(aiRequest, ct);

        // Log usage immediately before any parsing — ensures cost is always tracked.
        var usageLog = new AiUsageLog(
            profile.Id,
            _aiProvider.ProviderName,
            "gpt-4o",
            aiResponse.InputTokens,
            aiResponse.OutputTokens,
            aiResponse.CostUsd);
        _db.AiUsageLogs.Add(usageLog);
        await _db.SaveChangesAsync(ct);

        // Parse and validate the structured JSON response.
        var feedback = ParseFeedback(aiResponse.ResponseJson);

        // Persist the submission.
        var submission = new WritingSubmission(
            profile.Id,
            ScenarioTitle,
            command.DraftText,
            feedback.CorrectedEmail ?? string.Empty,
            aiResponse.ResponseJson,
            feedback.OverallScore,
            PromptKey);
        _db.WritingSubmissions.Add(submission);
        await _db.SaveChangesAsync(ct);

        return new WritingFeedbackDto(
            submission.Id,
            feedback.OverallScore,
            feedback.CorrectedEmail ?? string.Empty,
            feedback.FeedbackInSourceLanguage ?? string.Empty,
            feedback.GrammarIssues ?? [],
            feedback.VocabularyIssues ?? [],
            feedback.ToneIssues ?? [],
            feedback.SuggestedPhrases ?? [],
            feedback.MistakesToTrack ?? []);
    }

    // ── AI response parsing ───────────────────────────────────────────────────

    public static AiFeedbackPayload ParseFeedback(string responseJson)
    {
        // Strip markdown code fences if the model wraps JSON in ```json ... ```
        var cleaned = responseJson.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                cleaned = cleaned[(firstNewline + 1)..lastFence].Trim();
        }

        try
        {
            var result = JsonSerializer.Deserialize<AiFeedbackPayload>(cleaned,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new AiFeedbackPayload();
        }
        catch (JsonException ex)
        {
            // Malformed AI response: return empty feedback rather than crashing.
            // The submission will still be saved with the raw JSON for debugging.
            throw new InvalidOperationException($"AI response was not valid JSON: {ex.Message}", ex);
        }
    }
}

/// <summary>Deserialisation target for the AI feedback JSON response.</summary>
public sealed class AiFeedbackPayload
{
    [JsonPropertyName("overallScore")]
    public double? OverallScore { get; set; }

    [JsonPropertyName("correctedEmail")]
    public string? CorrectedEmail { get; set; }

    [JsonPropertyName("feedbackInSourceLanguage")]
    public string? FeedbackInSourceLanguage { get; set; }

    [JsonPropertyName("grammarIssues")]
    public List<string>? GrammarIssues { get; set; }

    [JsonPropertyName("vocabularyIssues")]
    public List<string>? VocabularyIssues { get; set; }

    [JsonPropertyName("toneIssues")]
    public List<string>? ToneIssues { get; set; }

    [JsonPropertyName("suggestedPhrases")]
    public List<string>? SuggestedPhrases { get; set; }

    [JsonPropertyName("mistakesToTrack")]
    public List<string>? MistakesToTrack { get; set; }
}
