using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase 4.4 (Workstream A8/B7) — recalculates a plan's volume/time/cost estimate from a candidate
/// set of <see cref="ImportExecutionGroupInstruction"/>s, honouring each group's <c>Included</c>
/// flag (an excluded group contributes zero volume/cost) — used by <see cref="ImportPlanDraftService"/>
/// every time an admin edit could change what execution would actually do. Fails closed
/// (<see cref="ImportPricingUnavailableException"/>) when the package's processing mode requires
/// AI and no pricing is resolvable, rather than silently estimating $0 (the pre-4.4 behaviour in
/// <c>ImportExecutionPlanGenerationService.BuildCostEstimateAsync</c>, which this deliberately does
/// not repeat).
/// </summary>
internal sealed class ImportPlanEstimateService : IImportPlanEstimateService
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".m4a", ".ogg" };
    private static readonly HashSet<string> TranscriptExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".md" };
    private static readonly HashSet<string> StructuredDataExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".csv", ".json" };
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private readonly IAiPricingResolver _pricingResolver;
    private readonly ImportCostEstimationOptions _costOptions;

    public ImportPlanEstimateService(IAiPricingResolver pricingResolver, IOptions<ImportCostEstimationOptions> costOptions)
    {
        _pricingResolver = pricingResolver;
        _costOptions = costOptions.Value;
    }

    public async Task<ImportExecutionPlanEstimate> RecalculateAsync(
        ImportPackage package, IReadOnlyList<ImportExecutionGroupInstruction> instructions, CancellationToken ct = default)
    {
        ImportPackageManifest? manifest = null;
        if (!string.IsNullOrEmpty(package.ManifestJson))
        {
            try { manifest = JsonSerializer.Deserialize<ImportPackageManifest>(package.ManifestJson); }
            catch (JsonException) { /* absent manifest handled below as zero-volume */ }
        }

        var includedGroupKeys = instructions.Where(i => i.Included)
            .Select(i => i.GroupKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<ImportPackageManifestEntry> includedEntries = manifest is null
            ? Array.Empty<ImportPackageManifestEntry>()
            : manifest.Entries.Where(e => !e.IsSuspicious
                && includedGroupKeys.Contains(ImportExecutionGroupKey.ForRelativePath(e.RelativePath))).ToList();

        var detectedGroups = instructions.Select(i =>
        {
            var count = manifest is null ? 0 : manifest.Entries.Count(e =>
                !e.IsSuspicious && string.Equals(ImportExecutionGroupKey.ForRelativePath(e.RelativePath), i.GroupKey, StringComparison.OrdinalIgnoreCase));
            var description = i.Included
                ? $"{count} file(s), routed to {(i.ResourceType?.ToString() ?? "(inferred per row)")}."
                : $"{count} file(s) — excluded from this plan revision.";
            return new ImportExecutionPlanDetectedGroup(i.GroupKey, description, count, i.SampleRelativePaths, i.ResourceType, 1.0);
        }).ToList();

        var volume = BuildVolumeEstimate(includedEntries);
        var time = BuildTimeEstimate(volume);
        var hasStructuredFiles = includedEntries.Any(e => StructuredDataExtensions.Contains(e.FileExtension));
        var cost = await BuildCostEstimateAsync(volume, package.ProcessingMode ?? ImportProcessingMode.Direct, hasStructuredFiles, ct);

        return new ImportExecutionPlanEstimate(
            detectedGroups, Array.Empty<string>(), Array.Empty<string>(), volume, time, cost,
            Array.Empty<string>(), Array.Empty<ImportExecutionPlanDecision>(), SamplingRoundsUsed: 0, StructureConfidence: 1.0);
    }

    private static ImportExecutionPlanVolumeEstimate BuildVolumeEstimate(IReadOnlyList<ImportPackageManifestEntry> entries)
    {
        var byExtension = entries
            .GroupBy(e => e.FileExtension, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var audioEntries = entries.Where(e => AudioExtensions.Contains(e.FileExtension)).ToList();
        var transcriptStems = entries
            .Where(e => TranscriptExtensions.Contains(e.FileExtension))
            .Select(e => Path.GetFileNameWithoutExtension(e.RelativePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var audioMissingTranscript = audioEntries
            .Count(e => !transcriptStems.Contains(Path.GetFileNameWithoutExtension(e.RelativePath)));

        var imageCount = entries.Count(e => ImageExtensions.Contains(e.FileExtension));

        var expectedCandidateCount = entries.Count(e =>
            AudioExtensions.Contains(e.FileExtension) ||
            StructuredDataExtensions.Contains(e.FileExtension) ||
            (TranscriptExtensions.Contains(e.FileExtension) &&
                !transcriptStems.Contains(Path.GetFileNameWithoutExtension(e.RelativePath))));

        return new ImportExecutionPlanVolumeEstimate(
            entries.Count, byExtension, expectedCandidateCount, audioMissingTranscript,
            audioMissingTranscript * 5.0, ExpectedTtsCandidates: 0, EstimatedTtsCharacters: 0,
            ExpectedImageAnalysisCount: imageCount, UnmatchedFileCount: 0);
    }

    private ImportExecutionPlanTimeEstimate BuildTimeEstimate(ImportExecutionPlanVolumeEstimate volume)
    {
        var expectedMinutes = volume.ExpectedCandidateCount / Math.Max(_costOptions.AssumedCandidatesPerMinute, 0.1);
        var minMinutes = expectedMinutes * (1 - _costOptions.CostRangeUncertaintyFraction);
        var maxMinutes = expectedMinutes * (1 + _costOptions.CostRangeUncertaintyFraction);

        return new ImportExecutionPlanTimeEstimate(
            $"Approximately {minMinutes:N0}-{maxMinutes:N0} minutes of background processing once approved.",
            minMinutes, maxMinutes,
            $"Assumes ~{_costOptions.AssumedCandidatesPerMinute:N0} candidates processed per minute.");
    }

    private async Task<ImportExecutionPlanCostEstimate> BuildCostEstimateAsync(
        ImportExecutionPlanVolumeEstimate volume, ImportProcessingMode mode, bool hasStructuredFiles, CancellationToken ct)
    {
        var breakdown = new List<ImportExecutionPlanCostBreakdownLine>();
        var assumptions = new List<string>();

        decimal aiCost = 0m;
        if (mode != ImportProcessingMode.Direct && volume.ExpectedCandidateCount > 0 && hasStructuredFiles)
        {
            var pricing = await _pricingResolver.ResolveAsync(
                _costOptions.AssumedAiProviderName, _costOptions.AssumedAiModelName, ct);
            if (pricing is null)
                throw new ImportPricingUnavailableException(
                    _costOptions.AssumedAiProviderName, _costOptions.AssumedAiModelName, "AI candidate enrichment");

            var totalInputTokens = (long)volume.ExpectedCandidateCount * _costOptions.AssumedInputTokensPerCandidate;
            var totalOutputTokens = (long)volume.ExpectedCandidateCount * _costOptions.AssumedOutputTokensPerCandidate;
            aiCost = totalInputTokens / 1000m * pricing.InputPer1KTokens + totalOutputTokens / 1000m * pricing.OutputPer1KTokens;
            assumptions.Add($"{volume.ExpectedCandidateCount:N0} candidates x ~{_costOptions.AssumedInputTokensPerCandidate + _costOptions.AssumedOutputTokensPerCandidate:N0} tokens each.");
        }
        breakdown.Add(new ImportExecutionPlanCostBreakdownLine("AI structuring/enrichment", aiCost));

        var sttCost = (decimal)volume.EstimatedAudioMinutesRequiringStt * _costOptions.SttCostPerMinute;
        breakdown.Add(new ImportExecutionPlanCostBreakdownLine("STT (transcription)", sttCost));
        if (volume.ExpectedAudioFilesRequiringStt > 0)
            assumptions.Add($"{volume.ExpectedAudioFilesRequiringStt:N0} audio file(s) without a matching transcript, ~5 minutes each assumed.");

        breakdown.Add(new ImportExecutionPlanCostBreakdownLine("TTS (disabled by default)", 0m));

        var imageCost = volume.ExpectedImageAnalysisCount * _costOptions.ImageAnalysisCostPerImage;
        breakdown.Add(new ImportExecutionPlanCostBreakdownLine("Image analysis", imageCost));

        var expected = breakdown.Sum(b => b.Amount);
        var min = expected * (1 - (decimal)_costOptions.CostRangeUncertaintyFraction);
        var max = expected * (1 + (decimal)_costOptions.CostRangeUncertaintyFraction);

        return new ImportExecutionPlanCostEstimate(
            Math.Round(expected, 2), Math.Round(Math.Max(min, 0), 2), Math.Round(max, 2), "USD",
            breakdown, assumptions,
            $"Assumes provider={_costOptions.AssumedAiProviderName}, model={_costOptions.AssumedAiModelName}.");
    }
}
