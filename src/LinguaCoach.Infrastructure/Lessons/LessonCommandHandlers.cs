using System.Text.Json;
using LinguaCoach.Application.Lessons;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Lessons;

public sealed class AdminCreateLessonHandler : IAdminCreateLessonHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminCreateLessonHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<LessonDto> HandleAsync(CreateLessonCommand command, CancellationToken ct = default)
    {
        Lesson item;
        try
        {
            item = new Lesson(
                command.Title, command.Body, LessonSourceMode.Manual,
                command.CefrLevel, command.Skill, command.Subskill,
                ToJsonArray(command.ContextTags), ToJsonArray(command.FocusTags),
                ToJsonArray(command.Examples), ToJsonArray(command.CommonMistakes),
                command.UsageNotes, command.DifficultyBand, command.EstimatedMinutes,
                generationProvider: null, generationModel: null, createdByUserId: command.CreatedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new LessonValidationException(ex.Message);
        }

        _db.Lessons.Add(item);
        await _db.SaveChangesAsync(ct);

        var links = await LessonLinkBuilder.BuildAndAddAsync(_db, item.Id, command.Links, ct);
        if (links.Count > 0)
            await _db.SaveChangesAsync(ct);

        return LessonMappers.ToDto(item, links);
    }

    private static string ToJsonArray(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : "[]";
}

public sealed class AdminUpdateLessonHandler : IAdminUpdateLessonHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminUpdateLessonHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<LessonDto> HandleAsync(UpdateLessonCommand command, CancellationToken ct = default)
    {
        var item = await _db.Lessons.FirstOrDefaultAsync(i => i.Id == command.Id, ct)
            ?? throw new LessonValidationException($"Lesson '{command.Id}' was not found.");

        try
        {
            item.UpdateDraft(
                command.Title, command.Body,
                ToJsonArray(command.Examples), ToJsonArray(command.CommonMistakes), command.UsageNotes,
                command.CefrLevel, command.Skill, command.Subskill,
                ToJsonArray(command.ContextTags), ToJsonArray(command.FocusTags),
                command.DifficultyBand, command.EstimatedMinutes);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new LessonValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        var links = await _db.LessonResourceLinks.Where(l => l.LessonId == item.Id).ToListAsync(ct);
        return LessonMappers.ToDto(item, links);
    }

    private static string ToJsonArray(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : "[]";
}

public sealed class AdminApproveLessonHandler : IAdminApproveLessonHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminApproveLessonHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<LessonDto> HandleAsync(ApproveLessonCommand command, CancellationToken ct = default)
    {
        var item = await _db.Lessons.FirstOrDefaultAsync(i => i.Id == command.Id, ct)
            ?? throw new LessonValidationException($"Lesson '{command.Id}' was not found.");

        item.Approve(command.ReviewedByUserId, command.Notes);
        await _db.SaveChangesAsync(ct);

        var links = await _db.LessonResourceLinks.Where(l => l.LessonId == item.Id).ToListAsync(ct);
        return LessonMappers.ToDto(item, links);
    }
}

public sealed class AdminRejectLessonHandler : IAdminRejectLessonHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminRejectLessonHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<LessonDto> HandleAsync(RejectLessonCommand command, CancellationToken ct = default)
    {
        var item = await _db.Lessons.FirstOrDefaultAsync(i => i.Id == command.Id, ct)
            ?? throw new LessonValidationException($"Lesson '{command.Id}' was not found.");

        try
        {
            item.Reject(command.Reason, command.ReviewedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new LessonValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        var links = await _db.LessonResourceLinks.Where(l => l.LessonId == item.Id).ToListAsync(ct);
        return LessonMappers.ToDto(item, links);
    }
}

/// <summary>Shared link-creation logic for <see cref="AdminCreateLessonHandler"/> and
/// <see cref="LessonGenerationService"/>: parses/validates each requested
/// <see cref="LessonResourceLinkInput"/>, looks up the referenced published row, and stages a
/// <see cref="LessonResourceLink"/> (added to the context, not yet saved) for each one that
/// resolves. Throws <see cref="LessonValidationException"/> on the first invalid type/role or
/// not-found resource — never silently drops a bad reference.</summary>
internal static class LessonLinkBuilder
{
    public static async Task<List<LessonResourceLink>> BuildAndAddAsync(
        LinguaCoachDbContext db, Guid lessonId, IReadOnlyList<LessonResourceLinkInput>? inputs, CancellationToken ct)
    {
        var links = new List<LessonResourceLink>();
        if (inputs is null) return links;

        foreach (var input in inputs)
        {
            if (!LessonResourceLookup.TryParseResourceType(input.ResourceType, out var resourceType))
                throw new LessonValidationException($"Unsupported resource type '{input.ResourceType}'.");
            if (!LessonResourceLookup.TryParseRole(input.Role, out var role))
                throw new LessonValidationException($"Unsupported resource link role '{input.Role}'.");

            var snapshot = await LessonResourceLookup.FindAsync(db, resourceType, input.ResourceId, ct)
                ?? throw new LessonValidationException(
                    $"Resource '{input.ResourceType}:{input.ResourceId}' was not found in the published Resource Bank.");

            var link = new LessonResourceLink(
                lessonId, resourceType, input.ResourceId, role, snapshot.Title, snapshot.ContentFingerprint);
            links.Add(link);
            db.LessonResourceLinks.Add(link);
        }

        return links;
    }
}
