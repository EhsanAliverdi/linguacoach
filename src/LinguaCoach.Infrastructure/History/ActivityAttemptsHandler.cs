using System.Text.Json;
using LinguaCoach.Application.History;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.History;

public sealed class ActivityAttemptsHandler : IGetActivityAttemptsHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<ActivityAttemptsHandler> _logger;

    public ActivityAttemptsHandler(LinguaCoachDbContext db, ILogger<ActivityAttemptsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ActivityAttemptHistoryDto> HandleAsync(
        GetActivityAttemptsQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        var activity = await _db.LearningActivities
            .FirstOrDefaultAsync(a => a.Id == query.ActivityId, ct)
            ?? throw new KeyNotFoundException($"Activity {query.ActivityId} not found.");

        // Verify student has at least one attempt on this activity
        var attempts = await _db.ActivityAttempts
            .Where(a => a.StudentProfileId == profile.Id
                     && a.LearningActivityId == query.ActivityId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        if (attempts.Count == 0)
            throw new UnauthorizedAccessException("No attempts found for this activity.");

        // Parse the activity content for display
        WritingContent? wc = null;
        if (activity.ActivityType == ActivityType.WritingScenario)
        {
            try
            {
                wc = JsonSerializer.Deserialize<WritingContent>(
                    activity.AiGeneratedContentJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* safe fallback */ }
        }

        var attemptDtos = attempts.Select((a, i) => ParseAttempt(a, i + 1)).ToList();

        _logger.LogInformation(
            "Activity attempt history returned ActivityId={ActivityId} AttemptCount={Count} UserId={UserId}",
            query.ActivityId, attempts.Count, query.UserId);

        return new ActivityAttemptHistoryDto(
            ActivityId: activity.Id,
            Title: activity.Title,
            ActivityType: activity.ActivityType.ToString(),
            Situation: wc?.Situation,
            LearningGoal: wc?.LearningGoal,
            TargetPhrases: wc?.TargetPhrases ?? [],
            Attempts: attemptDtos);
    }

    private static AttemptDetailDto ParseAttempt(Domain.Entities.ActivityAttempt attempt, int number)
    {
        try
        {
            using var doc = JsonDocument.Parse(attempt.FeedbackJson);
            var root = doc.RootElement;

            var changes = ParseChanges(root);
            var whatDidWell = ParseStringArray(root, "whatYouDidWell");
            var grammarIssues = ParseStringArray(root, "grammarIssues");
            var vocabIssues = ParseStringArray(root, "vocabularyIssues");
            var toneIssues = ParseStringArray(root, "toneIssues");
            var clarityIssues = ParseStringArray(root, "clarityIssues");

            return new AttemptDetailDto(
                AttemptId: attempt.Id,
                AttemptNumber: number,
                SubmittedAt: attempt.CreatedAt,
                Score: attempt.Score,
                CoachSummary: GetString(root, "coachSummary"),
                FocusFirst: GetBool(root, "focusFirst"),
                Changes: changes,
                WhatYouDidWell: whatDidWell,
                GrammarIssues: grammarIssues,
                VocabularyIssues: vocabIssues,
                ToneIssues: toneIssues,
                ClarityIssues: clarityIssues,
                MiniLesson: GetString(root, "miniLesson"),
                NextImprovementStep: GetString(root, "nextImprovementStep"),
                SuggestedImprovedVersion: GetString(root, "improvedVersion") ?? GetString(root, "correctedEmail"),
                NativeLanguageExplanation: GetString(root, "feedbackInSourceLanguage"),
                SubmittedContent: attempt.SubmittedContent);
        }
        catch
        {
            return new AttemptDetailDto(
                AttemptId: attempt.Id,
                AttemptNumber: number,
                SubmittedAt: attempt.CreatedAt,
                Score: attempt.Score,
                CoachSummary: null, FocusFirst: false,
                Changes: [], WhatYouDidWell: [],
                GrammarIssues: [], VocabularyIssues: [],
                ToneIssues: [], ClarityIssues: [],
                MiniLesson: null, NextImprovementStep: null,
                SuggestedImprovedVersion: null,
                NativeLanguageExplanation: null,
                SubmittedContent: attempt.SubmittedContent);
        }
    }

    private static IReadOnlyList<AttemptChangeDto> ParseChanges(JsonElement root)
    {
        if (!root.TryGetProperty("changes", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        return arr.EnumerateArray().Select(c => new AttemptChangeDto(
            Type: GetString(c, "type") ?? "replace",
            Original: GetString(c, "original"),
            Suggested: GetString(c, "suggested"),
            Reason: GetString(c, "reason"),
            Category: GetString(c, "category"),
            Severity: GetString(c, "severity"))).ToList();
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];
        return arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? string.Empty)
            .Where(s => s.Length > 0)
            .ToList();
    }

    private static string? GetString(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString();
        return null;
    }

    private static bool GetBool(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var v))
        {
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
        }
        return false;
    }

    private sealed class WritingContent
    {
        public string? Situation { get; set; }
        public string? LearningGoal { get; set; }
        public string[]? TargetPhrases { get; set; }
    }
}
