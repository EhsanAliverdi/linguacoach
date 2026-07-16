using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Lessons;

/// <summary>A read-only snapshot of a published Resource Bank row's fields, fetched by
/// (<see cref="PublishedResourceType"/>, id) — the same "typed discriminator + id" key
/// <see cref="LinguaCoach.Domain.Entities.LessonResourceLink"/> stores. Used both to validate a
/// resource reference exists before linking it, and as the raw material for the deterministic
/// draft composer (<see cref="LessonGenerationService"/>).</summary>
/// <summary>
/// Phase 4.6 — <see cref="AudioStorageKey"/>/<see cref="AudioContentType"/>/
/// <see cref="AudioDurationSeconds"/>/<see cref="MediaType"/>/<see cref="ImageUrl"/> are discovery
/// fields only: they surface a Listening/Speaking resource's media so a future consumer can use it,
/// but exercise/lesson generation itself remains text-composition only and does not read them (see
/// LessonGenerationService — deliberately unchanged by this phase). All null for resource types
/// with no associated media (Vocabulary/Grammar/Reading/Writing).
/// </summary>
internal sealed record LessonResourceSnapshot(
    string Title,
    string? Body,
    string CefrLevel,
    string Skill,
    string? Subskill,
    string? ContextTagsJson,
    string? FocusTagsJson,
    int? DifficultyBand,
    string? ContentFingerprint,
    string? MediaType = null,
    string? AudioStorageKey = null,
    string? AudioContentType = null,
    decimal? AudioDurationSeconds = null,
    string? ImageUrl = null
);

internal static class LessonResourceLookup
{
    public static async Task<LessonResourceSnapshot?> FindAsync(
        LinguaCoachDbContext db, PublishedResourceType resourceType, Guid resourceId, CancellationToken ct)
    {
        var e = await db.ResourceBankItems.FirstOrDefaultAsync(x => x.Id == resourceId && x.Type == resourceType, ct);
        if (e is null) return null;

        switch (resourceType)
        {
            case PublishedResourceType.Vocabulary:
            {
                var c = ResourceBankItemContent.Deserialize<VocabularyContent>(e.ContentJson);
                return new LessonResourceSnapshot(c.Word, c.Notes, e.CefrLevel, "Vocabulary", e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, null);
            }
            case PublishedResourceType.Grammar:
            {
                var c = ResourceBankItemContent.Deserialize<GrammarContent>(e.ContentJson);
                return new LessonResourceSnapshot(c.GrammarPoint, c.Description, e.CefrLevel, "Grammar", e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, null);
            }
            case PublishedResourceType.ReadingReference:
            {
                var c = ResourceBankItemContent.Deserialize<ReadingReferenceContent>(e.ContentJson);
                return new LessonResourceSnapshot(
                    !string.IsNullOrWhiteSpace(c.TextType) ? c.TextType! : "Reading reference",
                    c.ReferenceExcerpt, e.CefrLevel, "Reading", e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, null);
            }
            case PublishedResourceType.ReadingPassage:
            {
                var c = ResourceBankItemContent.Deserialize<ReadingPassageContent>(e.ContentJson);
                return new LessonResourceSnapshot(c.Title, c.Summary ?? c.PassageText, e.CefrLevel, c.PrimarySkill, e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, e.ContentFingerprint);
            }
            // Phase K17 — Writing/Listening/Speaking resources have been importable/publishable
            // since J5a/J5c/J5d, but Lessons could never be built from them: this switch never had
            // a case for them, so LessonResourceLookup.FindAsync silently returned null and every
            // Lesson/Exercise generation call against one of these resources failed with "Resource
            // ... was not found in the published Resource Bank" even though it existed. Added here
            // so Writing composers (email_reply etc.) have a resource type to actually work from.
            case PublishedResourceType.Writing:
            {
                var c = ResourceBankItemContent.Deserialize<WritingPromptContent>(e.ContentJson);
                return new LessonResourceSnapshot(c.Title, c.PromptText, e.CefrLevel, "Writing", e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, null);
            }
            case PublishedResourceType.Listening:
            {
                var c = ResourceBankItemContent.Deserialize<ListeningPassageContent>(e.ContentJson);
                return new LessonResourceSnapshot(c.Title, c.Transcript, e.CefrLevel, "Listening", e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, null,
                    MediaType: "Audio", AudioStorageKey: c.AudioStorageKey, AudioContentType: c.AudioContentType,
                    AudioDurationSeconds: c.AudioDurationSeconds);
            }
            case PublishedResourceType.Speaking:
            {
                var c = ResourceBankItemContent.Deserialize<SpeakingPromptContent>(e.ContentJson);
                return new LessonResourceSnapshot(c.Title, c.PromptText, e.CefrLevel, "Speaking", e.Subskill,
                    e.ContextTagsJson, e.FocusTagsJson, e.DifficultyBand, null,
                    MediaType: c.ImageUrl is not null ? "Image" : null, ImageUrl: c.ImageUrl);
            }
            default:
                return null;
        }
    }

    public static bool TryParseResourceType(string? raw, out PublishedResourceType resourceType) =>
        Enum.TryParse(raw, ignoreCase: true, out resourceType);

    public static bool TryParseRole(string? raw, out LessonResourceRole role) =>
        Enum.TryParse(raw, ignoreCase: true, out role);
}
