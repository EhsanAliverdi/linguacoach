using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase 4.5 — a <see cref="ResourceCandidate"/> may only link (via
/// <see cref="ImportCandidateAssetLink"/>) assets staged under its own <see cref="ImportPackage"/>.
/// This is always true by construction on the one call site that uses it today
/// (<see cref="ImportPackageProcessingService"/>'s Listening-candidate creation, where both the
/// audio and transcript assets come from the same package's own asset list), but is enforced
/// explicitly rather than only implicitly, so a future call site cannot silently create a
/// cross-package reference.
/// </summary>
internal static class ImportAssetProvenanceGuard
{
    public static void EnsureAssetBelongsToPackage(ImportAsset asset, Guid expectedPackageId)
    {
        if (asset.ImportPackageId != expectedPackageId)
        {
            throw new ResourceImportValidationException(
                $"ImportAsset '{asset.Id}' belongs to package '{asset.ImportPackageId}', not the candidate's " +
                $"own package '{expectedPackageId}' — an asset reference must never cross package boundaries.");
        }
    }
}
