using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ResourceImport;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>Phase 4 (2026-07-15), Part C — deterministic processing-mode decision rules.</summary>
public sealed class ImportProcessingModeDecisionServiceTests
{
    private static ImportProcessingModeDecisionService CreateService(ImportPackageLimitsOptions? limits = null) =>
        new(Options.Create(limits ?? new ImportPackageLimitsOptions()));

    private static ImportPackageManifestEntry Entry(string path, string ext, long size = 100) =>
        new(path, System.IO.Path.GetFileName(path), ext, size, size, "text/plain", "abc123", false, null);

    [Fact]
    public void Decide_returns_SampleDriven_when_expanded_size_exceeds_threshold()
    {
        var limits = new ImportPackageLimitsOptions { FullAiAnalysisMaxExpandedSizeBytes = 100 };
        var manifest = new ImportPackageManifest(
            true, null, 1000, 1000, 1,
            new[] { Entry("a.csv", ".csv") }, Array.Empty<ImportPackageFolderGroup>(),
            new[] { ".csv" }, Array.Empty<ImportPackageManifestEntry>(),
            Array.Empty<ImportPackageManifestEntry>(), Array.Empty<ImportPackageManifestEntry>());

        var decision = CreateService(limits).Decide(manifest);

        decision.Mode.Should().Be(ImportProcessingMode.SampleDriven);
        decision.Reason.Should().Contain("expanded size");
    }

    [Fact]
    public void Decide_returns_Direct_for_small_single_folder_structured_data_only()
    {
        var entries = new[] { Entry("words.csv", ".csv"), Entry("more.csv", ".csv") };
        var manifest = new ImportPackageManifest(
            true, null, 200, 200, 2, entries,
            new[] { new ImportPackageFolderGroup("", 2, new[] { ".csv" }) },
            new[] { ".csv" }, Array.Empty<ImportPackageManifestEntry>(),
            Array.Empty<ImportPackageManifestEntry>(), Array.Empty<ImportPackageManifestEntry>());

        var decision = CreateService().Decide(manifest);

        decision.Mode.Should().Be(ImportProcessingMode.Direct);
    }

    [Fact]
    public void Decide_returns_FullAiAssisted_for_mixed_media_within_size_limits()
    {
        var entries = new[] { Entry("audio.mp3", ".mp3"), Entry("image.jpg", ".jpg") };
        var manifest = new ImportPackageManifest(
            true, null, 200, 200, 2, entries,
            new[] { new ImportPackageFolderGroup("", 2, new[] { ".mp3", ".jpg" }) },
            new[] { ".mp3", ".jpg" }, Array.Empty<ImportPackageManifestEntry>(),
            Array.Empty<ImportPackageManifestEntry>(), Array.Empty<ImportPackageManifestEntry>());

        var decision = CreateService().Decide(manifest);

        decision.Mode.Should().Be(ImportProcessingMode.FullAiAssisted);
    }

    [Fact]
    public void Decide_returns_SampleDriven_when_entry_count_exceeds_threshold()
    {
        var limits = new ImportPackageLimitsOptions { FullAiAnalysisMaxFileCount = 1 };
        var entries = new[] { Entry("a.csv", ".csv"), Entry("b.csv", ".csv") };
        var manifest = new ImportPackageManifest(
            true, null, 200, 200, 2, entries,
            new[] { new ImportPackageFolderGroup("", 2, new[] { ".csv" }) },
            new[] { ".csv" }, Array.Empty<ImportPackageManifestEntry>(),
            Array.Empty<ImportPackageManifestEntry>(), Array.Empty<ImportPackageManifestEntry>());

        var decision = CreateService(limits).Decide(manifest);

        decision.Mode.Should().Be(ImportProcessingMode.SampleDriven);
        decision.Reason.Should().Contain("files");
    }
}
