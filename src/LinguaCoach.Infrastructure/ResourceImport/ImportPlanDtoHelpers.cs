using System.Text.Json;
using LinguaCoach.Application.ResourceImport;

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
}
