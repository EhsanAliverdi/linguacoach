using System.Text.Json;
using LinguaCoach.Application.Modules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Modules;

public sealed class AdminCreateModuleHandler : IAdminCreateModuleHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminCreateModuleHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ModuleDto> HandleAsync(CreateModuleCommand command, CancellationToken ct = default)
    {
        if (command.LessonLinks is not { Count: > 0 })
            throw new ModuleValidationException("At least one Lesson is required to create a Module.");
        if (command.ExerciseLinks is not { Count: > 0 })
            throw new ModuleValidationException("At least one Exercise is required to create a Module.");

        Module module;
        try
        {
            module = new Module(
                command.Title, ModuleSourceMode.Manual, command.Description,
                command.CefrLevel, command.Skill, command.Subskill,
                ToJsonArray(command.ContextTags), ToJsonArray(command.FocusTags),
                command.DifficultyBand, command.EstimatedMinutes, command.FeedbackPlanJson,
                generationProvider: null, generationModel: null, createdByUserId: command.CreatedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ModuleValidationException(ex.Message);
        }

        _db.Modules.Add(module);
        await _db.SaveChangesAsync(ct);

        var (lessonLinks, exerciseLinks) = await ModuleLinkBuilder.BuildAndAddAsync(
            _db, module.Id, command.LessonLinks, command.ExerciseLinks, requireApproved: false, ct);
        await _db.SaveChangesAsync(ct);

        return ModuleMappers.ToDto(module, lessonLinks, exerciseLinks);
    }

    private static string ToJsonArray(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : "[]";
}

public sealed class AdminUpdateModuleHandler : IAdminUpdateModuleHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminUpdateModuleHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ModuleDto> HandleAsync(UpdateModuleCommand command, CancellationToken ct = default)
    {
        var module = await _db.Modules.FirstOrDefaultAsync(m => m.Id == command.Id, ct)
            ?? throw new ModuleValidationException($"Module '{command.Id}' was not found.");

        try
        {
            module.UpdateDraft(
                command.Title, command.Description, command.CefrLevel, command.Skill, command.Subskill,
                ToJsonArray(command.ContextTags), ToJsonArray(command.FocusTags),
                command.DifficultyBand, command.EstimatedMinutes, command.FeedbackPlanJson);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new ModuleValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        var lessonLinks = await _db.ModuleLessonLinks.Where(l => l.ModuleId == module.Id).ToListAsync(ct);
        var exerciseLinks = await _db.ModuleExerciseLinks.Where(l => l.ModuleId == module.Id).ToListAsync(ct);
        return ModuleMappers.ToDto(module, lessonLinks, exerciseLinks);
    }

    private static string ToJsonArray(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : "[]";
}

public sealed class AdminApproveModuleHandler : IAdminApproveModuleHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminApproveModuleHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ModuleDto> HandleAsync(ApproveModuleCommand command, CancellationToken ct = default)
    {
        var module = await _db.Modules.FirstOrDefaultAsync(m => m.Id == command.Id, ct)
            ?? throw new ModuleValidationException($"Module '{command.Id}' was not found.");

        module.Approve(command.ReviewedByUserId, command.Notes);
        await _db.SaveChangesAsync(ct);

        var lessonLinks = await _db.ModuleLessonLinks.Where(l => l.ModuleId == module.Id).ToListAsync(ct);
        var exerciseLinks = await _db.ModuleExerciseLinks.Where(l => l.ModuleId == module.Id).ToListAsync(ct);
        return ModuleMappers.ToDto(module, lessonLinks, exerciseLinks);
    }
}

public sealed class AdminRejectModuleHandler : IAdminRejectModuleHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminRejectModuleHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ModuleDto> HandleAsync(RejectModuleCommand command, CancellationToken ct = default)
    {
        var module = await _db.Modules.FirstOrDefaultAsync(m => m.Id == command.Id, ct)
            ?? throw new ModuleValidationException($"Module '{command.Id}' was not found.");

        try
        {
            module.Reject(command.Reason, command.ReviewedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ModuleValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        var lessonLinks = await _db.ModuleLessonLinks.Where(l => l.ModuleId == module.Id).ToListAsync(ct);
        var exerciseLinks = await _db.ModuleExerciseLinks.Where(l => l.ModuleId == module.Id).ToListAsync(ct);
        return ModuleMappers.ToDto(module, lessonLinks, exerciseLinks);
    }
}

/// <summary>Shared link-creation logic for <see cref="AdminCreateModuleHandler"/> and
/// <see cref="ModuleGenerationService"/>. When <paramref name="requireApproved"/> is true (every
/// generation entry point), a Lesson/Exercise that isn't
/// <see cref="AdminReviewStatus.Approved"/> is rejected outright rather than silently linked —
/// generated Modules only ever compose already-reviewed content. Manual creation
/// (<paramref name="requireApproved"/> = false) allows any status, mirroring
/// <c>AdminCreateExerciseHandler</c>'s manual-create flexibility.</summary>
internal static class ModuleLinkBuilder
{
    public static async Task<(List<ModuleLessonLink> LessonLinks, List<ModuleExerciseLink> ExerciseLinks)> BuildAndAddAsync(
        LinguaCoachDbContext db, Guid moduleId,
        IReadOnlyList<ModuleLessonLinkInput> lessonInputs, IReadOnlyList<ModuleExerciseLinkInput> activityInputs,
        bool requireApproved, CancellationToken ct)
    {
        var lessonLinks = new List<ModuleLessonLink>();
        var sortOrder = 0;
        foreach (var input in lessonInputs)
        {
            if (!Enum.TryParse<LessonResourceRole>(input.Role, ignoreCase: true, out var role))
                throw new ModuleValidationException($"Unsupported Lesson link role '{input.Role}'.");

            var lesson = await db.Lessons.FirstOrDefaultAsync(l => l.Id == input.LessonId, ct)
                ?? throw new ModuleValidationException($"Lesson '{input.LessonId}' was not found.");
            if (requireApproved && lesson.ReviewStatus != AdminReviewStatus.Approved)
                throw new ModuleValidationException(
                    $"Lesson '{lesson.Title}' is not approved yet — approve it before generating a Module from it.");

            var link = new ModuleLessonLink(moduleId, input.LessonId, role, sortOrder++, lesson.Title);
            lessonLinks.Add(link);
            db.ModuleLessonLinks.Add(link);
        }

        var exerciseLinks = new List<ModuleExerciseLink>();
        sortOrder = 0;
        foreach (var input in activityInputs)
        {
            if (!Enum.TryParse<ModuleExerciseRole>(input.Role, ignoreCase: true, out var role))
                throw new ModuleValidationException($"Unsupported Activity link role '{input.Role}'.");

            var activity = await db.Exercises.FirstOrDefaultAsync(a => a.Id == input.ExerciseId, ct)
                ?? throw new ModuleValidationException($"Exercise '{input.ExerciseId}' was not found.");
            if (requireApproved && activity.ReviewStatus != AdminReviewStatus.Approved)
                throw new ModuleValidationException(
                    $"Exercise '{activity.Title}' is not approved yet — approve it before generating a Module from it.");

            var link = new ModuleExerciseLink(
                moduleId, input.ExerciseId, role, sortOrder++, input.Required, activity.Title);
            exerciseLinks.Add(link);
            db.ModuleExerciseLinks.Add(link);
        }

        return (lessonLinks, exerciseLinks);
    }
}
