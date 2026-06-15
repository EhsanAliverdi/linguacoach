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
        if (request.ExercisePatternKey == "reading_multiple_choice_single"
            || request.ExercisePatternKey == "listening_multiple_choice_single"
            || request.ExercisePatternKey == "select_missing_word")
            return EvaluateReadingMultipleChoiceSingleAsync(request);

        if (request.ExercisePatternKey == "reading_multiple_choice_multi"
            || request.ExercisePatternKey == "listening_multiple_choice_multi")
            return EvaluateReadingMultipleChoiceMultiAsync(request);

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
            ItemKey: request.ExercisePatternKey ?? "reading_multiple_choice_single",
            StudentAnswer: selectedOptionId,
            CorrectAnswer: correctOptionId,
            AcceptedAnswers: correctOptionId is null ? [] : [correctOptionId],
            IsCorrect: isCorrect,
            Score: score,
            MaxScore: maxScore,
            Feedback: feedback);

        var sourceNoun = request.ExercisePatternKey is "listening_multiple_choice_single" or "select_missing_word" ? "audio" : "passage";
        var coachSummary = isCorrect
            ? "Correct — you selected the best-supported answer."
            : $"Not quite — review the {sourceNoun} and the explanation to see why another option fits best.";

        var result = PatternEvaluationResult.Create(
            score: score,
            maxScore: maxScore,
            passed: isCorrect,
            completed: true,
            itemResults: [itemResult],
            coachSummary: coachSummary);

        return Task.FromResult(result);
    }

    // ── reading_multiple_choice_multi ────────────────────────────────────────

    private static Task<PatternEvaluationResult> EvaluateReadingMultipleChoiceMultiAsync(
        PatternEvaluationRequest request)
    {
        var exerciseData = ParseReadingMultiExerciseData(request.ContentJson);
        var selectedIds = ParseSelectedOptionIds(request.SubmittedAnswerJson);

        var correctIds = exerciseData?.CorrectOptionIds is { Count: > 0 }
            ? new HashSet<string>(exerciseData.CorrectOptionIds, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        var selectedSet = new HashSet<string>(selectedIds ?? [], StringComparer.Ordinal);

        var missed = correctIds.Where(id => !selectedSet.Contains(id)).ToList();
        var falsePositives = selectedSet.Where(id => !correctIds.Contains(id)).ToList();
        var isCorrect = missed.Count == 0 && falsePositives.Count == 0 && correctIds.Count > 0;

        var score = isCorrect ? 1.0 : 0.0;
        const double maxScore = 1.0;

        string feedback;
        if (selectedSet.Count == 0)
        {
            feedback = $"No answers selected — the correct answers are: {string.Join(", ", correctIds.OrderBy(x => x))}.";
        }
        else if (isCorrect)
        {
            feedback = exerciseData?.Explanation ?? "Correct — you selected all supported answers.";
        }
        else
        {
            var parts = new List<string>();
            if (missed.Count > 0)
                parts.Add($"You missed: {string.Join(", ", missed.OrderBy(x => x))}.");
            if (falsePositives.Count > 0)
                parts.Add($"Incorrectly selected: {string.Join(", ", falsePositives.OrderBy(x => x))}.");
            feedback = string.Join(" ", parts);
            if (!string.IsNullOrWhiteSpace(exerciseData?.Explanation))
                feedback += $" {exerciseData.Explanation}";
        }

        var optionDetails = new List<string>();
        if (exerciseData?.OptionExplanations is not null)
        {
            foreach (var (optId, note) in exerciseData.OptionExplanations.OrderBy(kvp => kvp.Key))
            {
                if (!string.IsNullOrWhiteSpace(note))
                    optionDetails.Add($"{optId}: {note}");
            }
        }
        if (optionDetails.Count > 0)
            feedback += " " + string.Join(" | ", optionDetails);

        var itemResult = new PatternEvaluationItemResult(
            ItemKey: request.ExercisePatternKey ?? "reading_multiple_choice_multi",
            StudentAnswer: selectedSet.Count > 0 ? string.Join(",", selectedSet.OrderBy(x => x)) : null,
            CorrectAnswer: string.Join(",", correctIds.OrderBy(x => x)),
            AcceptedAnswers: [string.Join(",", correctIds.OrderBy(x => x))],
            IsCorrect: isCorrect,
            Score: score,
            MaxScore: maxScore,
            Feedback: feedback);

        var sourceNoun = request.ExercisePatternKey == "listening_multiple_choice_multi" ? "audio" : "passage";
        var coachSummary = isCorrect
            ? $"Correct — you selected all answers supported by the {sourceNoun}."
            : missed.Count > 0 && falsePositives.Count == 0
                ? $"You found some correct answers but missed others — review the {sourceNoun} for all supported options."
                : $"Not quite — check which options are directly supported by the {sourceNoun} and avoid unsupported ones.";

        var result = PatternEvaluationResult.Create(
            score: score,
            maxScore: maxScore,
            passed: isCorrect,
            completed: true,
            itemResults: [itemResult],
            coachSummary: coachSummary);

        return Task.FromResult(result);
    }

    private static ReadingMultipleChoiceMultiExerciseData? ParseReadingMultiExerciseData(string contentJson)
    {
        try
        {
            var json = UnwrapStagedContent(contentJson);
            return JsonSerializer.Deserialize<ReadingMultipleChoiceMultiExerciseData>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<string>? ParseSelectedOptionIds(string submittedAnswerJson)
    {
        if (string.IsNullOrWhiteSpace(submittedAnswerJson)) return null;

        try
        {
            var dto = JsonSerializer.Deserialize<ReadingMultipleChoiceMultiSubmittedAnswer>(submittedAnswerJson, JsonOptions);
            return dto?.SelectedOptionIds is { Count: > 0 } ? dto.SelectedOptionIds : null;
        }
        catch (JsonException)
        {
            return null;
        }
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

/// <summary>
/// practiceContent.exerciseData shape for reading_multiple_choice_multi.
/// </summary>
public sealed class ReadingMultipleChoiceMultiExerciseData
{
    public List<string>? CorrectOptionIds { get; set; }
    public string? Explanation { get; set; }
    public Dictionary<string, string>? OptionExplanations { get; set; }
}

/// <summary>
/// Submitted answer shape for reading_multiple_choice_multi.
/// </summary>
public sealed class ReadingMultipleChoiceMultiSubmittedAnswer
{
    public List<string> SelectedOptionIds { get; set; } = [];
}
