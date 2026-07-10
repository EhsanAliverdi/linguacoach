using System.Text.Json;
using LinguaCoach.Application.Modules;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Modules;

/// <summary>
/// Phase H5 — deterministic "Generate Module" composer, for all four entry points
/// (<see cref="IGenerateModuleFromItemsHandler"/>/<see cref="IGenerateModuleFromResourceHandler"/>/
/// <see cref="IGenerateModuleFromLessonHandler"/>/<see cref="IGenerateModuleFromExerciseHandler"/>).
/// Composes a pending-review <see cref="Module"/> from EXISTING Lessons and
/// Exercises — never cascade-generates new ones, never calls an AI provider (same reasoning as
/// H3/H4: no existing AI service in this codebase composes a lesson plan from other content).
/// Every entry point only considers <see cref="AdminReviewStatus.Approved"/> sources — a
/// draft/pending Lesson or Exercise is never silently pulled into a generated Module.
/// </summary>
public sealed class ModuleGenerationService :
    IGenerateModuleFromItemsHandler, IGenerateModuleFromResourceHandler,
    IGenerateModuleFromLessonHandler, IGenerateModuleFromExerciseHandler
{
    private const string GenerationProvider = "Deterministic";
    private const string GenerationModel = "module-draft-composer-v1";
    private const int MaxCompatibleMatches = 5;

    private readonly LinguaCoachDbContext _db;

    public ModuleGenerationService(LinguaCoachDbContext db) => _db = db;

    public async Task<GenerateModuleResult> HandleAsync(
        GenerateModuleFromItemsRequest request, CancellationToken ct = default)
    {
        if (request.LessonLinks is not { Count: > 0 })
            throw new ModuleValidationException("At least one approved Lesson is required to generate a Module.");
        if (request.ExerciseLinks is not { Count: > 0 })
            throw new ModuleValidationException("At least one approved Exercise is required to generate a Module.");

        var lessons = new List<Lesson>();
        foreach (var input in request.LessonLinks)
            lessons.Add(await RequireApprovedLessonAsync(input.LessonId, ct));

        var activities = new List<Exercise>();
        foreach (var input in request.ExerciseLinks)
            activities.Add(await RequireApprovedExerciseAsync(input.ExerciseId, ct));

        return await ComposeAndSaveAsync(
            lessons, activities, request.LessonLinks, request.ExerciseLinks,
            request.Title, request.Notes, request.CreatedByUserId,
            ModuleSourceMode.GeneratedFromLessonAndExercises, ct);
    }

    public async Task<GenerateModuleResult> HandleAsync(
        GenerateModuleFromResourceRequest request, CancellationToken ct = default)
    {
        if (!LessonResourceLookup.TryParseResourceType(request.ResourceType, out var resourceType))
            throw new ModuleValidationException($"Unsupported resource type '{request.ResourceType}'.");

        var lesson = await _db.LessonResourceLinks
            .Where(l => l.ResourceType == resourceType && l.ResourceId == request.ResourceId)
            .Join(_db.Lessons.Where(i => i.ReviewStatus == Domain.Enums.AdminReviewStatus.Approved),
                l => l.LessonId, i => i.Id, (l, i) => i)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new ModuleValidationException(
                "No approved Lesson is linked to this resource yet — generate and approve a Lesson first.");

        var activity = await _db.ExerciseResourceLinks
            .Where(l => l.ResourceType == resourceType && l.ResourceId == request.ResourceId)
            .Join(_db.Exercises.Where(a => a.ReviewStatus == Domain.Enums.AdminReviewStatus.Approved),
                l => l.ExerciseId, a => a.Id, (l, a) => a)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new ModuleValidationException(
                "No approved Exercise is linked to this resource yet — generate and approve an Activity first.");

        var lessonLinks = new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") };
        var exerciseLinks = new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") };

        return await ComposeAndSaveAsync(
            new List<Lesson> { lesson }, new List<Exercise> { activity }, lessonLinks, exerciseLinks,
            request.Title, request.Notes, request.CreatedByUserId, ModuleSourceMode.GeneratedFromResources, ct);
    }

    public async Task<GenerateModuleResult> HandleAsync(
        GenerateModuleFromLessonRequest request, CancellationToken ct = default)
    {
        var lesson = await RequireApprovedLessonAsync(request.LessonId, ct);

        var candidates = _db.Exercises.Where(a => a.ReviewStatus == Domain.Enums.AdminReviewStatus.Approved);
        if (!string.IsNullOrWhiteSpace(lesson.CefrLevel))
            candidates = candidates.Where(a => a.CefrLevel == lesson.CefrLevel);
        if (!string.IsNullOrWhiteSpace(lesson.Skill))
            candidates = candidates.Where(a => a.Skill == lesson.Skill);

        var activities = await candidates.OrderByDescending(a => a.CreatedAt).Take(MaxCompatibleMatches).ToListAsync(ct);
        if (activities.Count == 0)
            throw new ModuleValidationException(
                "No compatible approved Exercise was found for this Lesson — generate or approve an Activity first.");

        var lessonLinks = new[] { new ModuleLessonLinkInput(lesson.Id, "Primary") };
        var exerciseLinks = activities
            .Select((a, i) => new ModuleExerciseLinkInput(a.Id, i == 0 ? "PrimaryPractice" : "SupportingPractice"))
            .ToList();

        return await ComposeAndSaveAsync(
            new List<Lesson> { lesson }, activities, lessonLinks, exerciseLinks,
            request.Title, request.Notes, request.CreatedByUserId, ModuleSourceMode.GeneratedFromLessonAndExercises, ct);
    }

    public async Task<GenerateModuleResult> HandleAsync(
        GenerateModuleFromExerciseRequest request, CancellationToken ct = default)
    {
        var activity = await RequireApprovedExerciseAsync(request.ExerciseId, ct);

        var candidates = _db.Lessons.Where(i => i.ReviewStatus == Domain.Enums.AdminReviewStatus.Approved);
        if (!string.IsNullOrWhiteSpace(activity.CefrLevel))
            candidates = candidates.Where(i => i.CefrLevel == activity.CefrLevel);
        if (!string.IsNullOrWhiteSpace(activity.Skill))
            candidates = candidates.Where(i => i.Skill == activity.Skill);

        var lessons = await candidates.OrderByDescending(i => i.CreatedAt).Take(MaxCompatibleMatches).ToListAsync(ct);
        if (lessons.Count == 0)
            throw new ModuleValidationException(
                "No compatible approved Lesson was found for this Exercise — generate or approve a Lesson first.");

        var lessonLinks = lessons
            .Select((i, idx) => new ModuleLessonLinkInput(i.Id, idx == 0 ? "Primary" : "Supporting"))
            .ToList();
        var exerciseLinks = new[] { new ModuleExerciseLinkInput(activity.Id, "PrimaryPractice") };

        return await ComposeAndSaveAsync(
            lessons, new List<Exercise> { activity }, lessonLinks, exerciseLinks,
            request.Title, request.Notes, request.CreatedByUserId, ModuleSourceMode.GeneratedFromLessonAndExercises, ct);
    }

    private async Task<Lesson> RequireApprovedLessonAsync(Guid lessonId, CancellationToken ct)
    {
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == lessonId, ct)
            ?? throw new ModuleValidationException($"Lesson '{lessonId}' was not found.");
        if (lesson.ReviewStatus != Domain.Enums.AdminReviewStatus.Approved)
            throw new ModuleValidationException(
                $"Lesson '{lesson.Title}' is not approved yet — approve it before generating a Module from it.");
        return lesson;
    }

    private async Task<Exercise> RequireApprovedExerciseAsync(Guid activityId, CancellationToken ct)
    {
        var activity = await _db.Exercises.FirstOrDefaultAsync(a => a.Id == activityId, ct)
            ?? throw new ModuleValidationException($"Exercise '{activityId}' was not found.");
        if (activity.ReviewStatus != Domain.Enums.AdminReviewStatus.Approved)
            throw new ModuleValidationException(
                $"Exercise '{activity.Title}' is not approved yet — approve it before generating a Module from it.");
        return activity;
    }

    private async Task<GenerateModuleResult> ComposeAndSaveAsync(
        List<Lesson> lessons,
        List<Exercise> activities,
        IReadOnlyList<ModuleLessonLinkInput> lessonLinkInputs,
        IReadOnlyList<ModuleExerciseLinkInput> exerciseLinkInputs,
        string? title,
        string? notes,
        Guid? createdByUserId,
        ModuleSourceMode sourceMode,
        CancellationToken ct)
    {
        var primaryLesson = lessons[0];
        var primaryActivity = activities[0];

        var resolvedTitle = !string.IsNullOrWhiteSpace(title) ? title!.Trim() : primaryLesson.Title;
        var cefrLevel = primaryLesson.CefrLevel ?? primaryActivity.CefrLevel;
        var skill = primaryLesson.Skill ?? primaryActivity.Skill;
        var subskill = primaryLesson.Subskill ?? primaryActivity.Subskill;
        var contextTags = MergeTagArrays(lessons.Select(l => l.ContextTagsJson).Concat(activities.Select(a => a.ContextTagsJson)));
        var focusTags = MergeTagArrays(lessons.Select(l => l.FocusTagsJson).Concat(activities.Select(a => a.FocusTagsJson)));
        var difficultyBand = primaryLesson.DifficultyBand ?? primaryActivity.DifficultyBand;
        var estimatedMinutes = activities.Any(a => a.EstimatedMinutes.HasValue)
            ? activities.Sum(a => a.EstimatedMinutes ?? 0)
            : (int?)null;

        var description = $"Deterministic module draft combining {lessons.Count} Lesson(s) and "
            + $"{activities.Count} Exercise(s): "
            + string.Join(", ", lessons.Select(l => l.Title).Concat(activities.Select(a => a.Title))) + "."
            + (notes is not null ? $" {notes.Trim()}" : string.Empty);

        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            completionMessage = "Great job completing this module!",
            note = "Deterministic module-level feedback plan — review before approval.",
        });

        Module module;
        try
        {
            module = new Module(
                resolvedTitle, sourceMode, description, objectiveKey: null,
                cefrLevel, skill, subskill,
                JsonSerializer.Serialize(contextTags), JsonSerializer.Serialize(focusTags),
                difficultyBand, estimatedMinutes, feedbackPlanJson,
                GenerationProvider, GenerationModel, createdByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ModuleValidationException(ex.Message);
        }

        _db.Modules.Add(module);
        await _db.SaveChangesAsync(ct);

        var (lessonLinks, exerciseLinks) = await ModuleLinkBuilder.BuildAndAddAsync(
            _db, module.Id, lessonLinkInputs, exerciseLinkInputs, requireApproved: true, ct);
        await _db.SaveChangesAsync(ct);

        var dto = ModuleMappers.ToDto(module, lessonLinks, exerciseLinks);
        return new GenerateModuleResult(dto, $"/admin/modules?id={module.Id}");
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
