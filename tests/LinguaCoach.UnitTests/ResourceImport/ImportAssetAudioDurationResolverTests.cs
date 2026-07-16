using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Infrastructure.Storage;
using Xunit;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase 4.4E — direct unit coverage of <see cref="ImportAssetAudioDurationResolver"/>: reuse of a
/// prior measurement when the asset's content checksum is unchanged, remeasurement when it has
/// changed, and clear failure propagation (never a silent five-minute fallback). Uses a fake
/// <see cref="IAudioDurationProbe"/> — never a real ffprobe call.
/// </summary>
public sealed class ImportAssetAudioDurationResolverTests
{
    private sealed class FakeProbe : IAudioDurationProbe
    {
        public decimal NextDurationSeconds { get; set; } = 42m;
        public AudioDurationProbeStatus? NextFailureStatus { get; set; }
        public string? NextFailureMessage { get; set; }
        public int CallCount { get; private set; }

        public Task<AudioDurationProbeResult> ProbeDurationAsync(Stream audioStream, string fileExtension, CancellationToken ct = default)
        {
            CallCount++;
            if (NextFailureStatus is { } status)
                return Task.FromResult(AudioDurationProbeResult.Failed(status, NextFailureMessage ?? "Simulated failure."));
            return Task.FromResult(AudioDurationProbeResult.Ok(NextDurationSeconds));
        }
    }

    private static async Task<(ImportAsset Asset, FakeFileStorageService Storage, FakeProbe Probe, ImportAssetAudioDurationResolver Resolver)> SeedAsync(
        string checksum = "checksum-1")
    {
        var storage = new FakeFileStorageService();
        await storage.SaveAsync("audio-key", new MemoryStream(new byte[] { 1, 2, 3 }), "audio/mpeg");
        var probe = new FakeProbe();
        var resolver = new ImportAssetAudioDurationResolver(storage, probe);

        var asset = new ImportAsset(
            Guid.NewGuid(), "audio.mp3", "audio.mp3", "audio-key", "audio/mpeg",
            ImportAssetMediaType.Audio, ".mp3", 3, checksum, DateTimeOffset.UtcNow);

        return (asset, storage, probe, resolver);
    }

    [Fact]
    public async Task Unmeasured_asset_is_measured_via_the_probe()
    {
        var (asset, _, probe, resolver) = await SeedAsync();
        probe.NextDurationSeconds = 123m;

        var result = await resolver.ResolveAsync(asset);

        result.Success.Should().BeTrue();
        result.WasReused.Should().BeFalse();
        result.DurationSeconds.Should().Be(123m);
        probe.CallCount.Should().Be(1);

        asset.AudioDurationSeconds.Should().Be(123m);
        asset.AudioDurationMeasurementStatus.Should().Be(ImportAudioDurationMeasurementStatus.Measured);
        asset.AudioDurationMeasurementChecksum.Should().Be("checksum-1");
        asset.AudioDurationMeasuredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Stored_measurement_is_reused_when_checksum_is_unchanged_no_second_probe_call()
    {
        var (asset, _, probe, resolver) = await SeedAsync();
        await resolver.ResolveAsync(asset); // first measurement
        probe.CallCount.Should().Be(1);

        var second = await resolver.ResolveAsync(asset);

        second.Success.Should().BeTrue();
        second.WasReused.Should().BeTrue();
        second.DurationSeconds.Should().Be(asset.AudioDurationSeconds);
        probe.CallCount.Should().Be(1, "the probe must not be called again when the checksum is unchanged");
    }

    [Fact]
    public async Task Changed_checksum_triggers_remeasurement()
    {
        var (asset, storage, probe, resolver) = await SeedAsync(checksum: "checksum-1");
        probe.NextDurationSeconds = 100m;
        await resolver.ResolveAsync(asset);
        probe.CallCount.Should().Be(1);

        // Simulate the asset's content changing (a new ImportAsset would normally be created for
        // genuinely new content — this directly exercises the reuse-decision boundary condition).
        var changedAsset = new ImportAsset(
            asset.ImportPackageId, asset.OriginalFileName, asset.RelativePath, asset.StorageKey,
            asset.MimeType, asset.DetectedMediaType, asset.FileExtension, asset.UncompressedSizeBytes,
            "checksum-2", asset.UploadedAtUtc);
        // Copy the OLD measurement onto the new-content asset to simulate "measurement recorded
        // against stale content" — this is the exact state HasReusableAudioDurationMeasurement()
        // must reject.
        typeof(ImportAsset).GetProperty(nameof(ImportAsset.AudioDurationSeconds))!.SetValue(changedAsset, 100m);
        typeof(ImportAsset).GetProperty(nameof(ImportAsset.AudioDurationMeasurementChecksum))!.SetValue(changedAsset, "checksum-1");
        typeof(ImportAsset).GetProperty(nameof(ImportAsset.AudioDurationMeasurementStatus))!.SetValue(changedAsset, ImportAudioDurationMeasurementStatus.Measured);

        probe.NextDurationSeconds = 200m;
        var result = await resolver.ResolveAsync(changedAsset);

        result.WasReused.Should().BeFalse("the stored measurement's checksum no longer matches the asset's current checksum");
        result.DurationSeconds.Should().Be(200m);
        probe.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Probe_failure_is_recorded_clearly_never_a_silent_duration_fallback()
    {
        var (asset, _, probe, resolver) = await SeedAsync();
        probe.NextFailureStatus = AudioDurationProbeStatus.UnsupportedOrCorrupt;
        probe.NextFailureMessage = "ffprobe exited with code 1: Invalid data found when processing input";

        var result = await resolver.ResolveAsync(asset);

        result.Success.Should().BeFalse();
        result.DurationSeconds.Should().BeNull();
        result.ErrorMessage.Should().Contain("Invalid data");

        asset.AudioDurationMeasurementStatus.Should().Be(ImportAudioDurationMeasurementStatus.Failed);
        asset.AudioDurationSeconds.Should().BeNull("a failed measurement must never leave a fabricated duration behind");
        asset.AudioDurationMeasurementError.Should().Contain("Invalid data");
    }
}
