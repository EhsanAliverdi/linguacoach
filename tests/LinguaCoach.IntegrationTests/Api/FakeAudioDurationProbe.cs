using LinguaCoach.Application.ResourceImport;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Phase 4.4E — the integration test host's substitute for <c>AudioDurationProbe</c> (which shells
/// out to a real <c>ffprobe</c> binary that is not guaranteed to be installed on every dev/CI
/// machine). Registered in <see cref="ApiTestFactory"/> in place of the real probe, mirroring the
/// existing "fake storage instead of real blob storage" override in the same file — the real probe
/// itself is covered by focused, environment-independent unit tests
/// (<c>AudioDurationProbeTests</c>) that do not require ffprobe to be present (missing-binary,
/// unsupported-extension, pre-cancelled-token paths).
///
/// Returns a fixed, clearly-labeled 300-second (5-minute) duration by default — deliberately the
/// same figure the old flat assumption used, so this substitution is behaviourally neutral for
/// every pre-existing STT integration test, while still exercising the real
/// <c>ImportAssetAudioDurationResolver</c>'s persistence/reuse-by-checksum logic genuinely (this
/// class only fakes the low-level "read the file and report a duration" step).
/// </summary>
public sealed class FakeAudioDurationProbe : IAudioDurationProbe
{
    public decimal NextDurationSeconds { get; set; } = 300m;
    public AudioDurationProbeStatus? NextFailureStatus { get; set; }
    public string? NextFailureMessage { get; set; }
    public int CallCount { get; private set; }

    public Task<AudioDurationProbeResult> ProbeDurationAsync(Stream audioStream, string fileExtension, CancellationToken ct = default)
    {
        CallCount++;
        if (NextFailureStatus is { } status)
            return Task.FromResult(AudioDurationProbeResult.Failed(status, NextFailureMessage ?? "Simulated probe failure."));
        return Task.FromResult(AudioDurationProbeResult.Ok(NextDurationSeconds));
    }
}
