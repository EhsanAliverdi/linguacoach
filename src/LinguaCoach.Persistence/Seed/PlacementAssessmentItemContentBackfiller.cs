using LinguaCoach.Domain.Questions;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Persistence.Seed;

// Backfills ContentJson/AnswerJson (Unified Question-Schema Phase 2) onto PlacementAssessmentItem
// rows created before those fields existed — historical per-attempt item rows from completed or
// in-progress assessments, which must never be dropped or rewritten, only additively enriched.
// Idempotent: only touches rows where ContentJson is still null.
public static class PlacementAssessmentItemContentBackfiller
{
    public static async Task BackfillAsync(LinguaCoachDbContext db)
    {
        var pending = await db.PlacementAssessmentItems
            .Where(i => i.ContentJson == null)
            .ToListAsync();

        if (pending.Count == 0) return;

        foreach (var item in pending)
        {
            var content = LegacyPlacementContentConverter.FromLegacyItem(
                item.ItemType, item.Prompt, item.CorrectAnswer ?? string.Empty,
                item.ReadingPassage, item.ListeningAudioScript);
            item.BackfillContent(content, item.Response);
        }

        await db.SaveChangesAsync();
    }
}
