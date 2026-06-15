using System.Text.Json;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity.Evaluators;

/// <summary>
/// Evaluates matching-pair activities deterministically by comparing submitted
/// pair selections against the expected correct map. No AI call is made.
/// </summary>
public sealed class KeyedSelectionEvaluator : IPatternEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MarkingMode MarkingMode => MarkingMode.KeyedSelection;

    public Task<PatternEvaluationResult> EvaluateAsync(
        PatternEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ExercisePatternKey == "reading_multiple_choice_single")
            return EvaluateReadingMultipleChoiceSingleAsync(request);

        var expectedMap = ParseExpectedPairs(request.ContentJson);
        var submittedMap = ParseSubmittedPairs(request.SubmittedAnswerJson);

        var itemResults = new List<PatternEvaluationItemResult>();
        double totalScore = 0;
        double totalMax = expectedMap.Count;

        // Track submitted keys to detect unknown keys
        var usedSubmittedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (phraseKey, expectedMeaningKey) in expectedMap)
        {
            submittedMap.TryGetValue(phraseKey, out var studentAnswer);
            usedSubmittedKeys.Add(phraseKey);

            var isCorrect = string.Equals(studentAnswer, expectedMeaningKey, StringComparison.Ordinal);
            var score = isCorrect ? 1.0 : 0.0;
            totalScore += score;

            string feedback;
            if (studentAnswer is null)
                feedback = $"Missing — expected \"{expectedMeaningKey}\".";
            else if (isCorrect)
                feedback = "Correct match.";
            else
                feedback = $"Incorrect — expected \"{expectedMeaningKey}\", you selected \"{studentAnswer}\".";

            itemResults.Add(new PatternEvaluationItemResult(
                ItemKey: phraseKey,
                StudentAnswer: studentAnswer,
                CorrectAnswer: expectedMeaningKey,
                AcceptedAnswers: [expectedMeaningKey],
                IsCorrect: isCorrect,
                Score: score,
                MaxScore: 1,
                Feedback: feedback));
        }

        // Unknown keys — submitted pairs not in expectedMap
        foreach (var (submittedKey, submittedValue) in submittedMap)
        {
            if (usedSubmittedKeys.Contains(submittedKey)) continue;

            itemResults.Add(new PatternEvaluationItemResult(
                ItemKey: submittedKey,
                StudentAnswer: submittedValue,
                CorrectAnswer: null,
                AcceptedAnswers: [],
                IsCorrect: false,
                Score: 0,
                MaxScore: 0,
                Feedback: $"Unexpected key \"{submittedKey}\" — not part of this exercise."));
        }

        var percentage = PatternEvaluationResult.CalculatePercentage(totalScore, totalMax);
        var passed = totalMax == 0 || percentage >= 60;
        var coachSummary = BuildCoachSummary(itemResults, (int)totalScore, (int)totalMax);

        var result = PatternEvaluationResult.Create(
            score: totalScore,
            maxScore: totalMax,
            passed: passed,
            completed: true,
            itemResults: itemResults,
            coachSummary: coachSummary);

        return Task.FromResult(result);
    }

    // ── reading_multiple_choice_single ───────────────────────────────────────────

    private static Task<PatternEvaluationResult> EvaluateReadingMultipleChoiceSingleAsync(
        PatternEvaluationRequest request)
    {
        var exerciseData = ParseReadingExerciseData(request.ContentJson);
        var selectedOptionId = ParseSelectedOptionId(request.SubmittedAnswerJson);

        var correctOptionId = exerciseData?.CorrectOptionId;
        var isCorrect = selectedOptionId is not null
            && correctOptionId is not null
            && string.Equals(selectedOptionId, correctOptionId, StringComparison.Ordinal);

        var score = isCorrect ? 1.0 : 0.0;
        const double maxScore = 1.0;

        string feedback;
        if (selectedOptionId is null)
            feedback = $"No answer selected — the correct answer is \"{correctOptionId}\".";
        else if (isCorrect)
            feedback = exerciseData?.Explanation ?? "Correct.";
        else
        {
            var distractorNote = exerciseData?.DistractorExplanations is not null
                && exerciseData.DistractorExplanations.TryGetValue(selectedOptionId, out var note)
                && !string.IsNullOrWhiteSpace(note)
                ? note
                : null;

            feedback = distractorNote is not null
                ? $"Incorrect — the correct answer is \"{correctOptionId}\". {distractorNote}"
                : $"Incorrect — the correct answer is \"{correctOptionId}\".";

            if (!string.IsNullOrWhiteSpace(exerciseData?.Explanation))
                feedback += $" {exerciseData.Explanation}";
        }

        var itemResult = new PatternEvaluationItemResult(
            ItemKey: "reading_multiple_choice_single",
            StudentAnswer: selectedOptionId,
            CorrectAnswer: correctOptionId,
            AcceptedAnswers: correctOptionId is null ? [] : [correctOptionId],
            IsCorrect: isCorrect,
            Score: score,
            MaxScore: maxScore,
            Feedback: feedback);

        var coachSummary = isCorrect
            ? "Correct — you selected the best-supported answer."
            : "Not quite — review the passage and the explanation to see why another option fits best.";

        var result = PatternEvaluationResult.Create(
            score: score,
            maxScore: maxScore,
            passed: isCorrect,
            completed: true,
            itemResults: [itemResult],
            coachSummary: coachSummary);

        return Task.FromResult(result);
    }

    private static ReadingMultipleChoiceExerciseData? ParseReadingExerciseData(string contentJson)
    {
        try
        {
            var json = UnwrapStagedContent(contentJson);
            return JsonSerializer.Deserialize<ReadingMultipleChoiceExerciseData>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ParseSelectedOptionId(string submittedAnswerJson)
    {
        if (string.IsNullOrWhiteSpace(submittedAnswerJson)) return null;

        try
        {
            var dto = JsonSerializer.Deserialize<ReadingMultipleChoiceSubmittedAnswer>(submittedAnswerJson, JsonOptions);
            return string.IsNullOrWhiteSpace(dto?.SelectedOptionId) ? null : dto.SelectedOptionId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ── parsing ────────────────────────────────────────────────────────────────

    /// <summary>
    /// If the content JSON is module_stage_v1, extracts practiceContent.exerciseData for evaluation.
    /// Falls back to the original JSON for legacy flat activities.
    /// </summary>
    private static string UnwrapStagedContent(string contentJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(contentJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("schemaVersion", out var sv)
                && sv.GetString() == "module_stage_v1"
                && root.TryGetProperty("practiceContent", out var pc)
                && pc.ValueKind == JsonValueKind.Object
                && pc.TryGetProperty("exerciseData", out var ed)
                && ed.ValueKind == JsonValueKind.Object)
            {
                return ed.GetRawText();
            }
        }
        catch { /* fall through */ }
        return contentJson;
    }

    private static Dictionary<string, string> ParseExpectedPairs(string contentJson)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            var json = UnwrapStagedContent(contentJson);
            var content = JsonSerializer.Deserialize<PhraseMatchContent>(json, JsonOptions);
            if (content?.Pairs is null) return result;

            for (var i = 0; i < content.Pairs.Count; i++)
            {
                var pair = content.Pairs[i];
                // Use a stable index-based key so phrase display text changes don't break evaluation
                var phraseKey = $"phrase_{i}";
                var meaningKey = $"meaning_{i}";
                result[phraseKey] = meaningKey;
            }
        }
        catch (JsonException) { /* return empty */ }

        return result;
    }

    private static Dictionary<string, string?> ParseSubmittedPairs(string submittedAnswerJson)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(submittedAnswerJson)) return result;

        try
        {
            var dto = JsonSerializer.Deserialize<PhraseMatchSubmittedAnswer>(submittedAnswerJson, JsonOptions);
            if (dto?.Pairs is not null) return dto.Pairs;
        }
        catch (JsonException) { /* return empty */ }

        return result;
    }

    // ── coach summary ──────────────────────────────────────────────────────────

    private static string BuildCoachSummary(List<PatternEvaluationItemResult> items, int correct, int total)
    {
        if (total == 0) return "Activity completed.";
        if (correct == total) return "Excellent — all phrase matches are correct!";
        if (correct == 0) return "Review the phrase meanings and try again.";
        return $"You matched {correct} of {total} phrases correctly. Check the highlighted pairs to improve.";
    }
}

/// <summary>
/// Submitted answer shape for phrase_match (MatchingPairs) activities.
/// Keys are stable phrase keys (e.g. "phrase_0"); values are meaning keys (e.g. "meaning_2").
/// </summary>
public sealed class PhraseMatchSubmittedAnswer
{
    public Dictionary<string, string?> Pairs { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// practiceContent.exerciseData shape for reading_multiple_choice_single.
/// </summary>
public sealed class ReadingMultipleChoiceExerciseData
{
    public string? CorrectOptionId { get; set; }
    public string? Explanation { get; set; }
    public Dictionary<string, string>? DistractorExplanations { get; set; }
}

/// <summary>
/// Submitted answer shape for reading_multiple_choice_single.
/// </summary>
public sealed class ReadingMultipleChoiceSubmittedAnswer
{
    public string? SelectedOptionId { get; set; }
}
