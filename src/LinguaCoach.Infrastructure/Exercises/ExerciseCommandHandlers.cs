using System.Text.Json;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Exercises;

public sealed class AdminCreateExerciseHandler : IAdminCreateExerciseHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminCreateExerciseHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ExerciseDto> HandleAsync(CreateExerciseCommand command, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ExerciseRendererType>(command.RendererType, ignoreCase: true, out var rendererType))
            throw new ExerciseValidationException($"Unsupported renderer type '{command.RendererType}'.");

        if (!await _db.Lessons.AnyAsync(l => l.Id == command.LessonId, ct))
            throw new ExerciseValidationException($"Lesson '{command.LessonId}' was not found.");

        Exercise item;
        try
        {
            item = new Exercise(
                command.Title, command.Instructions, command.ActivityType, rendererType, ExerciseSourceMode.Manual,
                command.Description, command.PatternKey, command.FormSchemaJson, command.AnswerKeyJson,
                command.ScoringRulesJson, command.FeedbackPlanJson, command.CefrLevel, command.Skill, command.Subskill,
                ToJsonArray(command.ContextTags), ToJsonArray(command.FocusTags),
                command.DifficultyBand, command.EstimatedMinutes, command.LessonId,
                generationProvider: null, generationModel: null, createdByUserId: command.CreatedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ExerciseValidationException(ex.Message);
        }

        _db.Exercises.Add(item);
        await _db.SaveChangesAsync(ct);

        var links = await ExerciseLinkBuilder.BuildAndAddAsync(_db, item.Id, command.Links, ct);
        if (links.Count > 0)
            await _db.SaveChangesAsync(ct);

        return ExerciseMappers.ToDto(item, links);
    }

    private static string ToJsonArray(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : "[]";
}

public sealed class AdminUpdateExerciseHandler : IAdminUpdateExerciseHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminUpdateExerciseHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ExerciseDto> HandleAsync(UpdateExerciseCommand command, CancellationToken ct = default)
    {
        var item = await _db.Exercises.FirstOrDefaultAsync(a => a.Id == command.Id, ct)
            ?? throw new ExerciseValidationException($"Activity '{command.Id}' was not found.");

        try
        {
            item.UpdateDraft(
                command.Title, command.Instructions, command.Description,
                command.FormSchemaJson, command.AnswerKeyJson, command.ScoringRulesJson, command.FeedbackPlanJson,
                command.CefrLevel, command.Skill, command.Subskill,
                ToJsonArray(command.ContextTags), ToJsonArray(command.FocusTags),
                command.DifficultyBand, command.EstimatedMinutes);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new ExerciseValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        var links = await _db.ExerciseResourceLinks.Where(l => l.ExerciseId == item.Id).ToListAsync(ct);
        return ExerciseMappers.ToDto(item, links);
    }

    private static string ToJsonArray(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : "[]";
}

public sealed class AdminApproveExerciseHandler : IAdminApproveExerciseHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminApproveExerciseHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ExerciseDto> HandleAsync(ApproveExerciseCommand command, CancellationToken ct = default)
    {
        var item = await _db.Exercises.FirstOrDefaultAsync(a => a.Id == command.Id, ct)
            ?? throw new ExerciseValidationException($"Activity '{command.Id}' was not found.");

        item.Approve(command.ReviewedByUserId, command.Notes);
        await _db.SaveChangesAsync(ct);

        var links = await _db.ExerciseResourceLinks.Where(l => l.ExerciseId == item.Id).ToListAsync(ct);
        return ExerciseMappers.ToDto(item, links);
    }
}

public sealed class AdminRejectExerciseHandler : IAdminRejectExerciseHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminRejectExerciseHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ExerciseDto> HandleAsync(RejectExerciseCommand command, CancellationToken ct = default)
    {
        var item = await _db.Exercises.FirstOrDefaultAsync(a => a.Id == command.Id, ct)
            ?? throw new ExerciseValidationException($"Activity '{command.Id}' was not found.");

        try
        {
            item.Reject(command.Reason, command.ReviewedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ExerciseValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        var links = await _db.ExerciseResourceLinks.Where(l => l.ExerciseId == item.Id).ToListAsync(ct);
        return ExerciseMappers.ToDto(item, links);
    }
}

/// <summary>Shared link-creation logic for <see cref="AdminCreateExerciseHandler"/> and
/// <see cref="ActivityGenerationService"/>. Mirrors <c>LessonLinkBuilder</c>'s shape exactly,
/// reusing the same <see cref="LessonResourceLookup"/> helper (generic over any published
/// resource type, not Learn-Item-specific despite its namespace) so resource validation/lookup
/// logic exists in exactly one place for both Lessons and Activities.</summary>
internal static class ExerciseLinkBuilder
{
    public static async Task<List<ExerciseResourceLink>> BuildAndAddAsync(
        LinguaCoachDbContext db, Guid exerciseId, IReadOnlyList<ExerciseResourceLinkInput>? inputs, CancellationToken ct)
    {
        var links = new List<ExerciseResourceLink>();
        if (inputs is null) return links;

        foreach (var input in inputs)
        {
            if (!LessonResourceLookup.TryParseResourceType(input.ResourceType, out var resourceType))
                throw new ExerciseValidationException($"Unsupported resource type '{input.ResourceType}'.");
            if (!LessonResourceLookup.TryParseRole(input.Role, out var role))
                throw new ExerciseValidationException($"Unsupported resource link role '{input.Role}'.");

            var snapshot = await LessonResourceLookup.FindAsync(db, resourceType, input.ResourceId, ct)
                ?? throw new ExerciseValidationException(
                    $"Resource '{input.ResourceType}:{input.ResourceId}' was not found in the published Resource Bank.");

            var link = new ExerciseResourceLink(
                exerciseId, resourceType, input.ResourceId, role, snapshot.Title, snapshot.ContentFingerprint);
            links.Add(link);
            db.ExerciseResourceLinks.Add(link);
        }

        return links;
    }
}
