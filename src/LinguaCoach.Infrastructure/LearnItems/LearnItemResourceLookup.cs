using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.LearnItems;

/// <summary>A read-only snapshot of a published Resource Bank row's fields, fetched by
/// (<see cref="PublishedResourceType"/>, id) — the same "typed discriminator + id" key
/// <see cref="LinguaCoach.Domain.Entities.LearnItemResourceLink"/> stores. Used both to validate a
/// resource reference exists before linking it, and as the raw material for the deterministic
/// draft composer (<see cref="LearnItemGenerationService"/>).</summary>
internal sealed record LearnItemResourceSnapshot(
    string Title,
    string? Body,
    string CefrLevel,
    string Skill,
    string? Subskill,
    string? ContextTagsJson,
    string? FocusTagsJson,
    int? DifficultyBand,
    string? ContentFingerprint
);

internal static class LearnItemResourceLookup
{
    public static async Task<LearnItemResourceSnapshot?> FindAsync(
        LinguaCoachDbContext db, PublishedResourceType resourceType, Guid resourceId, CancellationToken ct)
    {
        switch (resourceType)
        {
            case PublishedResourceType.Vocabulary:
            {
                var e = await db.CefrVocabularyEntries.FirstOrDefaultAsync(x => x.Id == resourceId, ct);
                return e is null ? null : new LearnItemResourceSnapshot(
                    e.Word, e.Notes, e.CefrLevel, "Vocabulary", e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, null);
            }
            case PublishedResourceType.Grammar:
            {
                var e = await db.CefrGrammarProfileEntries.FirstOrDefaultAsync(x => x.Id == resourceId, ct);
                return e is null ? null : new LearnItemResourceSnapshot(
                    e.GrammarPoint, e.Description, e.CefrLevel, "Grammar", e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, null);
            }
            case PublishedResourceType.ReadingReference:
            {
                var e = await db.CefrReadingReferences.FirstOrDefaultAsync(x => x.Id == resourceId, ct);
                return e is null ? null : new LearnItemResourceSnapshot(
                    !string.IsNullOrWhiteSpace(e.TextType) ? e.TextType! : "Reading reference",
                    e.ReferenceExcerpt, e.CefrLevel, "Reading", e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, null);
            }
            case PublishedResourceType.ReadingPassage:
            {
                var e = await db.CefrReadingPassages.FirstOrDefaultAsync(x => x.Id == resourceId, ct);
                return e is null ? null : new LearnItemResourceSnapshot(
                    e.Title, e.Summary ?? e.PassageText, e.CefrLevel, e.PrimarySkill, e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, e.ContentFingerprint);
            }
            default:
                return null;
        }
    }

    public static bool TryParseResourceType(string? raw, out PublishedResourceType resourceType) =>
        Enum.TryParse(raw, ignoreCase: true, out resourceType);

    public static bool TryParseRole(string? raw, out LearnItemResourceRole role) =>
        Enum.TryParse(raw, ignoreCase: true, out role);
}
