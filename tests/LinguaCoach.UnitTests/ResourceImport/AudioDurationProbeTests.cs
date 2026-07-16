using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Infrastructure.ResourceImport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase 4.4E — direct unit coverage of the real, ffprobe-shelling <see cref="AudioDurationProbe"/>,
/// scoped to paths that are deterministic and portable regardless of whether ffprobe is actually
/// installed on the machine running these tests (it is not assumed to be present — see
/// <see cref="Missing_probe_binary_fails_clearly"/> and the class-level remarks below). The
/// "successfully measures a real audio file via a real ffprobe binary" happy path is not exercised
/// here — it would require a real ffprobe installation this repository's dev/CI environment does
/// not guarantee; the reuse/checksum/estimate/ceiling/cost behaviours that depend on a successful
/// measurement are instead proven via <see cref="ImportAssetAudioDurationResolverTests"/> and
/// <c>ImportPackageProcessingServiceTests</c> using a fake <see cref="IAudioDurationProbe"/>.
/// </summary>
public sealed class AudioDurationProbeTests
{
    private static AudioDurationProbe MakeProbe(string ffprobePath = "ffprobe", int timeoutSeconds = 15) =>
        new(Options.Create(new AudioDurationProbeOptions { FfprobePath = ffprobePath, TimeoutSeconds = timeoutSeconds }),
            NullLogger<AudioDurationProbe>.Instance);

    [Fact]
    public async Task Unsupported_extension_fails_clearly_without_invoking_any_tool()
    {
        var probe = MakeProbe();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await probe.ProbeDurationAsync(stream, ".txt");

        result.Success.Should().BeFalse();
        result.Status.Should().Be(AudioDurationProbeStatus.UnsupportedOrCorrupt);
        result.ErrorMessage.Should().Contain("Unsupported audio extension");
    }

    [Fact]
    public async Task Missing_probe_binary_fails_clearly()
    {
        var probe = MakeProbe(ffprobePath: $"nonexistent-ffprobe-binary-{Guid.NewGuid():N}");
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        var result = await probe.ProbeDurationAsync(stream, ".mp3");

        result.Success.Should().BeFalse();
        result.Status.Should().Be(AudioDurationProbeStatus.ToolUnavailable);
        result.ErrorMessage.Should().Contain("ffprobe is not available");
        result.DurationSeconds.Should().BeNull();
    }

    [Fact]
    public async Task A_precancelled_token_is_reported_as_cancelled_not_a_tool_or_content_failure()
    {
        var probe = MakeProbe();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await probe.ProbeDurationAsync(stream, ".mp3", cts.Token);

        result.Success.Should().BeFalse();
        result.Status.Should().Be(AudioDurationProbeStatus.Cancelled);
    }

    [Theory]
    [InlineData(".mp3")]
    [InlineData(".wav")]
    [InlineData(".m4a")]
    [InlineData(".ogg")]
    public async Task Every_currently_accepted_audio_extension_is_recognized_as_supported(string extension)
    {
        // A missing binary still proves the extension itself passed the supported-format gate —
        // if it hadn't, this would fail with UnsupportedOrCorrupt instead of ToolUnavailable.
        var probe = MakeProbe(ffprobePath: $"nonexistent-ffprobe-binary-{Guid.NewGuid():N}");
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await probe.ProbeDurationAsync(stream, extension);

        result.Status.Should().Be(AudioDurationProbeStatus.ToolUnavailable);
    }
}
