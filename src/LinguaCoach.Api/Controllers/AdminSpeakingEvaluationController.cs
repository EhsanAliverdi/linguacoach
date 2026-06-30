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
    /// ConfigStatus values: Disabled | NoOp | ProviderConfigured | ProviderUnsupported | DryRunOnly | Enabled
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
    /// Also exposes mastery signal config status and threshold values.
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
            ApplyMasterySignals: _options.ApplyMasterySignals,
            AllowReviewSignals: _options.AllowReviewSignals,
            AllowPositiveSignals: _options.AllowPositiveSignals,
            MinimumConfidenceRequired: _options.MinimumConfidenceForMasterySignal,
            MinPositiveOverall: _options.MinimumOverallScoreForPositiveSignal,
            MinReviewOverallMax: _options.MaximumOverallScoreForReviewSignal,
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

    /// <summary>
    /// Returns invariant safety verification summary.
    /// Confirms CEFR updates, objective completions, and LP auto-regen are structurally disabled.
    /// Any invariant violation here indicates a configuration or code defect.
    /// </summary>
    [HttpGet("signal-safety-summary")]
    public async Task<ActionResult<AdminSignalSafetySummaryDto>> GetSignalSafetySummary(
        CancellationToken ct = default)
    {
        var summary = await _signalService.GetSignalSafetySummaryAsync(ct);
        return Ok(new AdminSignalSafetySummaryDto(
            CefrUpdatesDisabled: summary.CefrUpdatesDisabled,
            ObjectiveCompletionsDisabled: summary.ObjectiveCompletionsDisabled,
            LearningPlanAutoRegenDisabled: summary.LearningPlanAutoRegenDisabled,
            SignalApplicationEnabled: summary.SignalApplicationEnabled,
            PositiveSignalsEnabled: summary.PositiveSignalsEnabled,
            ReviewSignalsEnabled: summary.ReviewSignalsEnabled,
            TotalApplied: summary.TotalApplied,
            PositiveApplied: summary.PositiveApplied,
            ReviewApplied: summary.ReviewApplied,
            InvariantViolationsDetected: summary.InvariantViolationsDetected));
    }

    private string ResolveConfigStatus()
    {
        var isNoOp = _options.Provider.Equals("NoOp", StringComparison.OrdinalIgnoreCase);

        if (!_options.Enabled && isNoOp) return "Disabled";
        if (!_options.Enabled && !isNoOp) return "ProviderConfigured";
        if (_options.Enabled && isNoOp) return "NoOp";
        if (_options.Enabled && !_provider.IsSupported) return "ProviderUnsupported";
        if (_options.Enabled && !_options.ApplyMasterySignals) return "DryRunOnly";
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
    // Phase 16J — mastery signal config and thresholds
    bool ApplyMasterySignals,
    bool AllowReviewSignals,
    bool AllowPositiveSignals,
    string MinimumConfidenceRequired,
    double MinPositiveOverall,
    double MinReviewOverallMax,
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

public sealed record AdminSignalSafetySummaryDto(
    bool CefrUpdatesDisabled,
    bool ObjectiveCompletionsDisabled,
    bool LearningPlanAutoRegenDisabled,
    bool SignalApplicationEnabled,
    bool PositiveSignalsEnabled,
    bool ReviewSignalsEnabled,
    int TotalApplied,
    int PositiveApplied,
    int ReviewApplied,
    bool InvariantViolationsDetected);
