using LinguaCoach.Application.ContentSeeding;
using LinguaCoach.Application.Exercises;
using LinguaCoach.Application.Lessons;
using LinguaCoach.Application.SkillGraph;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.ContentSeeding;

/// <summary>See <see cref="IContentSeedingService"/> for the full rationale.</summary>
public sealed class ContentSeedingService : IContentSeedingService
{
    // Judgment call — a conservative per-call ceiling so one admin action can't kick off an
    // unbounded synchronous generation run, mirroring ResourceCandidateBatchAnalysisService/
    // LessonExerciseBatchGenerationService's MaxTotalPerCall discipline.
    private const int MaxResourcesPerCall = 60;
    private const int MaxCandidateNodes = 60;

    // Vocabulary/Grammar are the only two resource types with a fully deterministic gap_fill
    // composer (ExerciseGenerationService's Vocabulary/Grammar supported-type bucket) — kept
    // AI-free for bulk generation to avoid hallucination risk at scale. See the Sprint 6 review.
    private static readonly (PublishedResourceType Type, string TypeName)[] SeedableResourceTypes =
    [
        (PublishedResourceType.Vocabulary, "Vocabulary"),
        (PublishedResourceType.Grammar, "Grammar")
    ];

    private const string GapFillActivityType = "gap_fill";

    private readonly LinguaCoachDbContext _db;
    private readonly IGenerateLessonFromResourcesHandler _lessonHandler;
    private readonly IGenerateActivitiesFromLessonHandler _activitiesHandler;
    private readonly IModuleSkillGraphTaggingService _tagging;
    private readonly ILogger<ContentSeedingService> _logger;

    public ContentSeedingService(
        LinguaCoachDbContext db,
        IGenerateLessonFromResourcesHandler lessonHandler,
        IGenerateActivitiesFromLessonHandler activitiesHandler,
        IModuleSkillGraphTaggingService tagging,
        ILogger<ContentSeedingService> logger)
    {
        _db = db;
        _lessonHandler = lessonHandler;
        _activitiesHandler = activitiesHandler;
        _tagging = tagging;
        _logger = logger;
    }

    public async Task<ContentSeedingResult> RunAsync(ContentSeedingRequest request, CancellationToken ct = default)
    {
        var cefrLevels = (request.CefrLevels is { Count: > 0 } ? request.CefrLevels : CefrLevelConstants.All)
            .Where(CefrLevelConstants.IsValid)
            .ToList();
        var perGroup = Math.Max(1, request.MaxResourcesPerCefrLevelPerType);
        var exercisesPerLesson = Math.Max(1, request.ExercisesPerLesson);

        var items = new List<ContentSeedingItemResult>();

        foreach (var cefrLevel in cefrLevels)
        {
            foreach (var (resourceType, typeName) in SeedableResourceTypes)
            {
                if (items.Count >= MaxResourcesPerCall) break;

                var remaining = MaxResourcesPerCall - items.Count;
                var take = Math.Min(perGroup, remaining);

                var candidateResourceIds = await _db.ResourceBankItems
                    .AsNoTracking()
                    .Where(r => r.Type == resourceType && r.CefrLevel == cefrLevel && !r.IsArchived)
                    .Where(r => !_db.LessonResourceLinks.Any(l => l.ResourceType == resourceType && l.ResourceId == r.Id))
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => r.Id)
                    .Take(take)
                    .ToListAsync(ct);

                foreach (var resourceId in candidateResourceIds)
                {
                    var item = await SeedOneAsync(resourceType, typeName, resourceId, cefrLevel, exercisesPerLesson, request.CreatedByUserId, ct);
                    items.Add(item);
                }
            }
        }

        return new ContentSeedingResult(
            ResourcesConsidered: items.Count,
            ModulesCreatedAndApproved: items.Count(i => i.Success),
            NodeLinksCreated: items.Sum(i => i.NodesLinked),
            Items: items);
    }

    private async Task<ContentSeedingItemResult> SeedOneAsync(
        PublishedResourceType resourceType, string typeName, Guid resourceId, string cefrLevel,
        int exercisesPerLesson, Guid? createdByUserId, CancellationToken ct)
    {
        try
        {
            var lessonResult = await _lessonHandler.HandleAsync(new GenerateLessonFromResourcesRequest(
                Resources: [new LessonResourceLinkInput(typeName, resourceId, "Primary")],
                Notes: "Adaptive Curriculum Sprint 6 — bulk-seeded content.",
                CreatedByUserId: createdByUserId), ct);
            var lessonId = lessonResult.Lesson.Id;

            var activitiesResult = await _activitiesHandler.HandleAsync(new GenerateActivitiesFromLessonRequest(
                LessonId: lessonId,
                Specs: [new ActivityGenerationSpec(GapFillActivityType, exercisesPerLesson)],
                Notes: "Adaptive Curriculum Sprint 6 — bulk-seeded content.",
                CreatedByUserId: createdByUserId), ct);
            var moduleId = activitiesResult.ModuleId;

            var lesson = await _db.Lessons.FirstAsync(l => l.Id == lessonId, ct);
            lesson.Approve(createdByUserId, "Bulk-seeded (Sprint 6) — deterministic composer, auto-approved.");

            foreach (var activity in activitiesResult.Activities)
            {
                var exercise = await _db.Exercises.FirstAsync(a => a.Id == activity.Id, ct);
                exercise.Approve(createdByUserId, "Bulk-seeded (Sprint 6) — deterministic composer, auto-approved.");
            }

            var module = await _db.Modules.FirstAsync(m => m.Id == moduleId, ct);
            module.Approve(createdByUserId, "Bulk-seeded (Sprint 6) — deterministic composer, auto-approved.");

            await _db.SaveChangesAsync(ct);

            var nodesLinked = await TagModuleAsync(module, ct);

            return new ContentSeedingItemResult(typeName, resourceId, cefrLevel, Success: true, ModuleId: moduleId, NodesLinked: nodesLinked);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ContentSeedingService: failed to seed from resource {ResourceType}:{ResourceId} (CEFR {CefrLevel}).",
                typeName, resourceId, cefrLevel);
            return new ContentSeedingItemResult(typeName, resourceId, cefrLevel, Success: false, ErrorMessage: ex.Message);
        }
    }

    /// <summary>Adaptive Curriculum Sprint 2's auto-apply convention, unchanged: every proposed
    /// node key is validated against the real candidate list before a <c>ModuleSkillGraphNodeLink</c>
    /// row is ever created.</summary>
    private async Task<int> TagModuleAsync(Module module, CancellationToken ct)
    {
        if (module.CefrLevel is null || module.Skill is null)
            return 0;

        // SkillGraphNode.Skill is stored lower-invariant (its constructor normalizes it);
        // Module.Skill preserves whatever casing the caller passed (e.g. "Vocabulary" from
        // LessonResourceLookup's title-case resource-type mapping) — normalize before comparing.
        var moduleSkillLower = module.Skill.ToLowerInvariant();
        var candidateNodes = await _db.SkillGraphNodes.AsNoTracking()
            .Where(n => n.ReviewStatus == AdminReviewStatus.Approved && n.IsActive
                && n.CefrLevel == module.CefrLevel && n.Skill == moduleSkillLower)
            .OrderBy(n => n.Key)
            .Take(MaxCandidateNodes)
            .Select(n => new SkillGraphNodeCandidate(n.Id, n.Key, n.Title))
            .ToListAsync(ct);

        if (candidateNodes.Count == 0)
            return 0;

        var taggingResult = await _tagging.ProposeCoverageAsync(new ModuleSkillGraphTaggingRequest(
            module.Id, module.Title, module.Description ?? module.Title, module.CefrLevel, module.Skill, candidateNodes), ct);

        if (!taggingResult.Success || taggingResult.Matches.Count == 0)
            return 0;

        var alreadyLinkedNodeIds = await _db.ModuleSkillGraphNodeLinks.AsNoTracking()
            .Where(l => l.ModuleId == module.Id)
            .Select(l => l.SkillGraphNodeId)
            .ToListAsync(ct);

        var newLinks = taggingResult.Matches
            .Where(m => !alreadyLinkedNodeIds.Contains(m.NodeId))
            .Select(m => new ModuleSkillGraphNodeLink(module.Id, m.NodeId, m.Confidence))
            .ToList();

        if (newLinks.Count == 0)
            return 0;

        _db.ModuleSkillGraphNodeLinks.AddRange(newLinks);
        await _db.SaveChangesAsync(ct);

        return newLinks.Count;
    }
}
