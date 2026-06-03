using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.LearningPlan;
using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Writing;

public sealed class WritingExerciseSubmitHandler : ISubmitWritingDraftHandler
{
    private const string PromptKey = "writing.exercise.v2";

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly IAiProviderResolver _aiProviderResolver;
    private readonly ILearningPlanner _learningPlanner;
    private readonly ILogger<WritingExerciseSubmitHandler> _logger;

    public WritingExerciseSubmitHandler(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        IAiProviderResolver aiProviderResolver,
        ILearningPlanner learningPlanner,
        ILogger<WritingExerciseSubmitHandler> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiProviderResolver = aiProviderResolver;
        _learningPlanner = learningPlanner;
        _logger = logger;
    }

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

        // Load the scenario if one was specified.
        WritingScenario? scenario = null;
        if (command.ScenarioId.HasValue)
        {
            scenario = await _db.WritingScenarios
                .FirstOrDefaultAsync(s => s.Id == command.ScenarioId.Value, ct);
        }

        var scenarioTitle = scenario?.Title ?? "General writing practice";
        var scenarioSituation = scenario?.Situation ?? string.Empty;
        var scenarioTargetPhrases = scenario != null
            ? DeserializeStringArray(scenario.TargetPhrasesJson)
            : Array.Empty<string>();

        var plan = await _learningPlanner.BuildLessonPlanAsync(profile.Id, ct);

        var allVocabWords = plan.TargetVocabulary
            .Concat(plan.ReviewVocabulary)
            .Concat(plan.ReinforcementVocabulary)
            .Select(v => v.Word)
            .Distinct()
            .ToList();

        var variables = new Dictionary<string, string>
        {
            ["sourceLanguageName"] = profile.LanguagePair?.SourceLanguage?.Name ?? "Persian",
            ["targetLanguageName"] = profile.LanguagePair?.TargetLanguage?.Name ?? "English",
            ["userLevel"] = plan.CefrLevel,
            ["careerProfile"] = plan.CareerContext,
            ["scenario"] = scenarioTitle,
            ["scenarioSituation"] = scenarioSituation,
            ["targetVocabulary"] = string.Join(", ", allVocabWords),
            ["targetPhrases"] = string.Join(", ", scenarioTargetPhrases),
            ["userDraft"] = command.DraftText,
        };

        var selection = _aiProviderResolver.ResolveWritingFeedbackProvider();
        var aiRequest = await _contextBuilder.BuildAsync(PromptKey, variables, ct);
        aiRequest = aiRequest with { ModelHint = selection.ModelName, ApiKeyOverride = selection.ApiKeyOverride };
        var aiResponse = await selection.Provider.CompleteAsync(aiRequest, ct);

        // Log usage immediately before any parsing.
        var modelName = string.IsNullOrEmpty(aiResponse.ModelName) ? "unknown" : aiResponse.ModelName;
        var usageLog = new AiUsageLog(
            profile.Id,
            string.IsNullOrWhiteSpace(aiResponse.ProviderName) ? selection.ProviderName : aiResponse.ProviderName,
            modelName,
            aiResponse.InputTokens,
            aiResponse.OutputTokens,
            aiResponse.CostUsd);
        _db.AiUsageLogs.Add(usageLog);
        await _db.SaveChangesAsync(ct);

        var feedback = ParseFeedback(aiResponse.ResponseJson);

        var submission = new WritingSubmission(
            profile.Id,
            scenarioTitle,
            command.DraftText,
            feedback.CorrectedEmail ?? string.Empty,
            aiResponse.ResponseJson,
            feedback.OverallScore,
            PromptKey,
            scenarioId: command.ScenarioId);
        _db.WritingSubmissions.Add(submission);
        await _db.SaveChangesAsync(ct);

        var allVocabItems = plan.TargetVocabulary
            .Concat(plan.ReviewVocabulary)
            .Concat(plan.ReinforcementVocabulary)
            .GroupBy(v => v.Word, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (profile.LanguagePairId.HasValue)
            await StageVocabularyUpdatesAsync(profile.Id, profile.LanguagePairId.Value, command.DraftText, allVocabItems, ct);
        await StageLessonVocabularyLogsAsync(profile.Id, allVocabWords, ct);
        await StageLearningSummaryUpdateAsync(profile.Id, scenarioTitle, feedback, ct);
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
            feedback.MistakesToTrack ?? [],
            feedback.WhatYouDidWell ?? [],
            feedback.MainMistakes ?? [],
            feedback.GrammarExplanation ?? string.Empty,
            feedback.ToneExplanation ?? string.Empty,
            feedback.VocabularyToRemember ?? [],
            feedback.RewriteChallenge ?? string.Empty,
            feedback.NextPracticeSuggestion ?? string.Empty);
    }

    // ── Post-submission side-effects ──────────────────────────────────────────

    private async Task StageVocabularyUpdatesAsync(
        Guid studentProfileId, Guid languagePairId, string draftText,
        IReadOnlyList<VocabItem> lessonVocabulary, CancellationToken ct)
    {
        var draftLower = draftText.ToLowerInvariant();
        var words = lessonVocabulary.Select(v => v.Word).ToList();

        var existingEntries = await _db.VocabularyEntries
            .Where(v => v.StudentProfileId == studentProfileId && words.Contains(v.Word))
            .ToListAsync(ct);

        var existingByWord = existingEntries
            .ToDictionary(v => v.Word.ToLowerInvariant(), v => v);

        foreach (var item in lessonVocabulary)
        {
            var wordLower = item.Word.ToLowerInvariant();
            var usedInDraft = Regex.IsMatch(draftLower, @"\b" + Regex.Escape(wordLower) + @"\b");

            if (existingByWord.TryGetValue(wordLower, out var entry))
            {
                if (usedInDraft)
                    entry.RecordUsage(correct: true);
                else
                    entry.RecordExposure();
            }
            else
            {
                var definition = string.IsNullOrEmpty(item.Definition) ? item.Word : item.Definition;
                var newEntry = new VocabularyEntry(studentProfileId, languagePairId, word: item.Word, definition: definition);
                if (usedInDraft)
                    newEntry.RecordUsage(correct: true);
                else
                    newEntry.RecordExposure();
                _db.VocabularyEntries.Add(newEntry);
            }
        }
    }

    private async Task StageLessonVocabularyLogsAsync(
        Guid studentProfileId, IReadOnlyList<string> lessonVocabulary, CancellationToken ct)
    {
        var lastLesson = await _db.LessonVocabularyLogs
            .Where(l => l.StudentProfileId == studentProfileId)
            .MaxAsync(l => (int?)l.LessonNumber, ct) ?? 0;
        var lessonNumber = lastLesson + 1;

        var presentedEntries = await _db.VocabularyEntries
            .Where(v => v.StudentProfileId == studentProfileId && lessonVocabulary.Contains(v.Word))
            .Select(v => v.Id)
            .ToListAsync(ct);

        foreach (var entryId in presentedEntries)
            _db.LessonVocabularyLogs.Add(new LessonVocabularyLog(studentProfileId, entryId, lessonNumber));
    }

    private async Task StageLearningSummaryUpdateAsync(
        Guid studentProfileId, string scenarioTitle, AiFeedbackPayload feedback, CancellationToken ct)
    {
        var summary = await _db.UserLearningSummaries
            .FirstOrDefaultAsync(s => s.StudentProfileId == studentProfileId, ct);

        if (summary is null)
        {
            summary = new UserLearningSummary(studentProfileId);
            _db.UserLearningSummaries.Add(summary);
        }

        var score = feedback.OverallScore.HasValue ? $" Score: {feedback.OverallScore:F0}/100." : string.Empty;
        var weaknesses = feedback.GrammarIssues?.Count > 0 || feedback.VocabularyIssues?.Count > 0
            ? $"Grammar issues: {feedback.GrammarIssues?.Count ?? 0}. Vocab issues: {feedback.VocabularyIssues?.Count ?? 0}."
            : string.Empty;

        var progress = $"Completed writing exercise: {scenarioTitle}.{score}";
        if (progress.Length > UserLearningSummary.MaxSummaryLength)
            progress = progress[..UserLearningSummary.MaxSummaryLength];
        if (weaknesses.Length > UserLearningSummary.MaxSummaryLength)
            weaknesses = weaknesses[..UserLearningSummary.MaxSummaryLength];

        summary.Update(weaknesses, progress);
    }

    // ── AI response parsing ───────────────────────────────────────────────────

    public static AiFeedbackPayload ParseFeedback(string responseJson)
    {
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
            throw new AiResponseValidationException($"AI response was not valid JSON: {ex.Message}", ex);
        }
    }

    private static string[] DeserializeStringArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch { return []; }
    }
}

/// <summary>Deserialisation target for the AI feedback JSON response (v2).</summary>
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

    // Teaching fields (v2)
    [JsonPropertyName("whatYouDidWell")]
    public List<string>? WhatYouDidWell { get; set; }

    [JsonPropertyName("mainMistakes")]
    public List<string>? MainMistakes { get; set; }

    [JsonPropertyName("grammarExplanation")]
    public string? GrammarExplanation { get; set; }

    [JsonPropertyName("toneExplanation")]
    public string? ToneExplanation { get; set; }

    [JsonPropertyName("vocabularyToRemember")]
    public List<string>? VocabularyToRemember { get; set; }

    [JsonPropertyName("rewriteChallenge")]
    public string? RewriteChallenge { get; set; }

    [JsonPropertyName("nextPracticeSuggestion")]
    public string? NextPracticeSuggestion { get; set; }
}
