using System.Text.Json;
using LinguaCoach.Application.ModuleDefinitions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ModuleDefinitions;

public sealed class AdminCreateModuleDefinitionHandler : IAdminCreateModuleDefinitionHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminCreateModuleDefinitionHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ModuleDefinitionDto> HandleAsync(CreateModuleDefinitionCommand command, CancellationToken ct = default)
    {
        if (command.LearnItemLinks is not { Count: > 0 })
            throw new ModuleDefinitionValidationException("At least one Learn Item is required to create a Module.");
        if (command.ActivityLinks is not { Count: > 0 })
            throw new ModuleDefinitionValidationException("At least one Activity Definition is required to create a Module.");

        ModuleDefinition module;
        try
        {
            module = new ModuleDefinition(
                command.Title, ModuleSourceMode.Manual, command.Description, command.ObjectiveKey,
                command.CefrLevel, command.Skill, command.Subskill,
                ToJsonArray(command.ContextTags), ToJsonArray(command.FocusTags),
                command.DifficultyBand, command.EstimatedMinutes, command.FeedbackPlanJson,
                generationProvider: null, generationModel: null, createdByUserId: command.CreatedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ModuleDefinitionValidationException(ex.Message);
        }

        _db.ModuleDefinitions.Add(module);
        await _db.SaveChangesAsync(ct);

        var (learnItemLinks, activityLinks) = await ModuleLinkBuilder.BuildAndAddAsync(
            _db, module.Id, command.LearnItemLinks, command.ActivityLinks, requireApproved: false, ct);
        await _db.SaveChangesAsync(ct);

        return ModuleDefinitionMappers.ToDto(module, learnItemLinks, activityLinks);
    }

    private static string ToJsonArray(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : "[]";
}

public sealed class AdminUpdateModuleDefinitionHandler : IAdminUpdateModuleDefinitionHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminUpdateModuleDefinitionHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ModuleDefinitionDto> HandleAsync(UpdateModuleDefinitionCommand command, CancellationToken ct = default)
    {
        var module = await _db.ModuleDefinitions.FirstOrDefaultAsync(m => m.Id == command.Id, ct)
            ?? throw new ModuleDefinitionValidationException($"Module '{command.Id}' was not found.");

        try
        {
            module.UpdateDraft(
                command.Title, command.Description, command.CefrLevel, command.Skill, command.Subskill,
                ToJsonArray(command.ContextTags), ToJsonArray(command.FocusTags),
                command.DifficultyBand, command.EstimatedMinutes, command.FeedbackPlanJson);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new ModuleDefinitionValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        var learnItemLinks = await _db.ModuleDefinitionLearnItemLinks.Where(l => l.ModuleDefinitionId == module.Id).ToListAsync(ct);
        var activityLinks = await _db.ModuleDefinitionActivityLinks.Where(l => l.ModuleDefinitionId == module.Id).ToListAsync(ct);
        return ModuleDefinitionMappers.ToDto(module, learnItemLinks, activityLinks);
    }

    private static string ToJsonArray(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : "[]";
}

public sealed class AdminApproveModuleDefinitionHandler : IAdminApproveModuleDefinitionHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminApproveModuleDefinitionHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ModuleDefinitionDto> HandleAsync(ApproveModuleDefinitionCommand command, CancellationToken ct = default)
    {
        var module = await _db.ModuleDefinitions.FirstOrDefaultAsync(m => m.Id == command.Id, ct)
            ?? throw new ModuleDefinitionValidationException($"Module '{command.Id}' was not found.");

        module.Approve(command.ReviewedByUserId, command.Notes);
        await _db.SaveChangesAsync(ct);

        var learnItemLinks = await _db.ModuleDefinitionLearnItemLinks.Where(l => l.ModuleDefinitionId == module.Id).ToListAsync(ct);
        var activityLinks = await _db.ModuleDefinitionActivityLinks.Where(l => l.ModuleDefinitionId == module.Id).ToListAsync(ct);
        return ModuleDefinitionMappers.ToDto(module, learnItemLinks, activityLinks);
    }
}

public sealed class AdminRejectModuleDefinitionHandler : IAdminRejectModuleDefinitionHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminRejectModuleDefinitionHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<ModuleDefinitionDto> HandleAsync(RejectModuleDefinitionCommand command, CancellationToken ct = default)
    {
        var module = await _db.ModuleDefinitions.FirstOrDefaultAsync(m => m.Id == command.Id, ct)
            ?? throw new ModuleDefinitionValidationException($"Module '{command.Id}' was not found.");

        try
        {
            module.Reject(command.Reason, command.ReviewedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new ModuleDefinitionValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        var learnItemLinks = await _db.ModuleDefinitionLearnItemLinks.Where(l => l.ModuleDefinitionId == module.Id).ToListAsync(ct);
        var activityLinks = await _db.ModuleDefinitionActivityLinks.Where(l => l.ModuleDefinitionId == module.Id).ToListAsync(ct);
        return ModuleDefinitionMappers.ToDto(module, learnItemLinks, activityLinks);
    }
}

/// <summary>Shared link-creation logic for <see cref="AdminCreateModuleDefinitionHandler"/> and
/// <see cref="ModuleGenerationService"/>. When <paramref name="requireApproved"/> is true (every
/// generation entry point), a Learn Item/Activity Definition that isn't
/// <see cref="AdminReviewStatus.Approved"/> is rejected outright rather than silently linked —
/// generated Modules only ever compose already-reviewed content. Manual creation
/// (<paramref name="requireApproved"/> = false) allows any status, mirroring
/// <c>AdminCreateActivityDefinitionHandler</c>'s manual-create flexibility.</summary>
internal static class ModuleLinkBuilder
{
    public static async Task<(List<ModuleDefinitionLearnItemLink> LearnItemLinks, List<ModuleDefinitionActivityLink> ActivityLinks)> BuildAndAddAsync(
        LinguaCoachDbContext db, Guid moduleDefinitionId,
        IReadOnlyList<ModuleLearnItemLinkInput> learnItemInputs, IReadOnlyList<ModuleActivityLinkInput> activityInputs,
        bool requireApproved, CancellationToken ct)
    {
        var learnItemLinks = new List<ModuleDefinitionLearnItemLink>();
        var sortOrder = 0;
        foreach (var input in learnItemInputs)
        {
            if (!Enum.TryParse<LearnItemResourceRole>(input.Role, ignoreCase: true, out var role))
                throw new ModuleDefinitionValidationException($"Unsupported Learn Item link role '{input.Role}'.");

            var learnItem = await db.LearnItems.FirstOrDefaultAsync(l => l.Id == input.LearnItemId, ct)
                ?? throw new ModuleDefinitionValidationException($"Learn Item '{input.LearnItemId}' was not found.");
            if (requireApproved && learnItem.ReviewStatus != AdminReviewStatus.Approved)
                throw new ModuleDefinitionValidationException(
                    $"Learn Item '{learnItem.Title}' is not approved yet — approve it before generating a Module from it.");

            var link = new ModuleDefinitionLearnItemLink(moduleDefinitionId, input.LearnItemId, role, sortOrder++, learnItem.Title);
            learnItemLinks.Add(link);
            db.ModuleDefinitionLearnItemLinks.Add(link);
        }

        var activityLinks = new List<ModuleDefinitionActivityLink>();
        sortOrder = 0;
        foreach (var input in activityInputs)
        {
            if (!Enum.TryParse<ModuleActivityRole>(input.Role, ignoreCase: true, out var role))
                throw new ModuleDefinitionValidationException($"Unsupported Activity link role '{input.Role}'.");

            var activity = await db.ActivityDefinitions.FirstOrDefaultAsync(a => a.Id == input.ActivityDefinitionId, ct)
                ?? throw new ModuleDefinitionValidationException($"Activity Definition '{input.ActivityDefinitionId}' was not found.");
            if (requireApproved && activity.ReviewStatus != AdminReviewStatus.Approved)
                throw new ModuleDefinitionValidationException(
                    $"Activity Definition '{activity.Title}' is not approved yet — approve it before generating a Module from it.");

            var link = new ModuleDefinitionActivityLink(
                moduleDefinitionId, input.ActivityDefinitionId, role, sortOrder++, input.Required, activity.Title);
            activityLinks.Add(link);
            db.ModuleDefinitionActivityLinks.Add(link);
        }

        return (learnItemLinks, activityLinks);
    }
}
