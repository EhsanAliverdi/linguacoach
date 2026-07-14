using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.Modules;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Exercises;

/// <summary>
/// Phase K5 — see <see cref="IGenerateActivitiesFromLessonHandler"/>'s doc comment. Loops the
/// existing single-item handlers once per requested Exercise (continue-on-error is deliberately
/// NOT used here — an admin who asked for 10 wants to know immediately if generation fails
/// partway through, not silently get 6), then hands the full set of this Lesson's Exercise ids
/// to <see cref="IModuleAutoLinkService"/> to create-or-extend the Module.
///
/// Phase K12 — the whole batch (every generated Exercise plus the Module auto-link) now runs
/// inside one DB transaction. Each single-item generation call SaveChanges-es immediately, so
/// without a wrapping transaction a failure partway through (e.g. requesting a type unsupported
/// by this Lesson's linked resource) used to leave the Exercises created *before* the failure
/// permanently committed — orphaned, un-linked to any Module, invisible to the admin. Now a
/// failure rolls back everything from this call, matching the "admin wants to know immediately,
/// not silently get a partial result" intent the fail-fast design already stated.
///
/// Phase K14 — "multiple_choice_single" is routed to the AI-assisted resources-based handler
/// instead of the deterministic Lesson-based one. The deterministic composer's distractors are
/// pulled from other same-CEFR-level Resource Bank rows purely by creation order — with no
/// subskill/category tagging on the data (confirmed empty for the whole Vocabulary bank), that
/// produces semantically random, often nonsensical wrong answers (e.g. "zoo"/"yourself"/"yours"
/// as distractors for "yellow"). AI can actually reason about a plausible-but-wrong meaning; the
/// correct answer itself is still always the resource's own real definition, verbatim — AI only
/// ever supplies the distractors, matching the existing project principle that AI is never
/// trusted to decide which option is correct (see AiExerciseGenerationService's doc comment).
/// "gap_fill" and "short_answer" stay deterministic — neither has this distractor problem.
///
/// Phase K17 — "reading_multiple_choice_single" is also routed here, but for a different reason
/// than multiple_choice_single: it has no deterministic composer at all (no fact field on a
/// ReadingReference/ReadingPassage row to derive a correct answer from — see
/// ActivityGenerationService.ActivityTypeReadingMultipleChoiceSingle's doc comment).
/// </summary>
public sealed class LessonExerciseBatchGenerationService : IGenerateActivitiesFromLessonHandler
{
    // Judgment call — a conservative per-call ceiling so one admin action can't kick off an
    // unbounded synchronous generation run. Mirrors ResourceCandidateBatchAnalysisService's
    // MaxCandidatesPerBatch discipline.
    public const int MaxTotalPerCall = 50;

    /// <summary>Activity types with no deterministic composer — always routed through the
    /// AI-assisted resources handler. multiple_choice_single (K14) still has a deterministic
    /// composer too but is routed here for distractor quality; reading_multiple_choice_single
    /// (K17) has no deterministic composer at all (see its doc comment).</summary>
    private static readonly HashSet<string> AiOnlyOrAiPreferredTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ActivityGenerationService.ActivityTypeMultipleChoiceSingle,
        ActivityGenerationService.ActivityTypeReadingMultipleChoiceSingle,
        ActivityGenerationService.ActivityTypeReadingMultipleChoiceMulti,
        ActivityGenerationService.ActivityTypeListeningMultipleChoiceSingle,
        ActivityGenerationService.ActivityTypeListeningMultipleChoiceMulti,
    };

    private readonly IGenerateActivityFromLessonHandler _singleHandler;
    private readonly IGenerateActivityFromResourcesWithAiHandler _aiResourcesHandler;
    private readonly IModuleAutoLinkService _moduleAutoLink;
    private readonly LinguaCoachDbContext _db;

    public LessonExerciseBatchGenerationService(
        IGenerateActivityFromLessonHandler singleHandler,
        IGenerateActivityFromResourcesWithAiHandler aiResourcesHandler,
        IModuleAutoLinkService moduleAutoLink,
        LinguaCoachDbContext db)
    {
        _singleHandler = singleHandler;
        _aiResourcesHandler = aiResourcesHandler;
        _moduleAutoLink = moduleAutoLink;
        _db = db;
    }

    public async Task<GenerateActivitiesFromLessonResult> HandleAsync(
        GenerateActivitiesFromLessonRequest request, CancellationToken ct = default)
    {
        var specs = (request.Specs ?? Array.Empty<ActivityGenerationSpec>()).Where(s => s.Count > 0).ToList();
        if (specs.Count == 0)
            throw new ExerciseValidationException("At least one Exercise type with a count greater than 0 is required.");

        var totalRequested = specs.Sum(s => s.Count);
        if (totalRequested > MaxTotalPerCall)
            throw new ExerciseValidationException(
                $"Cannot generate more than {MaxTotalPerCall} Exercises in one call — requested {totalRequested}.");

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var created = new List<ExerciseDto>();
        foreach (var spec in specs)
        {
            for (var i = 0; i < spec.Count; i++)
            {
                var title = string.IsNullOrWhiteSpace(request.TitlePrefix)
                    ? null
                    : $"{request.TitlePrefix} ({created.Count + 1})";

                ExerciseDto activity;
                if (spec.ActivityType is not null && AiOnlyOrAiPreferredTypes.Contains(spec.ActivityType))
                {
                    var resourcesRequest = await BuildResourcesRequestAsync(request.LessonId, spec.ActivityType, title, request.Notes, request.CreatedByUserId, ct);
                    var result = await _aiResourcesHandler.HandleAsync(resourcesRequest, ct);
                    activity = result.Activity;
                }
                else
                {
                    var result = await _singleHandler.HandleAsync(new GenerateActivityFromLessonRequest(
                        request.LessonId, spec.ActivityType, title, request.Notes, request.CreatedByUserId), ct);
                    activity = result.Activity;
                }

                created.Add(activity);
            }
        }

        var moduleId = await _moduleAutoLink.EnsureLinkedAsync(
            request.LessonId, created.Select(a => a.Id).ToList(), request.CreatedByUserId, ct);

        await transaction.CommitAsync(ct);

        return new GenerateActivitiesFromLessonResult(created, moduleId, $"/admin/modules/{moduleId}");
    }

    /// <summary>Mirrors exactly what <see cref="ActivityGenerationService"/>'s own "from Lesson"
    /// handler does to resolve a Lesson's linked resources — reused here so the AI resources-based
    /// handler receives the same resource/role set and the same Lesson-derived defaults
    /// (CefrLevel/Skill/Subskill/tags/difficulty) a deterministic call would have used.</summary>
    private async Task<GenerateActivityFromResourcesRequest> BuildResourcesRequestAsync(
        Guid lessonId, string? activityType, string? title, string? notes, Guid? createdByUserId, CancellationToken ct)
    {
        var lesson = await _db.Lessons.FirstOrDefaultAsync(l => l.Id == lessonId, ct)
            ?? throw new ExerciseValidationException($"Lesson '{lessonId}' was not found.");

        var lessonLinks = await _db.LessonResourceLinks.Where(l => l.LessonId == lesson.Id).ToListAsync(ct);
        if (lessonLinks.Count == 0)
            throw new ExerciseValidationException("This Lesson has no linked resources to generate an Activity from.");

        var resources = lessonLinks
            .Select(link => new ExerciseResourceLinkInput(link.ResourceType.ToString(), link.ResourceId, link.Role.ToString()))
            .ToList();

        return new GenerateActivityFromResourcesRequest(
            resources, activityType, title ?? lesson.Title,
            lesson.CefrLevel, lesson.Skill, lesson.Subskill,
            ParseTags(lesson.ContextTagsJson), ParseTags(lesson.FocusTagsJson), lesson.DifficultyBand,
            notes, createdByUserId);
    }

    private static List<string> ParseTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (System.Text.Json.JsonException)
        {
            return new List<string>();
        }
    }
}
