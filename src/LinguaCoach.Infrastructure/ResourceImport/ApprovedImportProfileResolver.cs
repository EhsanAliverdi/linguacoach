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

        ImportPackageManifest? manifest = null;
        if (!string.IsNullOrEmpty(package.ManifestJson))
        {
            try
            {
                manifest = JsonSerializer.Deserialize<ImportPackageManifest>(package.ManifestJson);
            }
            catch (JsonException)
            {
                // Package manifest is validated/owned by upload/submission services, not this
                // resolver — if it's unreadable here, skip the cross-check rather than failing
                // execution over a concern outside this resolver's charter.
            }
        }

        var errors = ImportPlanInstructionValidator.Validate(instructions, manifest);
        if (errors.Count > 0)
            throw new ApprovedImportProfileResolutionException(
                $"Import Execution Plan '{plan.Id}' failed validation: " +
                string.Join(" ", errors.Select(e => e.GroupKey is null ? e.Message : $"[{e.GroupKey}] {e.Message}")));

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
}
