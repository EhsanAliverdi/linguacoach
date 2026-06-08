using LinguaCoach.Application.Placement;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>
/// Deterministic placement evaluator that does NOT call any AI provider.
/// Derives a CEFR level from the objective section scores (vocab/grammar, reading, listening)
/// and produces a stable, sensible result. Used in tests and as a safe fallback.
/// </summary>
public sealed class FakePlacementEvaluator : IPlacementEvaluator
{
    public Task<PlacementEvaluationResult> EvaluateAsync(PlacementEvaluationInput input, CancellationToken ct = default)
    {
        var scored = input.Sections.Where(s => s.Scored && s.Score.HasValue).ToList();
        var avg = scored.Count > 0 ? scored.Average(s => s.Score!.Value) : 55d;

        var overall = LevelFromScore(avg);

        string SectionLevel(string key)
        {
            var section = input.Sections.FirstOrDefault(s => s.SectionKey == key);
            if (section is null) return overall;
            if (section.Score.HasValue) return LevelFromScore(section.Score.Value);
            // Productive sections (writing/speaking): infer from amount of text produced.
            var len = section.ResponseText?.Length ?? 0;
            return len >= 200 ? overall : len >= 60 ? StepDown(overall) : "A2";
        }

        var skillLevels = new Dictionary<string, string>
        {
            ["grammar"] = SectionLevel(PlacementContent.VocabGrammarKey),
            ["vocabulary"] = SectionLevel(PlacementContent.VocabGrammarKey),
            ["reading"] = SectionLevel(PlacementContent.ReadingKey),
            ["listening"] = SectionLevel(PlacementContent.ListeningKey),
            ["writing"] = SectionLevel(PlacementContent.WritingKey),
            ["speaking"] = SectionLevel(PlacementContent.SpeakingKey),
            ["workplaceTone"] = StepDown(overall),
        };

        var strengths = new List<string>();
        var weaknesses = new List<string>();
        foreach (var (skill, level) in skillLevels)
        {
            if (IsBelow(level, "B1")) weaknesses.Add($"{skill} ({level})");
            else strengths.Add($"{skill} ({level})");
        }
        if (strengths.Count == 0) strengths.Add("willingness to practise");
        if (weaknesses.Count == 0) weaknesses.Add("formal workplace tone");

        var result = new PlacementEvaluationResult(
            EstimatedOverallLevel: overall,
            SkillLevels: skillLevels,
            Strengths: strengths.Take(4).ToList(),
            Weaknesses: weaknesses.Take(4).ToList(),
            RecommendedStartingCourse: $"Workplace English {overall}",
            RecommendedSessionDuration: 15,
            PlacementNotes: $"Based on your responses, your workplace English is around {overall}. We'll build a course that matches your level.");

        return Task.FromResult(result);
    }

    private static string LevelFromScore(double score) => score switch
    {
        >= 90 => "B2+",
        >= 80 => "B2",
        >= 70 => "B1+",
        >= 55 => "B1",
        >= 40 => "A2+",
        >= 25 => "A2",
        _ => "A1"
    };

    private static string StepDown(string level) => level switch
    {
        "C2" => "C1",
        "C1" => "B2+",
        "B2+" => "B2",
        "B2" => "B1+",
        "B1+" => "B1",
        "B1" => "A2+",
        "A2+" => "A2",
        "A2" => "A1",
        _ => "A1"
    };

    private static int Rank(string level) => level.Trim().ToUpperInvariant() switch
    {
        "A1" => 0, "A2" => 1, "A2+" => 2, "B1" => 3, "B1+" => 4,
        "B2" => 5, "B2+" => 6, "C1" => 7, "C2" => 8, _ => 3
    };

    private static bool IsBelow(string level, string threshold) => Rank(level) < Rank(threshold);
}
