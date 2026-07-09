using System.Text.Json;
using LinguaCoach.Application.LearnItems;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.LearnItems;

public sealed class AdminCreateLearnItemHandler : IAdminCreateLearnItemHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminCreateLearnItemHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<LearnItemDto> HandleAsync(CreateLearnItemCommand command, CancellationToken ct = default)
    {
        LearnItem item;
        try
        {
            item = new LearnItem(
                command.Title, command.Body, LearnItemSourceMode.Manual,
                command.CefrLevel, command.Skill, command.Subskill,
                ToJsonArray(command.ContextTags), ToJsonArray(command.FocusTags),
                ToJsonArray(command.Examples), ToJsonArray(command.CommonMistakes),
                command.UsageNotes, command.DifficultyBand, command.EstimatedMinutes,
                generationProvider: null, generationModel: null, createdByUserId: command.CreatedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new LearnItemValidationException(ex.Message);
        }

        _db.LearnItems.Add(item);
        await _db.SaveChangesAsync(ct);

        var links = await LearnItemLinkBuilder.BuildAndAddAsync(_db, item.Id, command.Links, ct);
        if (links.Count > 0)
            await _db.SaveChangesAsync(ct);

        return LearnItemMappers.ToDto(item, links);
    }

    private static string ToJsonArray(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : "[]";
}

public sealed class AdminUpdateLearnItemHandler : IAdminUpdateLearnItemHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminUpdateLearnItemHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<LearnItemDto> HandleAsync(UpdateLearnItemCommand command, CancellationToken ct = default)
    {
        var item = await _db.LearnItems.FirstOrDefaultAsync(i => i.Id == command.Id, ct)
            ?? throw new LearnItemValidationException($"Learn Item '{command.Id}' was not found.");

        try
        {
            item.UpdateDraft(
                command.Title, command.Body,
                ToJsonArray(command.Examples), ToJsonArray(command.CommonMistakes), command.UsageNotes,
                command.CefrLevel, command.Skill, command.Subskill,
                ToJsonArray(command.ContextTags), ToJsonArray(command.FocusTags),
                command.DifficultyBand, command.EstimatedMinutes);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new LearnItemValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        var links = await _db.LearnItemResourceLinks.Where(l => l.LearnItemId == item.Id).ToListAsync(ct);
        return LearnItemMappers.ToDto(item, links);
    }

    private static string ToJsonArray(IReadOnlyList<string>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : "[]";
}

public sealed class AdminApproveLearnItemHandler : IAdminApproveLearnItemHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminApproveLearnItemHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<LearnItemDto> HandleAsync(ApproveLearnItemCommand command, CancellationToken ct = default)
    {
        var item = await _db.LearnItems.FirstOrDefaultAsync(i => i.Id == command.Id, ct)
            ?? throw new LearnItemValidationException($"Learn Item '{command.Id}' was not found.");

        item.Approve(command.ReviewedByUserId, command.Notes);
        await _db.SaveChangesAsync(ct);

        var links = await _db.LearnItemResourceLinks.Where(l => l.LearnItemId == item.Id).ToListAsync(ct);
        return LearnItemMappers.ToDto(item, links);
    }
}

public sealed class AdminRejectLearnItemHandler : IAdminRejectLearnItemHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminRejectLearnItemHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<LearnItemDto> HandleAsync(RejectLearnItemCommand command, CancellationToken ct = default)
    {
        var item = await _db.LearnItems.FirstOrDefaultAsync(i => i.Id == command.Id, ct)
            ?? throw new LearnItemValidationException($"Learn Item '{command.Id}' was not found.");

        try
        {
            item.Reject(command.Reason, command.ReviewedByUserId);
        }
        catch (ArgumentException ex)
        {
            throw new LearnItemValidationException(ex.Message);
        }

        await _db.SaveChangesAsync(ct);

        var links = await _db.LearnItemResourceLinks.Where(l => l.LearnItemId == item.Id).ToListAsync(ct);
        return LearnItemMappers.ToDto(item, links);
    }
}

/// <summary>Shared link-creation logic for <see cref="AdminCreateLearnItemHandler"/> and
/// <see cref="LearnItemGenerationService"/>: parses/validates each requested
/// <see cref="LearnItemResourceLinkInput"/>, looks up the referenced published row, and stages a
/// <see cref="LearnItemResourceLink"/> (added to the context, not yet saved) for each one that
/// resolves. Throws <see cref="LearnItemValidationException"/> on the first invalid type/role or
/// not-found resource — never silently drops a bad reference.</summary>
internal static class LearnItemLinkBuilder
{
    public static async Task<List<LearnItemResourceLink>> BuildAndAddAsync(
        LinguaCoachDbContext db, Guid learnItemId, IReadOnlyList<LearnItemResourceLinkInput>? inputs, CancellationToken ct)
    {
        var links = new List<LearnItemResourceLink>();
        if (inputs is null) return links;

        foreach (var input in inputs)
        {
            if (!LearnItemResourceLookup.TryParseResourceType(input.ResourceType, out var resourceType))
                throw new LearnItemValidationException($"Unsupported resource type '{input.ResourceType}'.");
            if (!LearnItemResourceLookup.TryParseRole(input.Role, out var role))
                throw new LearnItemValidationException($"Unsupported resource link role '{input.Role}'.");

            var snapshot = await LearnItemResourceLookup.FindAsync(db, resourceType, input.ResourceId, ct)
                ?? throw new LearnItemValidationException(
                    $"Resource '{input.ResourceType}:{input.ResourceId}' was not found in the published Resource Bank.");

            var link = new LearnItemResourceLink(
                learnItemId, resourceType, input.ResourceId, role, snapshot.Title, snapshot.ContentFingerprint);
            links.Add(link);
            db.LearnItemResourceLinks.Add(link);
        }

        return links;
    }
}
