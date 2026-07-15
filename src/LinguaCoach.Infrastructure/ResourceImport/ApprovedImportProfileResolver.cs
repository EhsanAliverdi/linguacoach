using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Phase 4.3 (2026-07-16) — the single place ProfileJson is deserialized and validated. Every
// execution-time consumer (ImportPackageProcessingService today) must go through this rather than
// parsing ProfileJson itself. Resolves by the package's exact ApprovedImportProfileId — never
// "the latest" or "the most recent" ImportProfile for the package. ──

internal sealed class ApprovedImportProfileResolver : IApprovedImportProfileResolver
{
    // The only target field names any downstream pipeline (ResourceImportService's row inference,
    // Phase E6/E8 deterministic classification columns) recognizes — the same source of truth the
    // AI column-mapping proposal is validated against (see ResourceImportRecognizedFields). A
    // FieldMappings target outside this set can never produce a usable candidate — reject it at
    // resolve time instead of letting every row in that group silently fail Gate 3 one by one.
    private static readonly HashSet<string> RecognizedFieldMappingTargets =
        new(ResourceImportRecognizedFields.All, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".m4a", ".ogg" };

    private readonly LinguaCoachDbContext _db;

    public ApprovedImportProfileResolver(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<ApprovedImportExecutionProfile> ResolveAsync(Guid importPackageId, CancellationToken ct = default)
    {
        var package = await _db.ImportPackages.FirstOrDefaultAsync(p => p.Id == importPackageId, ct)
            ?? throw new ApprovedImportProfileResolutionException($"Import package '{importPackageId}' was not found.");

        if (package.ApprovedImportProfileId is not { } approvedProfileId)
            throw new ApprovedImportProfileResolutionException(
                $"Import package '{importPackageId}' has no approved Import Execution Plan — cannot execute.");

        var plan = await _db.ImportProfiles.FirstOrDefaultAsync(p => p.Id == approvedProfileId, ct)
            ?? throw new ApprovedImportProfileResolutionException(
                $"Approved Import Execution Plan '{approvedProfileId}' referenced by package '{importPackageId}' was not found.");

        if (plan.ImportPackageId != importPackageId)
            throw new ApprovedImportProfileResolutionException(
                $"Import Execution Plan '{plan.Id}' belongs to package '{plan.ImportPackageId}', not '{importPackageId}'.");

        if (plan.Status is not (ImportProfileStatus.Approved or ImportProfileStatus.Executing))
            throw new ApprovedImportProfileResolutionException(
                $"Import Execution Plan '{plan.Id}' is in status '{plan.Status}' — only an Approved or Executing plan can drive execution.");

        var instructions = DeserializeInstructions(plan);
        ValidateInstructions(instructions, plan.Id);

        if (!string.IsNullOrEmpty(package.ManifestJson))
            ValidateAgainstManifest(package, instructions);

        return new ApprovedImportExecutionProfile(plan.Id, package.Id, plan.Version, instructions);
    }

    private static List<ImportExecutionGroupInstruction> DeserializeInstructions(ImportProfile plan)
    {
        if (string.IsNullOrWhiteSpace(plan.ProfileJson))
            throw new ApprovedImportProfileResolutionException(
                $"Import Execution Plan '{plan.Id}' has an empty ProfileJson — no execution instructions are available.");

        List<ImportExecutionGroupInstruction>? instructions;
        try
        {
            instructions = JsonSerializer.Deserialize<List<ImportExecutionGroupInstruction>>(plan.ProfileJson);
        }
        catch (JsonException ex)
        {
            throw new ApprovedImportProfileResolutionException(
                $"Import Execution Plan '{plan.Id}' has a malformed ProfileJson: {ex.Message}");
        }

        if (instructions is null || instructions.Count == 0)
            throw new ApprovedImportProfileResolutionException(
                $"Import Execution Plan '{plan.Id}' has no group instructions — cannot execute.");

        return instructions;
    }

    private static void ValidateInstructions(List<ImportExecutionGroupInstruction> instructions, Guid planId)
    {
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var instruction in instructions)
        {
            if (string.IsNullOrWhiteSpace(instruction.GroupKey))
                throw new ApprovedImportProfileResolutionException(
                    $"Import Execution Plan '{planId}' has a group instruction with an empty group key.");

            if (!seenKeys.Add(instruction.GroupKey))
                throw new ApprovedImportProfileResolutionException(
                    $"Import Execution Plan '{planId}' has more than one group instruction for group '{instruction.GroupKey}'.");

            foreach (var target in instruction.FieldMappings.Values)
            {
                if (!RecognizedFieldMappingTargets.Contains(target))
                    throw new ApprovedImportProfileResolutionException(
                        $"Import Execution Plan '{planId}', group '{instruction.GroupKey}': field mapping targets " +
                        $"unrecognized field '{target}' — cannot execute an unsupported mapping.");
            }
        }
    }

    private static void ValidateAgainstManifest(ImportPackage package, List<ImportExecutionGroupInstruction> instructions)
    {
        ImportPackageManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ImportPackageManifest>(package.ManifestJson!);
        }
        catch (JsonException)
        {
            // Package manifest is validated/owned by upload/submission services, not this resolver —
            // if it's unreadable here, skip the cross-check rather than failing execution over a
            // concern outside this resolver's charter.
            return;
        }
        if (manifest is null) return;

        var byGroupKey = instructions.ToDictionary(i => i.GroupKey, StringComparer.OrdinalIgnoreCase);
        var manifestFolderKeys = manifest.Entries
            .Where(e => !e.IsSuspicious)
            .Select(e => ImportExecutionGroupKey.ForRelativePath(e.RelativePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var folderKey in manifestFolderKeys)
        {
            if (!byGroupKey.TryGetValue(folderKey, out var instruction))
                throw new ApprovedImportProfileResolutionException(
                    $"Import Execution Plan '{package.ApprovedImportProfileId}' has no instruction for group " +
                    $"'{folderKey}', which is present in the package manifest — cannot execute an incomplete plan.");

            if (!instruction.Included || instruction.ResourceType is null) continue;

            var entriesInGroup = manifest.Entries.Where(e =>
                !e.IsSuspicious && ImportExecutionGroupKey.ForRelativePath(e.RelativePath) == folderKey).ToList();
            var hasAudio = entriesInGroup.Any(e => AudioExtensions.Contains(e.FileExtension));

            if (hasAudio && instruction.ResourceType != ResourceCandidateType.ListeningPassage)
                throw new ApprovedImportProfileResolutionException(
                    $"Import Execution Plan '{package.ApprovedImportProfileId}', group '{folderKey}': audio content " +
                    $"can only route to ListeningPassage — '{instruction.ResourceType}' is an unsupported route for this group.");
        }
    }
}
