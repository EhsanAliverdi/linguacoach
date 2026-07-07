using System.Text.Json;
using LinguaCoach.Application.FormIo;
using LinguaCoach.Application.Placement;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>
/// Deterministic Form.io-native placement scorer. No AI, no LLM, fully reproducible.
/// Iterates every component declared in the item's scoring rules document and scores each one
/// independently against the matching key in the student's submission data, then aggregates.
/// </summary>
public sealed class PlacementScoringService : IPlacementScoringService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PlacementScoreResult ScoreSubmission(string? scoringRulesJson, IReadOnlyDictionary<string, JsonElement> submissionData)
    {
        if (string.IsNullOrWhiteSpace(scoringRulesJson))
            return new PlacementScoreResult(false, 0.0, "No scoring rules defined for this item.", []);

        ScoringRulesDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<ScoringRulesDocument>(scoringRulesJson, JsonOptions);
        }
        catch (JsonException)
        {
            return new PlacementScoreResult(false, 0.0, "Scoring rules are malformed.", []);
        }

        if (doc is null || doc.Components.Count == 0)
            return new PlacementScoreResult(false, 0.0, "No scoring rules defined for this item.", []);

        var results = new List<ComponentScoreResult>();
        foreach (var (key, rule) in doc.Components)
        {
            submissionData.TryGetValue(key, out var value);
            results.Add(ComponentAnswerScorer.Score(key, rule, value));
        }

        var scorable = results.Where(r => r.MaxPoints > 0).ToList();
        var totalPoints = scorable.Sum(r => r.PointsEarned);
        var totalMax = scorable.Sum(r => r.MaxPoints);

        var score = totalMax > 0 ? totalPoints / totalMax : 0.0;
        var isCorrect = totalMax > 0 && totalPoints >= totalMax - 0.0001;

        var notes = scorable.Count == 0
            ? "This item requires manual or AI evaluation."
            : isCorrect
                ? $"Correct. {results.Count} component(s) scored."
                : $"Incorrect. {scorable.Count(r => r.IsCorrect)}/{scorable.Count} component(s) correct.";

        return new PlacementScoreResult(isCorrect, Math.Round(score, 4), notes, results);
    }
}
