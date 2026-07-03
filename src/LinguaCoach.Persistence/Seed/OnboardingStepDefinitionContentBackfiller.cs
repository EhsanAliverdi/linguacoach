using LinguaCoach.Domain.Questions;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Persistence.Seed;

// Backfills ContentJson (Unified Question-Schema Phase 5) onto OnboardingStepDefinition rows
// created before this field existed — the live active flow's existing steps, which the seeder's
// "flows are immutable" rule means it never touches once created. Idempotent: only touches rows
// where ContentJson is still null, and only for the generic step types the shared schema covers
// (SingleChoice/MultipleChoice/FreeText/AssessmentQuestion) — the rest stay null by design.
public static class OnboardingStepDefinitionContentBackfiller
{
    public static async Task BackfillAsync(LinguaCoachDbContext db)
    {
        var pending = await db.OnboardingStepDefinitions
            .Where(s => s.ContentJson == null)
            .ToListAsync();

        var dirty = false;
        foreach (var step in pending)
        {
            var content = OnboardingContentConverter.FromLegacyStep(
                step.StepType, step.Title, step.OptionsJson, step.ValidationMetadataJson, step.AssessmentMetadataJson);
            if (content is null) continue;

            step.SetContent(content);
            dirty = true;
        }

        if (dirty) await db.SaveChangesAsync();
    }
}
