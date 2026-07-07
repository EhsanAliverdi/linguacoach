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
