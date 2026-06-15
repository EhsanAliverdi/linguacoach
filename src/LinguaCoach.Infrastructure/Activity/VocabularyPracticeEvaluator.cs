using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Evaluates a VocabularyPractice attempt deterministically.
/// No AI call — scoring is based on case-insensitive string matching.
/// Updates vocabulary item StrengthScore and Status after evaluation.
/// </summary>
public sealed class VocabularyPracticeEvaluator
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILogger<VocabularyPracticeEvaluator> _logger;

    public VocabularyPracticeEvaluator(LinguaCoachDbContext db, ILogger<VocabularyPracticeEvaluator> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates submitted vocabulary answers against expected answers from activity content.
    /// Updates StudentVocabularyItem records and builds feedback JSON.
    /// Returns (feedbackJson, score).
    /// </summary>
    public async Task<(string FeedbackJson, double Score)> EvaluateAsync(
        Guid studentProfileId,
        string activityContentJson,
        IReadOnlyList<VocabAnswerDto> submittedAnswers,
        CancellationToken ct = default)
    {
        // Parse expected answers from activity content
        var expectedItems = ParseExpectedItems(activityContentJson);
        if (expectedItems.Count == 0)
        {
            _logger.LogWarning("VocabularyPractice evaluation: no expected items found in content.");
            return (BuildEmptyFeedback(), 0);
        }

        // Load vocabulary items from DB to update them
        var vocabItemIds = expectedItems.Select(i => i.VocabularyItemId).ToList();
        var vocabItems = await _db.StudentVocabularyItems
            .Where(v => v.StudentProfileId == studentProfileId && vocabItemIds.Contains(v.Id))
            .ToListAsync(ct);

        var vocabItemMap = vocabItems.ToDictionary(v => v.Id);

        // Build answer lookup
        var answerMap = submittedAnswers.ToDictionary(a => a.VocabularyItemId, a => a.Answer);

        var itemFeedback = new List<ItemFeedbackRecord>();
        var scores = new List<double>();

        foreach (var expected in expectedItems)
        {
            var studentAnswer = answerMap.TryGetValue(expected.VocabularyItemId, out var ans)
                ? (ans ?? string.Empty).Trim()
                : string.Empty;

            var isCorrect = IsCorrect(studentAnswer, expected.ExpectedAnswer);
            var itemScore = isCorrect ? 100.0 : 0.0;
            scores.Add(itemScore);

            // Update vocabulary item
            if (vocabItemMap.TryGetValue(expected.VocabularyItemId, out var vocabItem))
            {
                vocabItem.RecordPractice(isCorrect);
            }

            itemFeedback.Add(new ItemFeedbackRecord(
                VocabularyItemId: expected.VocabularyItemId,
                Term: expected.Term,
                IsCorrect: isCorrect,
                StudentAnswer: studentAnswer,
                ExpectedAnswer: expected.ExpectedAnswer,
                Explanation: isCorrect
                    ? $"Correct — '{expected.Term}' is a strong professional phrase."
                    : $"The expected phrase was '{expected.ExpectedAnswer}'. {expected.Explanation}"));
        }

        await _db.SaveChangesAsync(ct);

        var overallScore = scores.Count > 0 ? Math.Round(scores.Average(), 0) : 0;
        var correctCount = itemFeedback.Count(i => i.IsCorrect);
        var totalCount = itemFeedback.Count;

        var coachSummary = BuildCoachSummary(correctCount, totalCount);
        var whatYouDidWell = itemFeedback.Where(i => i.IsCorrect).Select(i => $"Correct use of '{i.Term}'").ToList();
        var mainMistakes = itemFeedback.Where(i => !i.IsCorrect)
            .Select(i => $"Missed: '{i.ExpectedAnswer}'").ToList();

        var feedbackJson = JsonSerializer.Serialize(new
        {
            overallScore,
            coachSummary,
            itemFeedback = itemFeedback.Select(i => new
            {
                vocabularyItemId = i.VocabularyItemId,
                term = i.Term,
                isCorrect = i.IsCorrect,
                studentAnswer = i.StudentAnswer,
                expectedAnswer = i.ExpectedAnswer,
                explanation = i.Explanation,
            }),
            miniLesson = "Polite request phrases use modal verbs ('could', 'would') to soften direct commands.",
            nextImprovementStep = correctCount < totalCount
                ? "Review the missed phrases and try again."
                : "Great work — try your next writing activity to practise these phrases in context.",
            whatYouDidWell,
            mainMistakes,
        });

        _logger.LogInformation(
            "VocabularyPractice evaluated StudentProfileId={ProfileId} Score={Score} Correct={Correct}/{Total}",
            studentProfileId, overallScore, correctCount, totalCount);

        return (feedbackJson, overallScore);
    }

    private static bool IsCorrect(string studentAnswer, string expectedAnswer)
    {
        if (string.IsNullOrWhiteSpace(studentAnswer)) return false;
        return string.Equals(
            studentAnswer.Trim().ToLowerInvariant(),
            expectedAnswer.Trim().ToLowerInvariant(),
            StringComparison.Ordinal);
    }

    private static string BuildCoachSummary(int correct, int total)
    {
        if (total == 0) return "No items to evaluate.";
        var pct = correct * 100 / total;
        if (pct == 100) return $"Perfect — you got all {total} right!";
        if (pct >= 80) return $"Great work — {correct} out of {total} correct.";
        if (pct >= 60) return $"Good effort — {correct} out of {total} correct. Keep practising.";
        return $"{correct} out of {total} correct. Review the missed phrases and try again.";
    }

    private static string BuildEmptyFeedback() => JsonSerializer.Serialize(new
    {
        overallScore = 0,
        coachSummary = "No vocabulary items to evaluate.",
        itemFeedback = Array.Empty<object>(),
        miniLesson = (string?)null,
        nextImprovementStep = (string?)null,
        whatYouDidWell = Array.Empty<string>(),
        mainMistakes = Array.Empty<string>(),
    });

    private static IReadOnlyList<ExpectedItem> ParseExpectedItems(string contentJson)
    {
        try
        {
            var content = JsonSerializer.Deserialize<VocabPracticeContent>(contentJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var items = content?.Items ?? content?.PracticeContent?.ExerciseData?.Items;

            return items?
                .Where(i => i.VocabularyItemId != Guid.Empty
                         && (!string.IsNullOrWhiteSpace(i.ExpectedAnswer) || !string.IsNullOrWhiteSpace(i.CorrectAnswer)))
                .Select(i => new ExpectedItem(
                    i.VocabularyItemId,
                    i.Term ?? string.Empty,
                    i.ExpectedAnswer ?? i.CorrectAnswer!,
                    i.Explanation ?? i.Meaning ?? string.Empty))
                .ToList()
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    private sealed record ExpectedItem(Guid VocabularyItemId, string Term, string ExpectedAnswer, string Explanation);
    private sealed record ItemFeedbackRecord(
        Guid VocabularyItemId, string Term, bool IsCorrect,
        string StudentAnswer, string ExpectedAnswer, string Explanation);

    private sealed class VocabPracticeContent
    {
        [JsonPropertyName("items")]
        public List<VocabPracticeItemContent>? Items { get; set; }

        [JsonPropertyName("practiceContent")]
        public VocabPracticeContentPractice? PracticeContent { get; set; }
    }

    private sealed class VocabPracticeContentPractice
    {
        [JsonPropertyName("exerciseData")] public VocabPracticeExerciseData? ExerciseData { get; set; }
    }

    private sealed class VocabPracticeExerciseData
    {
        [JsonPropertyName("items")] public List<VocabPracticeItemContent>? Items { get; set; }
    }

    private sealed class VocabPracticeItemContent
    {
        [JsonPropertyName("vocabularyItemId")] public Guid VocabularyItemId { get; set; }
        [JsonPropertyName("term")] public string? Term { get; set; }
        [JsonPropertyName("expectedAnswer")] public string? ExpectedAnswer { get; set; }
        [JsonPropertyName("correctAnswer")] public string? CorrectAnswer { get; set; }
        [JsonPropertyName("meaning")] public string? Meaning { get; set; }
        [JsonPropertyName("explanation")] public string? Explanation { get; set; }
    }
}
