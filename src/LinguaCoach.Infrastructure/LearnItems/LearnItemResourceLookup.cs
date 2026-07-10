using LinguaCoach.Application.ResourceImport;
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
        var e = await db.ResourceBankItems.FirstOrDefaultAsync(x => x.Id == resourceId && x.Type == resourceType, ct);
        if (e is null) return null;

        switch (resourceType)
        {
            case PublishedResourceType.Vocabulary:
            {
                var c = ResourceBankItemContent.Deserialize<VocabularyContent>(e.ContentJson);
                return new LearnItemResourceSnapshot(c.Word, c.Notes, e.CefrLevel, "Vocabulary", e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, null);
            }
            case PublishedResourceType.Grammar:
            {
                var c = ResourceBankItemContent.Deserialize<GrammarContent>(e.ContentJson);
                return new LearnItemResourceSnapshot(c.GrammarPoint, c.Description, e.CefrLevel, "Grammar", e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, null);
            }
            case PublishedResourceType.ReadingReference:
            {
                var c = ResourceBankItemContent.Deserialize<ReadingReferenceContent>(e.ContentJson);
                return new LearnItemResourceSnapshot(
                    !string.IsNullOrWhiteSpace(c.TextType) ? c.TextType! : "Reading reference",
                    c.ReferenceExcerpt, e.CefrLevel, "Reading", e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, null);
            }
            case PublishedResourceType.ReadingPassage:
            {
                var c = ResourceBankItemContent.Deserialize<ReadingPassageContent>(e.ContentJson);
                return new LearnItemResourceSnapshot(c.Title, c.Summary ?? c.PassageText, e.CefrLevel, c.PrimarySkill, e.Subskill,
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
