namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>Phase 4.4E — configuration for <see cref="AudioDurationProbe"/>. No machine-specific
/// path is hardcoded: <see cref="FfprobePath"/> defaults to the bare tool name, resolved via the
/// process's PATH, and is fully overridable per environment (e.g. a container image that installs
/// ffmpeg at a fixed location).</summary>
public sealed class AudioDurationProbeOptions
{
    public const string SectionName = "AudioDurationProbe";

    public string FfprobePath { get; set; } = "ffprobe";

    /// <summary>Hard wall-clock bound on one probe invocation — a hung/misbehaving process must
    /// never block package processing indefinitely.</summary>
    public int TimeoutSeconds { get; set; } = 15;
}
