using System.Text.Json;
using LinguaCoach.Application.Lessons;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Lessons;

/// <summary>
/// Phase H3 — deterministic "Generate Learn" composer. Builds a pending-review
/// <see cref="Lesson"/> draft directly from the fields of one or more selected published
/// Resource Bank rows (see <see cref="LessonResourceLookup"/>) — no AI provider call. See
/// <see cref="IGenerateLessonFromResourcesHandler"/>'s doc comment for why: no existing AI
/// service in this codebase generates teaching prose from source text, and adding a new AI
/// feature key is out of scope for this foundation phase. Never modifies the resources it reads
/// from, never creates an Activity/Module row, never assigns anything to a student.
/// </summary>
public sealed class LessonGenerationService : IGenerateLessonFromResourcesHandler
{
    private const string GenerationProvider = "Deterministic";
    private const string GenerationModel = "lesson-draft-composer-v1";

    private readonly LinguaCoachDbContext _db;

    public LessonGenerationService(LinguaCoachDbContext db) => _db = db;

    public async Task<GenerateLessonFromResourcesResult> HandleAsync(
        GenerateLessonFromResourcesRequest request, CancellationToken ct = default)
    {
        if (request.Resources is not { Count: > 0 })
            throw new LessonValidationException("At least one resource is required to generate a Lesson.");
        if (request.DefaultCefrLevel is not null && !CefrLevelConstants.IsValid(request.DefaultCefrLevel))
            throw new LessonValidationException($"Default CEFR level '{request.DefaultCefrLevel}' is not a valid CEFR level.");
        if (request.DefaultDifficultyBand is < 1 or > 5)
            throw new LessonValidationException("Default difficulty band must be between 1 and 5.");

        var resolved = new List<(LessonResourceLinkInput Input, PublishedResourceType Type, LessonResourceRole Role, LessonResourceSnapshot Snapshot)>();
        foreach (var input in request.Resources)
        {
            if (!LessonResourceLookup.TryParseResourceType(input.ResourceType, out var resourceType))
                throw new LessonValidationException($"Unsupported resource type '{input.ResourceType}'.");
            if (!LessonResourceLookup.TryParseRole(input.Role, out var role))
                throw new LessonValidationException($"Unsupported resource link role '{input.Role}'.");

            var snapshot = await LessonResourceLookup.FindAsync(_db, resourceType, input.ResourceId, ct)
                ?? throw new LessonValidationException(
                    $"Resource '{input.ResourceType}:{input.ResourceId}' was not found in the published Resource Bank.");

            resolved.Add((input, resourceType, role, snapshot));
        }

        var primary = resolved.FirstOrDefault(r => r.Role == LessonResourceRole.Primary).Snapshot ?? resolved[0].Snapshot;

        var title = !string.IsNullOrWhiteSpace(request.Title) ? request.Title!.Trim() : primary.Title;
        var cefrLevel = request.DefaultCefrLevel ?? primary.CefrLevel;
        var skill = request.DefaultSkill ?? primary.Skill;
        var subskill = request.DefaultSubskill ?? primary.Subskill;
        var contextTags = request.DefaultContextTags is { Count: > 0 }
            ? request.DefaultContextTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.ContextTagsJson));
        var focusTags = request.DefaultFocusTags is { Count: > 0 }
            ? request.DefaultFocusTags
            : MergeTagArrays(resolved.Select(r => r.Snapshot.FocusTagsJson));
        var difficultyBand = request.DefaultDifficultyBand ?? primary.DifficultyBand;

        var body = ComposeBody(resolved.Select(r => (r.Snapshot.Title, r.Snapshot.Body)).ToList());
        var examples = resolved
            .Select(r => r.Snapshot.Body)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => b!.Trim())
            .Distinct()
            .ToList();
        var usageNotes = $"Deterministic draft — review and edit before approval. Generated from: "
            + string.Join(", ", resolved.Select(r => r.Snapshot.Title)) + ".";

        Lesson item;
        try
        {
            item = new Lesson(
                title, body, LessonSourceMode.GeneratedFromResources,
                cefrLevel, skill, subskill,
                JsonSerializer.Serialize(contextTags), JsonSerializer.Serialize(focusTags),
                JsonSerializer.Serialize(examples), "[]",
                request.Notes is not null ? $"{usageNotes} {request.Notes.Trim()}" : usageNotes,
                difficultyBand, estimatedMinutes: null,
                GenerationProvider, GenerationModel, request.CreatedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new LessonValidationException(ex.Message);
        }

        _db.Lessons.Add(item);
        await _db.SaveChangesAsync(ct);

        var links = resolved
            .Select(r => new LessonResourceLink(
                item.Id, r.Type, r.Input.ResourceId, r.Role, r.Snapshot.Title, r.Snapshot.ContentFingerprint))
            .ToList();
        _db.LessonResourceLinks.AddRange(links);
        await _db.SaveChangesAsync(ct);

        var dto = LessonMappers.ToDto(item, links);
        return new GenerateLessonFromResourcesResult(dto, $"/admin/lessons?id={item.Id}");
    }

    private static string ComposeBody(IReadOnlyList<(string Title, string? Body)> resources)
    {
        var intro = $"Deterministic draft generated from {resources.Count} linked resource(s):";
        var paragraphs = resources.Select(r =>
            $"**{r.Title}** — {(!string.IsNullOrWhiteSpace(r.Body) ? r.Body!.Trim() : "(no additional detail available)")}");
        return intro + "\n\n" + string.Join("\n\n", paragraphs);
    }

    private static List<string> MergeTagArrays(IEnumerable<string?> jsonArrays)
    {
        var merged = new List<string>();
        foreach (var json in jsonArrays)
        {
            if (string.IsNullOrWhiteSpace(json)) continue;
            try
            {
                var tags = JsonSerializer.Deserialize<List<string>>(json);
                if (tags is null) continue;
                foreach (var tag in tags)
                    if (!string.IsNullOrWhiteSpace(tag) && !merged.Contains(tag, StringComparer.OrdinalIgnoreCase))
                        merged.Add(tag);
            }
            catch (JsonException)
            {
                // Malformed tag JSON on a source row is never fatal to generation — skip it.
            }
        }
        return merged;
    }
}
