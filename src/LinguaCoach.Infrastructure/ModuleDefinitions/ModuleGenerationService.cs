using System.Text.Json;
using LinguaCoach.Application.ModuleDefinitions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.LearnItems;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ModuleDefinitions;

/// <summary>
/// Phase H5 — deterministic "Generate Module" composer, for all four entry points
/// (<see cref="IGenerateModuleFromItemsHandler"/>/<see cref="IGenerateModuleFromResourceHandler"/>/
/// <see cref="IGenerateModuleFromLearnItemHandler"/>/<see cref="IGenerateModuleFromActivityHandler"/>).
/// Composes a pending-review <see cref="ModuleDefinition"/> from EXISTING Learn Items and Activity
/// Definitions — never cascade-generates new ones, never calls an AI provider (same reasoning as
/// H3/H4: no existing AI service in this codebase composes a lesson plan from other content).
/// Every entry point only considers <see cref="AdminReviewStatus.Approved"/> sources — a
/// draft/pending Learn Item or Activity is never silently pulled into a generated Module.
/// </summary>
public sealed class ModuleGenerationService :
    IGenerateModuleFromItemsHandler, IGenerateModuleFromResourceHandler,
    IGenerateModuleFromLearnItemHandler, IGenerateModuleFromActivityHandler
{
    private const string GenerationProvider = "Deterministic";
    private const string GenerationModel = "module-draft-composer-v1";
    private const int MaxCompatibleMatches = 5;

    private readonly LinguaCoachDbContext _db;

    public ModuleGenerationService(LinguaCoachDbContext db) => _db = db;

    public async Task<GenerateModuleDefinitionResult> HandleAsync(
        GenerateModuleFromItemsRequest request, CancellationToken ct = default)
    {
        if (request.LearnItemLinks is not { Count: > 0 })
            throw new ModuleDefinitionValidationException("At least one approved Learn Item is required to generate a Module.");
        if (request.ActivityLinks is not { Count: > 0 })
            throw new ModuleDefinitionValidationException("At least one approved Activity Definition is required to generate a Module.");

        var learnItems = new List<LearnItem>();
        foreach (var input in request.LearnItemLinks)
            learnItems.Add(await RequireApprovedLearnItemAsync(input.LearnItemId, ct));

        var activities = new List<ActivityDefinition>();
        foreach (var input in request.ActivityLinks)
            activities.Add(await RequireApprovedActivityAsync(input.ActivityDefinitionId, ct));

        return await ComposeAndSaveAsync(
            learnItems, activities, request.LearnItemLinks, request.ActivityLinks,
            request.Title, request.Notes, request.CreatedByUserId,
            ModuleSourceMode.GeneratedFromLearnAndActivities, ct);
    }

    public async Task<GenerateModuleDefinitionResult> HandleAsync(
        GenerateModuleFromResourceRequest request, CancellationToken ct = default)
    {
        if (!LearnItemResourceLookup.TryParseResourceType(request.ResourceType, out var resourceType))
            throw new ModuleDefinitionValidationException($"Unsupported resource type '{request.ResourceType}'.");

        var learnItem = await _db.LearnItemResourceLinks
            .Where(l => l.ResourceType == resourceType && l.ResourceId == request.ResourceId)
            .Join(_db.LearnItems.Where(i => i.ReviewStatus == Domain.Enums.AdminReviewStatus.Approved),
                l => l.LearnItemId, i => i.Id, (l, i) => i)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new ModuleDefinitionValidationException(
                "No approved Learn Item is linked to this resource yet — generate and approve a Learn Item first.");

        var activity = await _db.ActivityResourceLinks
            .Where(l => l.ResourceType == resourceType && l.ResourceId == request.ResourceId)
            .Join(_db.ActivityDefinitions.Where(a => a.ReviewStatus == Domain.Enums.AdminReviewStatus.Approved),
                l => l.ActivityDefinitionId, a => a.Id, (l, a) => a)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new ModuleDefinitionValidationException(
                "No approved Activity Definition is linked to this resource yet — generate and approve an Activity first.");

        var learnLinks = new[] { new ModuleLearnItemLinkInput(learnItem.Id, "Primary") };
        var activityLinks = new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") };

        return await ComposeAndSaveAsync(
            new List<LearnItem> { learnItem }, new List<ActivityDefinition> { activity }, learnLinks, activityLinks,
            request.Title, request.Notes, request.CreatedByUserId, ModuleSourceMode.GeneratedFromResources, ct);
    }

    public async Task<GenerateModuleDefinitionResult> HandleAsync(
        GenerateModuleFromLearnItemRequest request, CancellationToken ct = default)
    {
        var learnItem = await RequireApprovedLearnItemAsync(request.LearnItemId, ct);

        var candidates = _db.ActivityDefinitions.Where(a => a.ReviewStatus == Domain.Enums.AdminReviewStatus.Approved);
        if (!string.IsNullOrWhiteSpace(learnItem.CefrLevel))
            candidates = candidates.Where(a => a.CefrLevel == learnItem.CefrLevel);
        if (!string.IsNullOrWhiteSpace(learnItem.Skill))
            candidates = candidates.Where(a => a.Skill == learnItem.Skill);

        var activities = await candidates.OrderByDescending(a => a.CreatedAt).Take(MaxCompatibleMatches).ToListAsync(ct);
        if (activities.Count == 0)
            throw new ModuleDefinitionValidationException(
                "No compatible approved Activity Definition was found for this Learn Item — generate or approve an Activity first.");

        var learnLinks = new[] { new ModuleLearnItemLinkInput(learnItem.Id, "Primary") };
        var activityLinks = activities
            .Select((a, i) => new ModuleActivityLinkInput(a.Id, i == 0 ? "PrimaryPractice" : "SupportingPractice"))
            .ToList();

        return await ComposeAndSaveAsync(
            new List<LearnItem> { learnItem }, activities, learnLinks, activityLinks,
            request.Title, request.Notes, request.CreatedByUserId, ModuleSourceMode.GeneratedFromLearnAndActivities, ct);
    }

    public async Task<GenerateModuleDefinitionResult> HandleAsync(
        GenerateModuleFromActivityRequest request, CancellationToken ct = default)
    {
        var activity = await RequireApprovedActivityAsync(request.ActivityDefinitionId, ct);

        var candidates = _db.LearnItems.Where(i => i.ReviewStatus == Domain.Enums.AdminReviewStatus.Approved);
        if (!string.IsNullOrWhiteSpace(activity.CefrLevel))
            candidates = candidates.Where(i => i.CefrLevel == activity.CefrLevel);
        if (!string.IsNullOrWhiteSpace(activity.Skill))
            candidates = candidates.Where(i => i.Skill == activity.Skill);

        var learnItems = await candidates.OrderByDescending(i => i.CreatedAt).Take(MaxCompatibleMatches).ToListAsync(ct);
        if (learnItems.Count == 0)
            throw new ModuleDefinitionValidationException(
                "No compatible approved Learn Item was found for this Activity Definition — generate or approve a Learn Item first.");

        var learnLinks = learnItems
            .Select((i, idx) => new ModuleLearnItemLinkInput(i.Id, idx == 0 ? "Primary" : "Supporting"))
            .ToList();
        var activityLinks = new[] { new ModuleActivityLinkInput(activity.Id, "PrimaryPractice") };

        return await ComposeAndSaveAsync(
            learnItems, new List<ActivityDefinition> { activity }, learnLinks, activityLinks,
            request.Title, request.Notes, request.CreatedByUserId, ModuleSourceMode.GeneratedFromLearnAndActivities, ct);
    }

    private async Task<LearnItem> RequireApprovedLearnItemAsync(Guid learnItemId, CancellationToken ct)
    {
        var learnItem = await _db.LearnItems.FirstOrDefaultAsync(l => l.Id == learnItemId, ct)
            ?? throw new ModuleDefinitionValidationException($"Learn Item '{learnItemId}' was not found.");
        if (learnItem.ReviewStatus != Domain.Enums.AdminReviewStatus.Approved)
            throw new ModuleDefinitionValidationException(
                $"Learn Item '{learnItem.Title}' is not approved yet — approve it before generating a Module from it.");
        return learnItem;
    }

    private async Task<ActivityDefinition> RequireApprovedActivityAsync(Guid activityId, CancellationToken ct)
    {
        var activity = await _db.ActivityDefinitions.FirstOrDefaultAsync(a => a.Id == activityId, ct)
            ?? throw new ModuleDefinitionValidationException($"Activity Definition '{activityId}' was not found.");
        if (activity.ReviewStatus != Domain.Enums.AdminReviewStatus.Approved)
            throw new ModuleDefinitionValidationException(
                $"Activity Definition '{activity.Title}' is not approved yet — approve it before generating a Module from it.");
        return activity;
    }

    private async Task<GenerateModuleDefinitionResult> ComposeAndSaveAsync(
        List<LearnItem> learnItems,
        List<ActivityDefinition> activities,
        IReadOnlyList<ModuleLearnItemLinkInput> learnItemLinkInputs,
        IReadOnlyList<ModuleActivityLinkInput> activityLinkInputs,
        string? title,
        string? notes,
        Guid? createdByUserId,
        ModuleSourceMode sourceMode,
        CancellationToken ct)
    {
        var primaryLearnItem = learnItems[0];
        var primaryActivity = activities[0];

        var resolvedTitle = !string.IsNullOrWhiteSpace(title) ? title!.Trim() : primaryLearnItem.Title;
        var cefrLevel = primaryLearnItem.CefrLevel ?? primaryActivity.CefrLevel;
        var skill = primaryLearnItem.Skill ?? primaryActivity.Skill;
        var subskill = primaryLearnItem.Subskill ?? primaryActivity.Subskill;
        var contextTags = MergeTagArrays(learnItems.Select(l => l.ContextTagsJson).Concat(activities.Select(a => a.ContextTagsJson)));
        var focusTags = MergeTagArrays(learnItems.Select(l => l.FocusTagsJson).Concat(activities.Select(a => a.FocusTagsJson)));
        var difficultyBand = primaryLearnItem.DifficultyBand ?? primaryActivity.DifficultyBand;
        var estimatedMinutes = activities.Any(a => a.EstimatedMinutes.HasValue)
            ? activities.Sum(a => a.EstimatedMinutes ?? 0)
            : (int?)null;

        var description = $"Deterministic module draft combining {learnItems.Count} Learn Item(s) and "
            + $"{activities.Count} Activity Definition(s): "
            + string.Join(", ", learnItems.Select(l => l.Title).Concat(activities.Select(a => a.Title))) + "."
            + (notes is not null ? $" {notes.Trim()}" : string.Empty);

        var feedbackPlanJson = JsonSerializer.Serialize(new
        {
            completionMessage = "Great job completing this module!",
            note = "Deterministic module-level feedback plan — review before approval.",
        });

        ModuleDefinition module;
        try
        {
            module = new ModuleDefinition(
                resolvedTitle, sourceMode, description, objectiveKey: null,
                cefrLevel, skill, subskill,
                JsonSerializer.Serialize(contextTags), JsonSerializer.Serialize(focusTags),
                difficultyBand, estimatedMinutes, feedbackPlanJson,
                GenerationProvider, GenerationModel, createdByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ModuleDefinitionValidationException(ex.Message);
        }

        _db.ModuleDefinitions.Add(module);
        await _db.SaveChangesAsync(ct);

        var (learnItemLinks, activityLinks) = await ModuleLinkBuilder.BuildAndAddAsync(
            _db, module.Id, learnItemLinkInputs, activityLinkInputs, requireApproved: true, ct);
        await _db.SaveChangesAsync(ct);

        var dto = ModuleDefinitionMappers.ToDto(module, learnItemLinks, activityLinks);
        return new GenerateModuleDefinitionResult(dto, $"/admin/modules?id={module.Id}");
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
