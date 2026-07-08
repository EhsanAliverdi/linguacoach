using System.Text.Json;
using LinguaCoach.Application.Placement;

namespace LinguaCoach.Application.FormIo;

/// <summary>Scores a single Form.io component's submitted value against a
/// <see cref="ComponentScoringRule"/> — shared by placement (<c>PlacementScoringService</c>) and
/// onboarding (<c>StudentOnboardingFlowService</c>'s CEFR quick-check questions) so the
/// single_choice/multiple_choice/text_exact/text_normalized comparison logic exists in exactly
/// one place.</summary>
public static class ComponentAnswerScorer
{
    public static ComponentScoreResult Score(string key, ComponentScoringRule rule, JsonElement value)
    {
        if (rule.RequiresManualOrAiEvaluation)
        {
            var raw = ExtractRawText(value);
            return new ComponentScoreResult(key, raw, false, 0.0, 0.0);
        }

        return rule.Kind switch
        {
            ScoringRuleKinds.SingleChoice => ScoreSingleChoice(key, rule, value),
            ScoringRuleKinds.MultipleChoice => ScoreMultipleChoice(key, rule, value),
            ScoringRuleKinds.TextExact => ScoreText(key, rule, value, normalize: false),
            ScoringRuleKinds.TextNormalized => ScoreText(key, rule, value, normalize: true),
            ScoringRuleKinds.OrderedSequence => ScoreOrderedSequence(key, rule, value),
            _ => new ComponentScoreResult(key, ExtractRawText(value), false, 0.0, rule.Points),
        };
    }

    private static ComponentScoreResult ScoreSingleChoice(string key, ComponentScoringRule rule, JsonElement value)
    {
        var given = ExtractRawText(value)?.Trim();
        var expected = rule.CorrectAnswer?.Trim();
        var isCorrect = !string.IsNullOrEmpty(given) && !string.IsNullOrEmpty(expected)
            && string.Equals(given, expected, StringComparison.OrdinalIgnoreCase);

        return new ComponentScoreResult(key, given, isCorrect, isCorrect ? rule.Points : 0.0, rule.Points);
    }

    private static ComponentScoreResult ScoreMultipleChoice(string key, ComponentScoringRule rule, JsonElement value)
    {
        var given = ExtractValueSet(value);
        var expected = (rule.CorrectAnswers ?? [])
            .Select(v => v.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var isCorrect = expected.Count > 0 && given.SetEquals(expected);
        var normalized = string.Join(",", given.OrderBy(v => v, StringComparer.OrdinalIgnoreCase));

        return new ComponentScoreResult(key, normalized, isCorrect, isCorrect ? rule.Points : 0.0, rule.Points);
    }

    private static ComponentScoreResult ScoreText(string key, ComponentScoringRule rule, JsonElement value, bool normalize)
    {
        var raw = ExtractRawText(value);
        var given = raw?.Trim() ?? string.Empty;
        var expected = rule.CorrectAnswer?.Trim() ?? string.Empty;

        if (normalize)
        {
            given = NormalizeText(given);
            expected = NormalizeText(expected);
        }

        var isCorrect = given.Length > 0 && expected.Length > 0
            && (normalize
                ? string.Equals(given, expected, StringComparison.OrdinalIgnoreCase)
                : string.Equals(given, expected, StringComparison.Ordinal));

        return new ComponentScoreResult(key, raw?.Trim(), isCorrect, isCorrect ? rule.Points : 0.0, rule.Points);
    }

    /// <summary>Scores a stock Form.io "datagrid" (reorder enabled) submission by positional
    /// comparison against the backend-only correct order — the same semantics
    /// ExactMatchEvaluator.EvaluateReorderParagraphsAsync uses for the legacy content-driven
    /// reorder_paragraphs path, so a Form.io-scored instance of the pattern behaves identically.
    /// One point per correctly-placed position; the component's max score is
    /// CorrectOrder.Count * Points.</summary>
    private static ComponentScoreResult ScoreOrderedSequence(string key, ComponentScoringRule rule, JsonElement value)
    {
        var correctOrder = rule.CorrectOrder ?? [];
        var submittedOrder = ExtractOrderedIds(value);

        // Deduplicate submitted ids (keep first occurrence) — mirrors the legacy evaluator.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var deduped = submittedOrder.Where(id => seen.Add(id)).ToList();

        var correctPositions = 0;
        for (var i = 0; i < correctOrder.Count; i++)
        {
            var submittedId = i < deduped.Count ? deduped[i] : null;
            if (string.Equals(submittedId, correctOrder[i], StringComparison.Ordinal))
                correctPositions++;
        }

        var maxPoints = correctOrder.Count * rule.Points;
        var pointsEarned = correctPositions * rule.Points;
        var isCorrect = correctOrder.Count > 0 && correctPositions == correctOrder.Count;
        var normalizedValue = string.Join(",", deduped);

        return new ComponentScoreResult(key, normalizedValue, isCorrect, pointsEarned, maxPoints);
    }

    /// <summary>Extracts row ids, in submitted order, from a Form.io datagrid submission value —
    /// an array of either plain id strings, or row objects carrying an "itemId" property (the
    /// convention used by reorder-style datagrid rows; see reorder_paragraphs_workplace_seed_v1).</summary>
    private static IReadOnlyList<string> ExtractOrderedIds(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) return [];

        var ids = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    ids.Add(item.GetString()!);
                    break;
                case JsonValueKind.Object when item.TryGetProperty("itemId", out var idProp)
                    && idProp.ValueKind == JsonValueKind.String:
                    ids.Add(idProp.GetString()!);
                    break;
            }
        }
        return ids;
    }

    private static string NormalizeText(string value) =>
        string.Join(' ', value.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static string? ExtractRawText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Array when value.GetArrayLength() > 0 => ExtractRawText(value[0]),
        _ => null,
    };

    /// <summary>Extracts a set of selected values from either a Form.io "selectboxes" object
    /// shape ({"key": true, "other": false}) or a plain JSON array of selected keys.</summary>
    public static HashSet<string> ExtractValueSet(JsonElement value)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in value.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.True) set.Add(prop.Name);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String) set.Add(item.GetString()!);
                }
                break;
            case JsonValueKind.String:
                set.Add(value.GetString()!);
                break;
        }

        return set;
    }
}
