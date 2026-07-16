using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using LinguaCoach.Application.ResourceImport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase 4.4E — real audio-duration measurement via <c>ffprobe</c> (part of the ffmpeg suite).
/// Reads container/format metadata only (<c>-show_entries format=duration</c>) — never decodes,
/// transcribes, or otherwise interprets the audio content itself. The audio stream is copied to a
/// bounded temporary file (ffprobe needs a seekable, on-disk input for reliable duration reporting
/// across formats) which is always deleted afterward, even on failure/cancellation.
///
/// Safety discipline: the tool path is fully configurable (<see cref="AudioDurationProbeOptions.FfprobePath"/>,
/// never a hardcoded machine-specific path), arguments are passed via <see cref="ProcessStartInfo.ArgumentList"/>
/// (never a concatenated command-line string — no shell is ever invoked, so there is no shell
/// injection surface), and every invocation is bounded by both a configurable timeout and the
/// caller's own <see cref="CancellationToken"/>.
/// </summary>
public sealed class AudioDurationProbe : IAudioDurationProbe
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".m4a", ".ogg" };

    private readonly AudioDurationProbeOptions _options;
    private readonly ILogger<AudioDurationProbe> _logger;

    public AudioDurationProbe(IOptions<AudioDurationProbeOptions> options, ILogger<AudioDurationProbe> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AudioDurationProbeResult> ProbeDurationAsync(
        Stream audioStream, string fileExtension, CancellationToken ct = default)
    {
        if (!SupportedExtensions.Contains(fileExtension))
        {
            return AudioDurationProbeResult.Failed(
                AudioDurationProbeStatus.UnsupportedOrCorrupt,
                $"Unsupported audio extension '{fileExtension}' for duration measurement.");
        }

        if (ct.IsCancellationRequested)
            return AudioDurationProbeResult.Failed(AudioDurationProbeStatus.Cancelled, "Audio duration measurement was cancelled.");

        var tempFile = Path.Combine(Path.GetTempPath(), $"import-audio-probe-{Guid.NewGuid():N}{fileExtension}");
        try
        {
            await using (var fileOut = File.Create(tempFile))
                await audioStream.CopyToAsync(fileOut, ct);

            return await RunFfprobeAsync(tempFile, ct);
        }
        catch (OperationCanceledException)
        {
            return AudioDurationProbeResult.Failed(AudioDurationProbeStatus.Cancelled, "Audio duration measurement was cancelled.");
        }
        finally
        {
            TryDeleteTempFile(tempFile);
        }
    }

    private async Task<AudioDurationProbeResult> RunFfprobeAsync(string filePath, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.FfprobePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // ArgumentList — never a concatenated command-line string. No shell is invoked (UseShellExecute
        // is false), so this has no shell-injection surface regardless of file path contents.
        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("format=duration");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add(filePath);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "AudioDurationProbe: ffprobe could not be started at '{FfprobePath}'.", _options.FfprobePath);
            return AudioDurationProbeResult.Failed(
                AudioDurationProbeStatus.ToolUnavailable,
                $"ffprobe is not available at '{_options.FfprobePath}'. Configure {AudioDurationProbeOptions.SectionName}:FfprobePath.");
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stdErrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            if (ct.IsCancellationRequested)
                return AudioDurationProbeResult.Failed(AudioDurationProbeStatus.Cancelled, "Audio duration measurement was cancelled.");

            return AudioDurationProbeResult.Failed(
                AudioDurationProbeStatus.Timeout,
                $"ffprobe did not complete within {_options.TimeoutSeconds}s.");
        }

        string stdOut, stdErr;
        try
        {
            stdOut = await stdOutTask;
            stdErr = await stdErrTask;
        }
        catch (OperationCanceledException)
        {
            return AudioDurationProbeResult.Failed(AudioDurationProbeStatus.Cancelled, "Audio duration measurement was cancelled.");
        }

        if (process.ExitCode != 0)
        {
            return AudioDurationProbeResult.Failed(
                AudioDurationProbeStatus.UnsupportedOrCorrupt,
                $"ffprobe exited with code {process.ExitCode}: {Truncate(stdErr)}");
        }

        if (!TryParseDurationSeconds(stdOut, out var durationSeconds))
        {
            return AudioDurationProbeResult.Failed(
                AudioDurationProbeStatus.UnsupportedOrCorrupt,
                "ffprobe did not report a usable duration for this file.");
        }

        return AudioDurationProbeResult.Ok(durationSeconds);
    }

    private static bool TryParseDurationSeconds(string ffprobeJsonOutput, out decimal durationSeconds)
    {
        durationSeconds = 0m;
        try
        {
            using var doc = JsonDocument.Parse(ffprobeJsonOutput);
            if (!doc.RootElement.TryGetProperty("format", out var format)) return false;
            if (!format.TryGetProperty("duration", out var durationEl)) return false;
            var raw = durationEl.ValueKind == JsonValueKind.String ? durationEl.GetString() : durationEl.ToString();
            if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return false;
            if (parsed <= 0) return false;
            durationSeconds = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { /* best-effort — process may have already exited */ }
    }

    private static void TryDeleteTempFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup — a leftover temp file is not worth failing the probe over */ }
    }

    private static string Truncate(string value) => value.Length <= 500 ? value : value[..500] + "…";
}
