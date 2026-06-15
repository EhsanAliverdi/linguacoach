using System.Text.Json;
using System.Text.RegularExpressions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity.Evaluators;

/// <summary>
/// Evaluates gap-fill activities deterministically by comparing submitted answers
/// against accepted answers from contentJson. No AI call is made.
/// </summary>
public sealed class ExactMatchEvaluator : IPatternEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MarkingMode MarkingMode => MarkingMode.ExactMatch;

    public Task<PatternEvaluationResult> EvaluateAsync(
        PatternEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ExercisePatternKey == "reorder_paragraphs")
            return EvaluateReorderParagraphsAsync(request);

        var expectedItems = ParseExpectedItems(request.ContentJson, request.ExercisePatternKey);
        var submittedMap = ParseSubmittedAnswers(request.SubmittedAnswerJson);

        var itemResults = new List<PatternEvaluationItemResult>();
        double totalScore = 0;
        double totalMax = expectedItems.Count;

        foreach (var (key, accepted) in expectedItems)
        {
            submittedMap.TryGetValue(key, out var raw);
            var normalized = Normalize(raw);
            var isCorrect = accepted.Any(a => string.Equals(Normalize(a), normalized, StringComparison.Ordinal));
            var score = isCorrect ? 1.0 : 0.0;
            totalScore += score;

            itemResults.Add(new PatternEvaluationItemResult(
                ItemKey: key,
                StudentAnswer: raw,
                CorrectAnswer: accepted.FirstOrDefault(),
                AcceptedAnswers: accepted,
                IsCorrect: isCorrect,
                Score: score,
                MaxScore: 1,
                Feedback: isCorrect ? "Correct." : $"The expected answer is \"{accepted.FirstOrDefault()}\"."));
        }

        var percentage = PatternEvaluationResult.CalculatePercentage(totalScore, totalMax);
        var passed = totalMax == 0 || percentage >= 60;

        var coachSummary = BuildCoachSummary(itemResults);

        var result = PatternEvaluationResult.Create(
            score: totalScore,
            maxScore: totalMax,
            passed: passed,
            completed: true,
            itemResults: itemResults,
            coachSummary: coachSummary);

        return Task.FromResult(result);
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

    private static List<(string Key, IReadOnlyList<string> Accepted)> ParseExpectedItems(
        string contentJson, string? patternKey)
    {
        var result = new List<(string, IReadOnlyList<string>)>();
        var json = UnwrapStagedContent(contentJson);

        if (patternKey == "listen_and_gap_fill")
        {
            var content = JsonSerializer.Deserialize<ListenAndGapFillContent>(json, JsonOptions);
            if (content?.Gaps is null) return result;
            foreach (var gap in content.Gaps)
            {
                var key = gap.Id ?? gap.SentenceWithBlank ?? Guid.NewGuid().ToString();
                var accepted = BuildAcceptedList(gap.Answer);
                result.Add((key, accepted));
            }
        }
        else if (patternKey == "reading_fill_in_blanks" || patternKey == "reading_writing_fill_in_blanks")
        {
            var content = JsonSerializer.Deserialize<ReadingFillInBlanksContent>(json, JsonOptions);
            if (content?.Gaps is null) return result;
            foreach (var gap in content.Gaps)
            {
                var key = gap.Id ?? Guid.NewGuid().ToString();
                var accepted = BuildAcceptedList(gap.Answer);
                result.Add((key, accepted));
            }
        }
        else
        {
            // gap_fill_workplace_phrase (and fallback)
            var content = JsonSerializer.Deserialize<GapFillWorkplacePhraseContent>(json, JsonOptions);
            if (content?.Items is null) return result;
            for (var i = 0; i < content.Items.Count; i++)
            {
                var item = content.Items[i];
                var key = $"gap_{i + 1}";
                var accepted = BuildAcceptedList(item.Answer);
                result.Add((key, accepted));
            }
        }

        return result;
    }

    private static IReadOnlyList<string> BuildAcceptedList(string? primary)
    {
        if (string.IsNullOrWhiteSpace(primary)) return Array.Empty<string>();
        // Primary answer may encode alternatives separated by " / " or "|"
        var parts = Regex.Split(primary, @"\s*[/|]\s*")
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();
        return parts.Count > 0 ? parts : [primary.Trim()];
    }

    private static Dictionary<string, string?> ParseSubmittedAnswers(string submittedAnswerJson)
    {
        if (string.IsNullOrWhiteSpace(submittedAnswerJson))
            return new Dictionary<string, string?>(StringComparer.Ordinal);

        try
        {
            var dto = JsonSerializer.Deserialize<GapFillSubmittedAnswer>(submittedAnswerJson, JsonOptions);
            if (dto?.Answers is not null)
                return dto.Answers;
        }
        catch (JsonException) { /* fall through */ }

        return new Dictionary<string, string?>(StringComparer.Ordinal);
    }

    // ── normalization ──────────────────────────────────────────────────────────

    public static string Normalize(string? input)
    {
        if (input is null) return string.Empty;
        // Lowercase
        var s = input.ToLowerInvariant();
        // Collapse whitespace
        s = Regex.Replace(s, @"\s+", " ").Trim();
        // Strip trailing punctuation that doesn't affect meaning
        s = Regex.Replace(s, @"[.,!?;:]+$", string.Empty).TrimEnd();
        return s;
    }

    // ── reorder_paragraphs ────────────────────────────────────────────────────

    private static Task<PatternEvaluationResult> EvaluateReorderParagraphsAsync(
        PatternEvaluationRequest request)
    {
        var json = UnwrapStagedContent(request.ContentJson);
        ReorderParagraphsContent? content = null;
        try { content = JsonSerializer.Deserialize<ReorderParagraphsContent>(json, JsonOptions); }
        catch (JsonException) { /* fall through to empty */ }

        var correctOrder = content?.CorrectOrder ?? [];
        var totalMax = (double)correctOrder.Count;

        ReorderParagraphsSubmittedAnswer? submitted = null;
        if (!string.IsNullOrWhiteSpace(request.SubmittedAnswerJson))
        {
            try { submitted = JsonSerializer.Deserialize<ReorderParagraphsSubmittedAnswer>(request.SubmittedAnswerJson, JsonOptions); }
            catch (JsonException) { /* leave null */ }
        }

        var submittedOrder = submitted?.OrderedIds ?? [];

        // Deduplicate submitted ids (keep first occurrence)
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var deduped = submittedOrder.Where(id => seen.Add(id)).ToList();

        var itemResults = new List<PatternEvaluationItemResult>();
        double totalScore = 0;

        for (var i = 0; i < correctOrder.Count; i++)
        {
            var expectedId = correctOrder[i];
            var submittedId = i < deduped.Count ? deduped[i] : null;
            var isCorrect = string.Equals(submittedId, expectedId, StringComparison.Ordinal);
            var score = isCorrect ? 1.0 : 0.0;
            totalScore += score;

            string itemExplanation = string.Empty;
            if (content?.ItemExplanations is not null)
                content.ItemExplanations.TryGetValue(expectedId, out itemExplanation!);

            var feedback = isCorrect
                ? $"Correct — \"{expectedId}\" is in the right position."
                : submittedId is null
                    ? $"Position {i + 1}: expected \"{expectedId}\" but nothing was placed here."
                    : $"Position {i + 1}: you placed \"{submittedId}\" but \"{expectedId}\" belongs here.";

            if (!string.IsNullOrWhiteSpace(itemExplanation))
                feedback += $" {itemExplanation}";

            itemResults.Add(new PatternEvaluationItemResult(
                ItemKey: $"position_{i + 1}",
                StudentAnswer: submittedId,
                CorrectAnswer: expectedId,
                AcceptedAnswers: [expectedId],
                IsCorrect: isCorrect,
                Score: score,
                MaxScore: 1,
                Feedback: feedback));
        }

        var percentage = PatternEvaluationResult.CalculatePercentage(totalScore, totalMax);
        var passed = totalMax == 0 || percentage >= 60;

        var correct = itemResults.Count(r => r.IsCorrect);
        var total = itemResults.Count;
        string coachSummary;
        if (total == 0)
            coachSummary = "Activity completed.";
        else if (correct == total)
            coachSummary = !string.IsNullOrWhiteSpace(content?.Explanation)
                ? $"Well done — perfect order! {content.Explanation}"
                : "Well done — the paragraphs are in the correct order!";
        else if (correct == 0)
            coachSummary = "Review how topic sentences, pronouns, and sequence words connect paragraphs logically.";
        else
            coachSummary = $"You placed {correct} of {total} paragraphs correctly. Look at the highlighted positions and consider how each paragraph connects to the next.";

        var result = PatternEvaluationResult.Create(
            score: totalScore,
            maxScore: totalMax,
            passed: passed,
            completed: true,
            itemResults: itemResults,
            coachSummary: coachSummary);

        return Task.FromResult(result);
    }

    // ── coach summary ──────────────────────────────────────────────────────────

    private static string BuildCoachSummary(List<PatternEvaluationItemResult> items)
    {
        var correct = items.Count(i => i.IsCorrect);
        var total = items.Count;
        if (total == 0) return "Activity completed.";
        if (correct == total) return "Well done — all gaps filled correctly!";
        if (correct == 0) return "Review the target phrases carefully and try again.";
        return $"You got {correct} out of {total} correct. Review the highlighted gaps to improve your score.";
    }
}

/// <summary>
/// Submitted answer shape for gap-fill patterns.
/// Keys are gap IDs (e.g. "gap_1", "gap_2") or gap item IDs from contentJson.
/// </summary>
public sealed class GapFillSubmittedAnswer
{
    public Dictionary<string, string?> Answers { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Submitted answer shape for reorder_paragraphs.
/// </summary>
public sealed class ReorderParagraphsSubmittedAnswer
{
    public List<string> OrderedIds { get; set; } = [];
}
