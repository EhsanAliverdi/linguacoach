using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>Phase 4.4 — shared helper for the one non-execution place ProfileJson is deserialized:
/// building <see cref="ImportExecutionPlanDto.GroupInstructions"/> for display/editing. Tolerant of
/// malformed JSON (returns an empty list) since a DTO builder must never throw over a display
/// concern — execution-time validation (which does throw) lives in
/// <see cref="ApprovedImportProfileResolver"/> and edit-time validation lives in
/// <see cref="ImportPlanDraftService"/>.</summary>
internal static class ImportPlanDtoHelpers
{
    public static IReadOnlyList<ImportExecutionGroupInstruction> DeserializeGroupInstructionsSafe(string? profileJson)
    {
        if (string.IsNullOrWhiteSpace(profileJson)) return Array.Empty<ImportExecutionGroupInstruction>();
        try
        {
            IReadOnlyList<ImportExecutionGroupInstruction>? parsed =
                JsonSerializer.Deserialize<List<ImportExecutionGroupInstruction>>(profileJson);
            return parsed ?? Array.Empty<ImportExecutionGroupInstruction>();
        }
        catch (JsonException)
        {
            return Array.Empty<ImportExecutionGroupInstruction>();
        }
    }

    /// <summary>Phase 4.4B — loads a plan's full ceiling-amendment audit history, oldest first, for
    /// display in <see cref="ImportExecutionPlanDto.CeilingAmendments"/>.</summary>
    public static async Task<IReadOnlyList<ImportCostCeilingAmendmentDto>> LoadCeilingAmendmentsAsync(
        LinguaCoachDbContext db, Guid planId, CancellationToken ct)
    {
        // Ordered client-side — SQLite (the test provider) cannot translate ORDER BY over a
        // DateTimeOffset column server-side.
        var rows = await db.ImportCostCeilingAmendments
            .Where(a => a.ImportProfileId == planId)
            .ToListAsync(ct);

        return rows.OrderBy(a => a.CreatedAtUtc).Select(a => new ImportCostCeilingAmendmentDto(
            a.Id, a.PreviousCeiling, a.NewCeiling, a.Currency, a.Reason, a.AdministratorUserId, a.CreatedAtUtc)).ToList();
    }
}
