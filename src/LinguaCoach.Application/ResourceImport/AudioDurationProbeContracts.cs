using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Application.ResourceImport;

// ── Phase 4.4E — real audio-duration measurement, replacing the flat five-minute-per-file
// assumption. IAudioDurationProbe is a thin, stateless abstraction over a media-metadata tool
// (ffprobe by default) — it reads container/format metadata only, never transcribes or decodes
// audio content, and never determines duration via AI. ──

public enum AudioDurationProbeStatus
{
    Success,
    /// <summary>The configured probe tool could not be started (missing binary, not executable,
    /// etc.) — a clear, distinct failure from "the file itself is bad."</summary>
    ToolUnavailable,
    /// <summary>The probe did not complete within the configured timeout.</summary>
    Timeout,
    /// <summary>The caller's own <c>CancellationToken</c> was cancelled.</summary>
    Cancelled,
    /// <summary>The file extension is not one Import accepts as audio, or the tool ran but could
    /// not extract a usable duration (corrupt/unsupported content).</summary>
    UnsupportedOrCorrupt,
}

public sealed record AudioDurationProbeResult(AudioDurationProbeStatus Status, decimal? DurationSeconds, string? ErrorMessage)
{
    public bool Success => Status == AudioDurationProbeStatus.Success && DurationSeconds is > 0;

    public static AudioDurationProbeResult Ok(decimal durationSeconds) =>
        new(AudioDurationProbeStatus.Success, durationSeconds, null);

    public static AudioDurationProbeResult Failed(AudioDurationProbeStatus status, string errorMessage) =>
        new(status, null, errorMessage);
}

/// <summary>Measures the real duration of one audio file's content. Never transcribes; never
/// calls a paid AI/STT provider — reads only format/container metadata.</summary>
public interface IAudioDurationProbe
{
    Task<AudioDurationProbeResult> ProbeDurationAsync(Stream audioStream, string fileExtension, CancellationToken ct = default);
}

/// <summary>Result of resolving one <c>ImportAsset</c>'s audio duration — either reused from a
/// prior measurement (content checksum unchanged) or freshly measured (and persisted onto the
/// asset entity by the caller's next <c>SaveChangesAsync</c>).</summary>
public sealed record ImportAssetAudioDurationResult(bool Success, decimal? DurationSeconds, string? ErrorMessage, bool WasReused);

/// <summary>Resolves one <c>ImportAsset</c>'s real audio duration — reusing a prior measurement
/// when the asset's content checksum has not changed, remeasuring (via <see cref="IAudioDurationProbe"/>)
/// otherwise. Mutates the asset entity's measurement fields in memory only; the caller is
/// responsible for <c>SaveChangesAsync</c>.</summary>
public interface IImportAssetAudioDurationResolver
{
    Task<ImportAssetAudioDurationResult> ResolveAsync(ImportAsset asset, CancellationToken ct = default);
}
