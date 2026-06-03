using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Persistence.Seed;

/// <summary>
/// Seeds existing WritingScenario rows into LearningActivity as SystemFallback activities.
/// Idempotent — safe to run multiple times. Skips any scenario already mirrored.
/// </summary>
public static class LearningActivitySeeder
{
    public static async Task SeedAsync(
        LinguaCoachDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        var scenarios = await db.WritingScenarios
            .Where(s => s.IsActive)
            .ToListAsync(ct);

        if (scenarios.Count == 0)
        {
            logger.LogWarning("LearningActivitySeeder: no WritingScenarios found — skipping.");
            return;
        }

        // Collect scenario IDs that have already been mirrored.
        var existingSourceIds = await db.LearningActivities
            .Where(a => a.SourceWritingScenarioId != null)
            .Select(a => a.SourceWritingScenarioId!.Value)
            .ToHashSetAsync(ct);

        var toSeed = scenarios
            .Where(s => !existingSourceIds.Contains(s.Id))
            .ToList();

        if (toSeed.Count == 0)
        {
            logger.LogInformation("LearningActivitySeeder: all {Count} scenarios already mirrored.", scenarios.Count);
            return;
        }

        var activities = toSeed.Select(s =>
        {
            var phrases = TryDeserializeArray(s.TargetPhrasesJson);
            var vocab = TryDeserializeArray(s.TargetVocabularyJson);

            var content = new
            {
                situation = s.Situation,
                learningGoal = s.LearningGoal,
                targetPhrases = phrases,
                targetVocabulary = vocab,
                exampleText = s.ExampleText,
                commonMistakeToAvoid = s.CommonMistakeToAvoid,
                instructionInSourceLanguage = string.Empty
            };

            return new LearningActivity(
                activityType: ActivityType.WritingScenario,
                source: ActivitySource.SystemFallback,
                title: s.Title,
                difficulty: s.Difficulty,
                aiGeneratedContentJson: JsonSerializer.Serialize(content),
                learningModuleId: null,
                sourceWritingScenarioId: s.Id);
        }).ToList();

        db.LearningActivities.AddRange(activities);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "LearningActivitySeeder: mirrored {Count} WritingScenario(s) as SystemFallback LearningActivities.",
            activities.Count);
    }

    private static string[] TryDeserializeArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
