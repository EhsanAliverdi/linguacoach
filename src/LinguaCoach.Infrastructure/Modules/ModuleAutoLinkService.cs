using LinguaCoach.Application.Modules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Modules;

/// <summary>Phase K5 — see <see cref="IModuleAutoLinkService"/>'s doc comment for the full
/// rationale. Create-or-append: exactly one Module ever links a given Lesson through this path
/// (the first Module found via <see cref="Domain.Entities.ModuleLessonLink"/> is treated as "the"
/// Module for that Lesson) — repeated calls keep extending it with newly generated Exercises
/// instead of spawning a new Module every time.</summary>
public sealed class ModuleAutoLinkService : IModuleAutoLinkService
{
    private readonly LinguaCoachDbContext _db;

    public ModuleAutoLinkService(LinguaCoachDbContext db) => _db = db;

    public async Task<Guid> EnsureLinkedAsync(
        Guid lessonId, IReadOnlyList<Guid> exerciseIds, Guid? createdByUserId, CancellationToken ct = default)
    {
        if (exerciseIds is not { Count: > 0 })
            throw new ModuleValidationException("At least one Exercise is required to link a Module.");

        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == lessonId, ct)
            ?? throw new ModuleValidationException($"Lesson '{lessonId}' was not found.");

        var existingModuleId = await _db.ModuleLessonLinks
            .Where(l => l.LessonId == lessonId)
            .Select(l => (Guid?)l.ModuleId)
            .FirstOrDefaultAsync(ct);

        if (existingModuleId is { } moduleId)
        {
            var alreadyLinked = await _db.ModuleExerciseLinks
                .Where(l => l.ModuleId == moduleId)
                .Select(l => l.ExerciseId)
                .ToListAsync(ct);
            var newExerciseIds = exerciseIds.Except(alreadyLinked).Distinct().ToList();
            if (newExerciseIds.Count == 0)
                return moduleId;

            var nextSortOrder = (await _db.ModuleExerciseLinks
                .Where(l => l.ModuleId == moduleId)
                .Select(l => (int?)l.SortOrder)
                .MaxAsync(ct) ?? -1) + 1;

            foreach (var exerciseId in newExerciseIds)
            {
                var title = await _db.Exercises.Where(a => a.Id == exerciseId).Select(a => a.Title).FirstAsync(ct);
                _db.ModuleExerciseLinks.Add(new ModuleExerciseLink(
                    moduleId, exerciseId, ModuleExerciseRole.PrimaryPractice, nextSortOrder++, required: true, title));
            }
            await _db.SaveChangesAsync(ct);
            return moduleId;
        }

        var module = new Module(
            lesson.Title, ModuleSourceMode.GeneratedFromLessonAndExercises,
            description: null, lesson.CefrLevel, lesson.Skill, lesson.Subskill,
            lesson.ContextTagsJson, lesson.FocusTagsJson, lesson.DifficultyBand, estimatedMinutes: null,
            feedbackPlanJson: null, generationProvider: "Deterministic", generationModel: "auto-link-v1",
            createdByUserId: createdByUserId);
        _db.Modules.Add(module);
        await _db.SaveChangesAsync(ct);

        _db.ModuleLessonLinks.Add(new ModuleLessonLink(module.Id, lessonId, LessonResourceRole.Primary, 0, lesson.Title));

        var sortOrder = 0;
        foreach (var exerciseId in exerciseIds.Distinct())
        {
            var title = await _db.Exercises.Where(a => a.Id == exerciseId).Select(a => a.Title).FirstAsync(ct);
            _db.ModuleExerciseLinks.Add(new ModuleExerciseLink(
                module.Id, exerciseId, ModuleExerciseRole.PrimaryPractice, sortOrder++, required: true, title));
        }
        await _db.SaveChangesAsync(ct);

        return module.Id;
    }
}
