using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase 4.4E — resolves one <see cref="ImportAsset"/>'s real audio duration: reuses a prior
/// measurement when the asset's content checksum is unchanged (<see cref="ImportAsset.HasReusableAudioDurationMeasurement"/>),
/// otherwise reads the asset's content and measures it via <see cref="IAudioDurationProbe"/>.
/// Mutates the asset entity's measurement fields in memory only — the caller must
/// <c>SaveChangesAsync</c>.
/// </summary>
internal sealed class ImportAssetAudioDurationResolver : IImportAssetAudioDurationResolver
{
    private readonly IFileStorageService _storage;
    private readonly IAudioDurationProbe _probe;

    public ImportAssetAudioDurationResolver(IFileStorageService storage, IAudioDurationProbe probe)
    {
        _storage = storage;
        _probe = probe;
    }

    public async Task<ImportAssetAudioDurationResult> ResolveAsync(ImportAsset asset, CancellationToken ct = default)
    {
        if (asset.HasReusableAudioDurationMeasurement())
            return new ImportAssetAudioDurationResult(true, asset.AudioDurationSeconds, null, WasReused: true);

        await using var stream = await _storage.ReadAsync(asset.StorageKey, ct);
        var probeResult = await _probe.ProbeDurationAsync(stream, asset.FileExtension, ct);

        if (probeResult.Success)
        {
            asset.RecordAudioDurationMeasured(probeResult.DurationSeconds!.Value, DateTimeOffset.UtcNow);
            return new ImportAssetAudioDurationResult(true, probeResult.DurationSeconds, null, WasReused: false);
        }

        var errorMessage = probeResult.ErrorMessage ?? "Could not measure audio duration.";
        asset.RecordAudioDurationMeasurementFailed(errorMessage, DateTimeOffset.UtcNow);
        return new ImportAssetAudioDurationResult(false, null, errorMessage, WasReused: false);
    }
}
