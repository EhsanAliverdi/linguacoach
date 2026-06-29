using LinguaCoach.Application.Speaking;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

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
    private readonly ISpeakingEvaluationSignalApplicationService _signalService;

    public AdminSpeakingEvaluationController(
        IOptions<SpeakingEvaluationOptions> options,
        ISpeakingEvaluationProvider provider,
        ISpeakingEvaluationQualityQuery qualityQuery,
        ISpeakingEvaluationSignalApplicationService signalService)
    {
        _options = options.Value;
        _provider = provider;
        _qualityQuery = qualityQuery;
        _signalService = signalService;
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

    /// <summary>
    /// Returns the mastery signal integration status and counts: applied, dry-run, blocked.
    /// Shows ApplyMasterySignals config state and per-evaluation signal disposition.
    /// </summary>
    [HttpGet("applied-signals")]
    public async Task<ActionResult<AdminSpeakingAppliedSignalSummaryDto>> GetAppliedSignalSummary(
        CancellationToken ct = default)
    {
        var summary = await _signalService.GetSummaryAsync(ct);
        return Ok(new AdminSpeakingAppliedSignalSummaryDto(
            MasteryIntegrationEnabled: summary.MasteryIntegrationEnabled,
            ReviewSignalsAllowed: summary.ReviewSignalsAllowed,
            PositiveSignalsAllowed: summary.PositiveSignalsAllowed,
            ObjectiveCompletionAllowed: summary.ObjectiveCompletionAllowed,
            CefrUpdateAllowed: summary.CefrUpdateAllowed,
            MinimumConfidenceRequired: summary.MinimumConfidenceRequired,
            TotalCompletedEvaluations: summary.TotalCompletedEvaluations,
            CandidateSignals: summary.CandidateSignals,
            AppliedSignals: summary.AppliedSignals,
            BlockedByConfig: summary.BlockedByConfig,
            BlockedByConfidence: summary.BlockedByConfidence,
            BlockedBySignalType: summary.BlockedBySignalType,
            BlockedByFailedOrUnsupported: summary.BlockedByFailedOrUnsupported,
            BlockedByMissingScore: summary.BlockedByMissingScore,
            DuplicateSkipped: summary.DuplicateSkipped,
            NoSignal: summary.NoSignal,
            FailedApplication: summary.FailedApplication));
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

public sealed record AdminSpeakingAppliedSignalSummaryDto(
    bool MasteryIntegrationEnabled,
    bool ReviewSignalsAllowed,
    bool PositiveSignalsAllowed,
    bool ObjectiveCompletionAllowed,
    bool CefrUpdateAllowed,
    string MinimumConfidenceRequired,
    int TotalCompletedEvaluations,
    int CandidateSignals,
    int AppliedSignals,
    int BlockedByConfig,
    int BlockedByConfidence,
    int BlockedBySignalType,
    int BlockedByFailedOrUnsupported,
    int BlockedByMissingScore,
    int DuplicateSkipped,
    int NoSignal,
    int FailedApplication);
