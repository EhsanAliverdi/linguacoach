namespace LinguaCoach.Application.ContentSeeding;

/// <summary>
/// Adaptive Curriculum Sprint 6 — bulk content seeding. Closes the "0 approved Modules" gap every
/// prior sprint's review flagged as blocking live verification, by chaining the existing
/// single-item generation handlers (never reinventing generation logic) across many unconsumed
/// Resource Bank items in one bounded admin action: generate a Lesson, generate its Exercises
/// (which auto-creates/links a Module — see <c>IGenerateActivitiesFromLessonHandler</c>), approve
/// all three, then AI-tag the Module against real skill-graph nodes
/// (<c>IModuleSkillGraphTaggingService</c>, unchanged from Sprint 2). Continue-on-error per
/// resource — a bulk sweep should report partial failures, not abort the whole run.
/// </summary>
public interface IContentSeedingService
{
    Task<ContentSeedingResult> RunAsync(ContentSeedingRequest request, CancellationToken ct = default);
}

/// <summary>Scoped to Vocabulary/Grammar resources only this sprint — the only two resource types
/// with a fully deterministic Lesson+Exercise composer (gap_fill), keeping bulk-generated content
/// free of AI-hallucination risk. Reading/Listening/Speaking/Writing bulk seeding is left for a
/// later pass; see the Sprint 6 review doc.</summary>
public sealed record ContentSeedingRequest(
    /// <summary>Null = every CEFR level in <c>CefrLevelConstants.All</c>.</summary>
    IReadOnlyList<string>? CefrLevels = null,
    /// <summary>Resources to consume per (CEFR level × resource type) in this call.</summary>
    int MaxResourcesPerCefrLevelPerType = 3,
    int ExercisesPerLesson = 2,
    Guid? CreatedByUserId = null);

public sealed record ContentSeedingItemResult(
    string ResourceType,
    Guid ResourceId,
    string CefrLevel,
    bool Success,
    Guid? ModuleId = null,
    int NodesLinked = 0,
    string? ErrorMessage = null);

public sealed record ContentSeedingResult(
    int ResourcesConsidered,
    int ModulesCreatedAndApproved,
    int NodeLinksCreated,
    IReadOnlyList<ContentSeedingItemResult> Items);
