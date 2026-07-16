using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.ResourceImport;
using Xunit;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase 4.5 — a candidate must never link (via ImportCandidateAssetLink) an asset from a
/// different ImportPackage. Pure, no database.
/// </summary>
public sealed class ImportAssetProvenanceGuardTests
{
    private static ImportAsset NewAsset(Guid packageId) => new(
        packageId, "audio.mp3", "audio.mp3", $"storage-{Guid.NewGuid():N}", "audio/mpeg",
        LinguaCoach.Domain.Enums.ImportAssetMediaType.Audio, ".mp3", 1000,
        $"checksum-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);

    [Fact]
    public void Asset_from_the_same_package_passes()
    {
        var packageId = Guid.NewGuid();
        var asset = NewAsset(packageId);

        var act = () => ImportAssetProvenanceGuard.EnsureAssetBelongsToPackage(asset, packageId);

        act.Should().NotThrow();
    }

    [Fact]
    public void Asset_from_a_different_package_is_rejected()
    {
        var asset = NewAsset(Guid.NewGuid());
        var otherPackageId = Guid.NewGuid();

        var act = () => ImportAssetProvenanceGuard.EnsureAssetBelongsToPackage(asset, otherPackageId);

        act.Should().Throw<ResourceImportValidationException>()
            .WithMessage("*must never cross package boundaries*");
    }
}
