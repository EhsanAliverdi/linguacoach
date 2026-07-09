using System.Text.Json;
using LinguaCoach.Application.ActivityDefinitions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.LearnItems;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ActivityDefinitions;

public sealed class AdminCreateActivityDefinitionHandler : IAdminCreateActivityDefinitionHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminCreateActivityDefinitionHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ActivityDefinitionDto> HandleAsync(CreateActivityDefinitionCommand command, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ActivityRendererType>(command.RendererType, ignoreCase: true, out var rendererType))
            throw new ActivityDefinitionValidationException($"Unsupported renderer type '{command.RendererType}'.");

        if (command.LearnItemId.HasValue
            && !await _db.LearnItems.AnyAsync(l => l.Id == command.LearnItemId.Value, ct))
            throw new ActivityDefinitionValidationException($"Learn Item '{command.LearnItemId}' was not found.");

        ActivityDefinition item;
        try
        {
            item = new ActivityDefinition(
                command.Title, command.Instructions, command.ActivityType, rendererType, ActivitySourceMode.Manual,
                command.Description, command.PatternKey, command.FormSchemaJson, command.AnswerKeyJson,
                command.ScoringRulesJson, command.FeedbackPlanJson, command.CefrLevel, command.Skill, command.Subskill,
                ToJsonArray(command.ContextTags), ToJsonArray(command.FocusTags),
                command.DifficultyBand, command.EstimatedMinutes, command.LearnItemId,
                generationProvider: null, generationModel: null, createdByUserId: command.CreatedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ActivityDefinitionValidationException(ex.Message);
        }

        _db.ActivityDefinitions.Add(item);
        await _db.SaveChangesAsync(ct);

        var links = await ActivityLinkBuilder.BuildAndAddAsync(_db, item.Id, command.Links, ct);
        if (links.Count > 0)
            await _db.SaveChangesAsync(ct);

        return ActivityDefinitionMappers.ToDto(item, links);
    }

    private static string ToJsonArray(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : "[]";
}

public sealed class AdminUpdateActivityDefinitionHandler : IAdminUpdateActivityDefinitionHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminUpdateActivityDefinitionHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ActivityDefinitionDto> HandleAsync(UpdateActivityDefinitionCommand command, CancellationToken ct = default)
    {
        var item = await _db.ActivityDefinitions.FirstOrDefaultAsync(a => a.Id == command.Id, ct)
            ?? throw new ActivityDefinitionValidationException($"Activity '{command.Id}' was not found.");

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
            throw new ActivityDefinitionValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        var links = await _db.ActivityResourceLinks.Where(l => l.ActivityDefinitionId == item.Id).ToListAsync(ct);
        return ActivityDefinitionMappers.ToDto(item, links);
    }

    private static string ToJsonArray(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : "[]";
}

public sealed class AdminApproveActivityDefinitionHandler : IAdminApproveActivityDefinitionHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminApproveActivityDefinitionHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ActivityDefinitionDto> HandleAsync(ApproveActivityDefinitionCommand command, CancellationToken ct = default)
    {
        var item = await _db.ActivityDefinitions.FirstOrDefaultAsync(a => a.Id == command.Id, ct)
            ?? throw new ActivityDefinitionValidationException($"Activity '{command.Id}' was not found.");

        item.Approve(command.ReviewedByUserId, command.Notes);
        await _db.SaveChangesAsync(ct);

        var links = await _db.ActivityResourceLinks.Where(l => l.ActivityDefinitionId == item.Id).ToListAsync(ct);
        return ActivityDefinitionMappers.ToDto(item, links);
    }
}

public sealed class AdminRejectActivityDefinitionHandler : IAdminRejectActivityDefinitionHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminRejectActivityDefinitionHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ActivityDefinitionDto> HandleAsync(RejectActivityDefinitionCommand command, CancellationToken ct = default)
    {
        var item = await _db.ActivityDefinitions.FirstOrDefaultAsync(a => a.Id == command.Id, ct)
            ?? throw new ActivityDefinitionValidationException($"Activity '{command.Id}' was not found.");

        try
        {
            item.Reject(command.Reason, command.ReviewedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ActivityDefinitionValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        var links = await _db.ActivityResourceLinks.Where(l => l.ActivityDefinitionId == item.Id).ToListAsync(ct);
        return ActivityDefinitionMappers.ToDto(item, links);
    }
}

/// <summary>Shared link-creation logic for <see cref="AdminCreateActivityDefinitionHandler"/> and
/// <see cref="ActivityGenerationService"/>. Mirrors <c>LearnItemLinkBuilder</c>'s shape exactly,
/// reusing the same <see cref="LearnItemResourceLookup"/> helper (generic over any published
/// resource type, not Learn-Item-specific despite its namespace) so resource validation/lookup
/// logic exists in exactly one place for both Learn Items and Activities.</summary>
internal static class ActivityLinkBuilder
{
    public static async Task<List<ActivityResourceLink>> BuildAndAddAsync(
        LinguaCoachDbContext db, Guid activityDefinitionId, IReadOnlyList<ActivityResourceLinkInput>? inputs, CancellationToken ct)
    {
        var links = new List<ActivityResourceLink>();
        if (inputs is null) return links;

        foreach (var input in inputs)
        {
            if (!LearnItemResourceLookup.TryParseResourceType(input.ResourceType, out var resourceType))
                throw new ActivityDefinitionValidationException($"Unsupported resource type '{input.ResourceType}'.");
            if (!LearnItemResourceLookup.TryParseRole(input.Role, out var role))
                throw new ActivityDefinitionValidationException($"Unsupported resource link role '{input.Role}'.");

            var snapshot = await LearnItemResourceLookup.FindAsync(db, resourceType, input.ResourceId, ct)
                ?? throw new ActivityDefinitionValidationException(
                    $"Resource '{input.ResourceType}:{input.ResourceId}' was not found in the published Resource Bank.");

            var link = new ActivityResourceLink(
                activityDefinitionId, resourceType, input.ResourceId, role, snapshot.Title, snapshot.ContentFingerprint);
            links.Add(link);
            db.ActivityResourceLinks.Add(link);
        }

        return links;
    }
}
