using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ResourceImport;

/// <summary>
/// Phase 4.4 — the one place <see cref="ImportExecutionGroupInstruction"/> structural/semantic
/// rules are checked, shared by <c>ApprovedImportProfileResolver</c> (execution-time, throws on
/// the first violation — an approved plan is already final) and <c>ImportPlanDraftService</c>
/// (edit-time, collects every violation so the admin sees all of them grouped by source group at
/// once, per Workstream A9 — "do not allow approve anyway"). Pure function over already-loaded
/// data: no database access, no I/O, so it's safe to call from a bounded preview too.
/// </summary>
public static class ImportPlanInstructionValidator
{
    private static readonly HashSet<string> RecognizedFieldMappingTargets =
        new(ResourceImportRecognizedFields.All, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".m4a", ".ogg" };

    /// <summary>Returns every violation found (empty list = valid). Never throws.</summary>
    public static IReadOnlyList<ImportPlanValidationError> Validate(
        IReadOnlyList<ImportExecutionGroupInstruction> instructions, ImportPackageManifest? manifest)
    {
        var errors = new List<ImportPlanValidationError>();
        ValidateInstructionShape(instructions, errors);
        if (manifest is not null)
            ValidateAgainstManifest(instructions, manifest, errors);
        return errors;
    }

    private static void ValidateInstructionShape(
        IReadOnlyList<ImportExecutionGroupInstruction> instructions, List<ImportPlanValidationError> errors)
    {
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var instruction in instructions)
        {
            if (string.IsNullOrWhiteSpace(instruction.GroupKey))
            {
                errors.Add(new ImportPlanValidationError(null, "A group instruction has an empty group key."));
                continue;
            }

            if (!seenKeys.Add(instruction.GroupKey))
            {
                errors.Add(new ImportPlanValidationError(instruction.GroupKey,
                    $"Group '{instruction.GroupKey}' has more than one instruction."));
            }

            foreach (var target in instruction.FieldMappings.Values)
            {
                if (!RecognizedFieldMappingTargets.Contains(target))
                {
                    errors.Add(new ImportPlanValidationError(instruction.GroupKey,
                        $"Field mapping targets unrecognized field '{target}' — cannot execute an unsupported mapping. " +
                        $"Recognized fields: {string.Join(", ", ResourceImportRecognizedFields.All)}."));
                }
            }
        }
    }

    private static void ValidateAgainstManifest(
        IReadOnlyList<ImportExecutionGroupInstruction> instructions, ImportPackageManifest manifest,
        List<ImportPlanValidationError> errors)
    {
        var byGroupKey = instructions
            .Where(i => !string.IsNullOrWhiteSpace(i.GroupKey))
            .GroupBy(i => i.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var manifestFolderKeys = manifest.Entries
            .Where(e => !e.IsSuspicious)
            .Select(e => ImportExecutionGroupKey.ForRelativePath(e.RelativePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var folderKey in manifestFolderKeys)
        {
            if (!byGroupKey.TryGetValue(folderKey, out var instruction))
            {
                errors.Add(new ImportPlanValidationError(folderKey,
                    $"Group '{folderKey}' is present in the package manifest but has no instruction — " +
                    "every manifest group must be represented (explicitly excluded if not needed)."));
                continue;
            }

            if (!instruction.Included || instruction.ResourceType is null) continue;

            var entriesInGroup = manifest.Entries.Where(e =>
                !e.IsSuspicious && ImportExecutionGroupKey.ForRelativePath(e.RelativePath) == folderKey).ToList();
            var hasAudio = entriesInGroup.Any(e => AudioExtensions.Contains(e.FileExtension));

            if (hasAudio && instruction.ResourceType != ResourceCandidateType.ListeningPassage)
            {
                errors.Add(new ImportPlanValidationError(folderKey,
                    $"Audio content can only route to ListeningPassage — '{instruction.ResourceType}' is an " +
                    "unsupported route for this group."));
            }
        }
    }
}

/// <summary>One validation violation, optionally scoped to a source group so the admin UI can
/// display it against the right group card (Workstream A9: "grouped by source group").
/// <see cref="GroupKey"/> is null only for a structural error that isn't attributable to one group
/// (e.g. an instruction with a blank key).</summary>
public sealed record ImportPlanValidationError(string? GroupKey, string Message);
