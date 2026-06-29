using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Api.Controllers;

/// <summary>
/// Admin read-only visibility into the speaking evaluation pipeline configuration and quality metrics.
/// No mutations — configuration is controlled via appsettings / environment variables.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/speaking-evaluation")]
public sealed class AdminSpeakingEvaluationController : ControllerBase
{
    private readonly SpeakingEvaluationOptions _options;
    private readonly ISpeakingEvaluationProvider _provider;
    private readonly ISpeakingEvaluationQualityQuery _qualityQuery;

    public AdminSpeakingEvaluationController(
        IOptions<SpeakingEvaluationOptions> options,
        ISpeakingEvaluationProvider provider,
        ISpeakingEvaluationQualityQuery qualityQuery)
    {
        _options = options.Value;
        _provider = provider;
        _qualityQuery = qualityQuery;
    }

    /// <summary>
    /// Returns the current speaking evaluation configuration and provider status.
    /// ConfigStatus values: Disabled | NoOp | ProviderConfigured | ProviderUnsupported | Enabled
    /// </summary>
    [HttpGet("status")]
    public ActionResult<AdminSpeakingEvaluationStatusDto> GetStatus()
    {
        var configStatus = ResolveConfigStatus();
        var caps = _provider.Capabilities;

        return Ok(new AdminSpeakingEvaluationStatusDto(
            ConfigStatus: configStatus,
            ProviderName: _provider.ProviderName,
            Enabled: _options.Enabled,
            Model: _options.Model,
            TranscriptionModel: _options.TranscriptionModel,
            MaxBatchSize: _options.MaxBatchSize,
            MaxRetries: _options.MaxRetries,
            MaxAudioDurationSeconds: _options.MaxAudioDurationSeconds,
            MaxAudioSizeBytes: _options.MaxAudioSizeBytes,
            Capabilities: new AdminSpeakingCapabilitiesDto(
                caps.SupportsAudioInput,
                caps.SupportsTranscript,
                caps.SupportsFluencyScore,
                caps.SupportsPronunciationScore,
                caps.SupportsStructuredOutput)));
    }

    /// <summary>
    /// Returns quality metrics for all speaking evaluations and dry-run signal counts.
    /// Dry-run signals are never applied to mastery, CEFR, or Learning Plan progress.
    /// </summary>
    [HttpGet("quality-summary")]
    public async Task<ActionResult<AdminSpeakingEvaluationQualitySummaryDto>> GetQualitySummary(
        CancellationToken ct = default)
    {
        var configStatus = ResolveConfigStatus();
        var caps = _provider.Capabilities;
        var quality = await _qualityQuery.GetQualitySummaryAsync(ct);

        return Ok(new AdminSpeakingEvaluationQualitySummaryDto(
            ConfigStatus: configStatus,
            ProviderName: _provider.ProviderName,
            Enabled: _options.Enabled,
            SupportsTranscript: caps.SupportsTranscript,
            SupportsPronunciationScore: caps.SupportsPronunciationScore,
            Quality: quality));
    }

    private string ResolveConfigStatus()
    {
        var isNoOp = _options.Provider.Equals("NoOp", StringComparison.OrdinalIgnoreCase);

        if (!_options.Enabled && isNoOp) return "Disabled";
        if (!_options.Enabled && !isNoOp) return "ProviderConfigured";
        if (_options.Enabled && isNoOp) return "NoOp";
        if (_options.Enabled && !_provider.IsSupported) return "ProviderUnsupported";
        return "Enabled";
    }
}

public sealed record AdminSpeakingEvaluationStatusDto(
    string ConfigStatus,
    string ProviderName,
    bool Enabled,
    string Model,
    string TranscriptionModel,
    int MaxBatchSize,
    int MaxRetries,
    int MaxAudioDurationSeconds,
    long MaxAudioSizeBytes,
    AdminSpeakingCapabilitiesDto Capabilities);

public sealed record AdminSpeakingCapabilitiesDto(
    bool SupportsAudioInput,
    bool SupportsTranscript,
    bool SupportsFluencyScore,
    bool SupportsPronunciationScore,
    bool SupportsStructuredOutput);

public sealed record AdminSpeakingEvaluationQualitySummaryDto(
    string ConfigStatus,
    string ProviderName,
    bool Enabled,
    bool SupportsTranscript,
    bool SupportsPronunciationScore,
    SpeakingEvaluationQualitySummaryDto Quality);
