using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using LinguaCoach.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Mandatory Import Execution Plan addendum (2026-07-15) — Parts 1-4. Pre-flight only: works
// purely from the already-built manifest (Part B), performs deterministic clustering + automatic
// representative sampling (no admin sample picking), and runs at most
// ImportCostEstimationOptions.MaxSamplingRounds bounded AI review calls over file *metadata*
// only (names/paths/sizes) — never file content, never per-file AI analysis, never STT/TTS.
// Persists a Draft -> AwaitingApproval ImportProfile row (the plan) with a full volume/time/cost
// estimate. Nothing past this point may run until an administrator explicitly approves it. ──

internal sealed class ImportExecutionPlanGenerationService : IImportExecutionPlanGenerationService
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".m4a", ".ogg" };
    private static readonly HashSet<string> TranscriptExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".md" };
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly HashSet<string> StructuredDataExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".csv", ".json" };

    private static readonly HashSet<string> StructuredMappingPreviewExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".csv", ".json", ".jsonl" };

    private readonly LinguaCoachDbContext _db;
    private readonly IZipPackageInspector _inspector; // unused directly, kept for future re-inspection hooks
    private readonly IImportProcessingModeDecisionService _modeDecision;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly IAiPricingResolver _pricingResolver;
    private readonly INotificationService _notifications;
    private readonly IFileStorageService _storage;
    private readonly IResourceImportService _resourceImportService;
    private readonly IResourceImportColumnMappingService _columnMappingService;
    private readonly ImportPackageLimitsOptions _limits;
    private readonly ImportCostEstimationOptions _costOptions;
    private readonly ILogger<ImportExecutionPlanGenerationService> _logger;

    public ImportExecutionPlanGenerationService(
        LinguaCoachDbContext db,
        IZipPackageInspector inspector,
        IImportProcessingModeDecisionService modeDecision,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        IAiPricingResolver pricingResolver,
        INotificationService notifications,
        IFileStorageService storage,
        IResourceImportService resourceImportService,
        IResourceImportColumnMappingService columnMappingService,
        IOptions<ImportPackageLimitsOptions> limits,
        IOptions<ImportCostEstimationOptions> costOptions,
        ILogger<ImportExecutionPlanGenerationService> logger)
    {
        _db = db;
        _inspector = inspector;
        _modeDecision = modeDecision;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _pricingResolver = pricingResolver;
        _notifications = notifications;
        _storage = storage;
        _resourceImportService = resourceImportService;
        _columnMappingService = columnMappingService;
        _limits = limits.Value;
        _costOptions = costOptions.Value;
        _logger = logger;
    }

    public async Task<ImportExecutionPlanDto> GenerateAsync(GenerateImportExecutionPlanCommand command, CancellationToken ct = default)
    {
        var package = await _db.ImportPackages.FirstOrDefaultAsync(p => p.Id == command.ImportPackageId, ct)
            ?? throw new ResourceImportValidationException("Import package not found.");

        if (string.IsNullOrEmpty(package.ManifestJson))
            throw new ResourceImportValidationException("Import package has no manifest yet — confirm the upload first.");

        var manifest = JsonSerializer.Deserialize<ImportPackageManifest>(package.ManifestJson)!;
        if (!manifest.IsAccepted)
            throw new ResourceImportValidationException("Cannot generate a plan for a rejected package.");

        var modeDecision = _modeDecision.Decide(manifest);
        package.SetProcessingMode(modeDecision.Mode, modeDecision.Reason);

        // ── Part 2/3 — deterministic clustering + automatic representative sampling (no admin
        // sample-picking step: the system always does this itself). ──
        var groups = BuildDeterministicGroups(manifest);
        var samples = SelectRepresentativeSamples(manifest, groups);

        var (aiGroupUpdates, ambiguousGroups, unsupportedNotes, structureConfidence, roundsUsed) =
            await RunBoundedAiReviewAsync(manifest, groups, samples, ct);

        foreach (var update in aiGroupUpdates)
        {
            var idx = groups.FindIndex(g => g.GroupKey == update.GroupKey);
            if (idx >= 0) groups[idx] = update;
        }

        var volume = BuildVolumeEstimate(manifest, groups);
        var time = BuildTimeEstimate(volume);
        var cost = await BuildCostEstimateAsync(volume, modeDecision.Mode, ct);
        var risks = BuildRisks(manifest, ambiguousGroups, unsupportedNotes, volume);
        var decisions = BuildProposedDecisions(volume);

        // ── Part F — a real structured-file mapping preview. Only buildable for inline
        // (non-ZIP) packages: those are the only ones whose ImportAsset rows already exist at
        // plan-generation time (a ZIP package's assets aren't materialized until the approved
        // plan's Extract stage runs). ZIP packages keep their pre-existing behavior unchanged. ──
        var structuredMappingPreviews = string.IsNullOrEmpty(package.ArchiveStorageKey)
            ? await BuildStructuredMappingPreviewsAsync(package.Id, ct)
            : Array.Empty<ImportExecutionPlanStructuredMappingPreview>();

        var estimate = new ImportExecutionPlanEstimate(
            groups, ambiguousGroups, unsupportedNotes, volume, time, cost, risks, decisions,
            roundsUsed, structureConfidence, structuredMappingPreviews);

        // ── Versioning (Part 7) — supersede any prior not-yet-executed plan for this package. ──
        var priorPlans = await _db.ImportProfiles
            .Where(p => p.ImportPackageId == package.Id)
            .OrderByDescending(p => p.Version)
            .ToListAsync(ct);

        var priorLive = priorPlans.FirstOrDefault(p =>
            p.Status is ImportProfileStatus.Draft or ImportProfileStatus.AwaitingApproval or ImportProfileStatus.Approved);
        priorLive?.Supersede();

        var nextVersion = priorPlans.Count == 0 ? 1 : priorPlans.Max(p => p.Version) + 1;
        var changeReason = nextVersion == 1 ? null : (command.ChangeReason ?? "Plan regenerated from an updated package sample.");

        var pricingSnapshot = JsonSerializer.Serialize(new
        {
            _costOptions.AssumedAiProviderName,
            _costOptions.AssumedAiModelName,
            _costOptions.SttCostPerMinute,
            _costOptions.TtsCostPerThousandCharacters,
            _costOptions.ImageAnalysisCostPerImage,
            _costOptions.CostRangeUncertaintyFraction,
            SnapshotAtUtc = DateTimeOffset.UtcNow,
        });

        var groupInstructions = BuildGroupInstructions(groups, structuredMappingPreviews);

        var plan = new ImportProfile(
            package.Id,
            nextVersion,
            profileJson: JsonSerializer.Serialize(groupInstructions),
            sampleAssetIds: Array.Empty<Guid>(),
            estimatedCandidateCount: volume.ExpectedCandidateCount,
            createdAtUtc: DateTimeOffset.UtcNow,
            aiProviderName: _costOptions.AssumedAiProviderName,
            aiModelName: _costOptions.AssumedAiModelName,
            estimatedCostExpected: cost.ExpectedCost,
            estimatedCostMin: cost.MinCost,
            estimatedCostMax: cost.MaxCost,
            currency: cost.Currency,
            planEstimateJson: JsonSerializer.Serialize(estimate),
            pricingSnapshotJson: pricingSnapshot,
            changeReason: changeReason);

        plan.SubmitForApproval();

        _db.ImportProfiles.Add(plan);
        package.MoveToStatus(ImportPackageStatus.AwaitingMappingApproval);
        await _db.SaveChangesAsync(ct);

        if (package.CreatedByUserId is { } createdBy)
        {
            await _notifications.QueueInAppAsync(
                createdBy,
                "Import plan ready for review",
                $"An Import Execution Plan for \"{package.OriginalArchiveFileName}\" is ready — " +
                $"estimated cost ${cost.ExpectedCost:N2} (range ${cost.MinCost:N2}–${cost.MaxCost:N2}), " +
                $"{volume.ExpectedCandidateCount:N0} candidate(s) expected. Review and approve to start processing.",
                NotificationCategory.Admin, NotificationSeverity.Info,
                deepLinkUrl: $"/admin/content/import/packages/{package.Id}/plan", ct: ct);
        }

        return ToDto(package, plan, estimate);
    }

    // ── Phase 4.3 — the typed, frozen execution contract ProfileJson now persists. Built once per
    // plan generation from the same deterministic/AI-reviewed groups the admin sees in the plan
    // review UI, plus (for inline/non-ZIP packages only — see BuildStructuredMappingPreviewsAsync's
    // limitation) each group's aggregated column-mapping preview. Every group defaults to
    // Included=true; there is no admin exclusion UI yet (Phase 4.3 scope), but ImportProfile stays
    // Draft-editable via ReplaceProfileJson for a future one, or for a script/test to construct a
    // deliberately different approved plan. ──

    private static List<ImportExecutionGroupInstruction> BuildGroupInstructions(
        List<ImportExecutionPlanDetectedGroup> groups,
        IReadOnlyList<ImportExecutionPlanStructuredMappingPreview> structuredMappingPreviews)
    {
        return groups.Select(group =>
        {
            var fieldMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var preview in structuredMappingPreviews)
            {
                if (!string.Equals(ImportExecutionGroupKey.ForRelativePath(preview.AssetRelativePath), group.GroupKey, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var kv in preview.ProposedMapping)
                    fieldMappings.TryAdd(kv.Key, kv.Value);
            }

            return new ImportExecutionGroupInstruction(
                GroupKey: group.GroupKey,
                Included: true,
                ResourceType: group.ProposedResourceType,
                FieldMappings: fieldMappings,
                SampleRelativePaths: group.SampleRelativePaths);
        }).ToList();
    }

    // ── Part 2/3 — deterministic clustering ─────────────────────────────────────────────────

    private static List<ImportExecutionPlanDetectedGroup> BuildDeterministicGroups(ImportPackageManifest manifest)
    {
        var groups = new List<ImportExecutionPlanDetectedGroup>();

        foreach (var folder in manifest.FolderGroups)
        {
            var entriesInFolder = manifest.Entries
                .Where(e => (System.IO.Path.GetDirectoryName(e.RelativePath)?.Replace('\\', '/') ?? string.Empty) == folder.FolderPath)
                .ToList();

            var audioCount = entriesInFolder.Count(e => AudioExtensions.Contains(e.FileExtension));
            var transcriptCount = entriesInFolder.Count(e => TranscriptExtensions.Contains(e.FileExtension));
            var imageCount = entriesInFolder.Count(e => ImageExtensions.Contains(e.FileExtension));
            var structuredCount = entriesInFolder.Count(e => StructuredDataExtensions.Contains(e.FileExtension));

            ResourceCandidateType? proposedType = null;
            string description;
            if (audioCount > 0 && transcriptCount > 0)
            {
                proposedType = ResourceCandidateType.ListeningPassage;
                description = $"{audioCount} audio file(s) with {transcriptCount} transcript file(s) — likely Listening candidates.";
            }
            else if (audioCount > 0)
            {
                proposedType = ResourceCandidateType.ListeningPassage;
                description = $"{audioCount} audio file(s) with no matching transcript — Listening candidates requiring transcription.";
            }
            else if (imageCount > 0)
            {
                description = $"{imageCount} image file(s) — possible Speaking image-description prompts, pending sample review.";
            }
            else if (structuredCount == entriesInFolder.Count && structuredCount > 0)
            {
                description = $"{structuredCount} structured data file(s) (CSV/JSON) — deterministic column mapping likely applies.";
            }
            else
            {
                description = $"{entriesInFolder.Count} file(s) of type(s) {string.Join(", ", folder.Extensions)} — resource type pending sample review.";
            }

            groups.Add(new ImportExecutionPlanDetectedGroup(
                GroupKey: string.IsNullOrEmpty(folder.FolderPath) ? "(root)" : folder.FolderPath,
                Description: description,
                FileCount: folder.FileCount,
                SampleRelativePaths: entriesInFolder.Take(3).Select(e => e.RelativePath).ToList(),
                ProposedResourceType: proposedType,
                Confidence: proposedType is not null ? 0.7 : 0.3));
        }

        return groups;
    }

    private List<ImportPackageManifestEntry> SelectRepresentativeSamples(
        ImportPackageManifest manifest, List<ImportExecutionPlanDetectedGroup> groups)
    {
        var selected = new List<ImportPackageManifestEntry>();
        foreach (var group in groups)
        {
            var entriesInGroup = manifest.Entries
                .Where(e => (System.IO.Path.GetDirectoryName(e.RelativePath)?.Replace('\\', '/') ?? string.Empty) == group.GroupKey
                    || (group.GroupKey == "(root)" && string.IsNullOrEmpty(System.IO.Path.GetDirectoryName(e.RelativePath))))
                .OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (entriesInGroup.Count == 0) continue;

            var picks = new List<ImportPackageManifestEntry> { entriesInGroup[0] };
            if (entriesInGroup.Count > 2) picks.Add(entriesInGroup[entriesInGroup.Count / 2]);
            if (entriesInGroup.Count > 1) picks.Add(entriesInGroup[^1]);
            picks.Add(entriesInGroup.OrderByDescending(e => e.UncompressedSizeBytes).First());
            picks.Add(entriesInGroup.OrderBy(e => e.UncompressedSizeBytes).First());

            selected.AddRange(picks.Distinct().Take(_limits.SampleMaxFilesPerGroup));
            if (selected.Count >= _limits.SampleMaxTotalFiles) break;
        }

        return selected.Distinct().Take(_limits.SampleMaxTotalFiles).ToList();
    }

    // ── Part 2 — bounded AI review round(s) over metadata only ─────────────────────────────

    private async Task<(List<ImportExecutionPlanDetectedGroup> Updates, List<string> Ambiguous, List<string> Unsupported, double Confidence, int RoundsUsed)>
        RunBoundedAiReviewAsync(
            ImportPackageManifest manifest,
            List<ImportExecutionPlanDetectedGroup> groups,
            List<ImportPackageManifestEntry> samples,
            CancellationToken ct)
    {
        var round = 0;
        var maxRounds = Math.Max(1, _costOptions.MaxSamplingRounds);
        List<ImportExecutionPlanDetectedGroup> updates = new();
        List<string> ambiguous = new();
        List<string> unsupported = new();
        double confidence = 0.5;

        while (round < maxRounds)
        {
            round++;
            var variables = new Dictionary<string, string>
            {
                ["fileCount"] = manifest.EntryCount.ToString(),
                ["distinctExtensions"] = string.Join(", ", manifest.DistinctExtensions),
                ["detectedGroupsJson"] = JsonSerializer.Serialize(groups.Select(g => new { g.GroupKey, g.Description, g.FileCount, g.SampleRelativePaths })),
                ["sampleMetadataJson"] = JsonSerializer.Serialize(samples.Select(s => new { s.RelativePath, s.FileExtension, s.UncompressedSizeBytes })),
                ["samplingRound"] = round.ToString(),
                ["maxSamplingRounds"] = maxRounds.ToString(),
            };

            var correlationId = Guid.NewGuid().ToString("N")[..16];
            try
            {
                var request = await _contextBuilder.BuildAsync(DefaultAiSeeder.ImportPackagePlanReviewKey, variables, ct);
                var result = await _aiExecution.ExecuteWithMetaAsync(
                    DefaultAiSeeder.ImportPackagePlanReviewKey, request, null, correlationId, ct);

                var parsed = TryParsePlanReview(result.ResponseJson, groups);
                if (parsed is null) break;

                updates = parsed.Value.Groups;
                ambiguous = parsed.Value.Ambiguous;
                unsupported = parsed.Value.Unsupported;
                confidence = parsed.Value.Confidence;

                if (!parsed.Value.NeedsAnotherRound || round >= maxRounds) break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "AI plan review unavailable for package structure analysis (non-blocking); falling back to deterministic groups.");
                unsupported.Add("AI structural review was unavailable — plan is based on deterministic clustering only.");
                break;
            }
        }

        return (updates, ambiguous, unsupported, confidence, round);
    }

    private static (List<ImportExecutionPlanDetectedGroup> Groups, List<string> Ambiguous, List<string> Unsupported, double Confidence, bool NeedsAnotherRound)?
        TryParsePlanReview(string rawResponse, List<ImportExecutionPlanDetectedGroup> baseGroups)
    {
        try
        {
            var cleaned = rawResponse.Trim();
            if (cleaned.StartsWith("```"))
            {
                var firstNewline = cleaned.IndexOf('\n');
                var lastFence = cleaned.LastIndexOf("```");
                if (firstNewline > 0 && lastFence > firstNewline)
                    cleaned = cleaned[(firstNewline + 1)..lastFence].Trim();
            }

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var updated = new List<ImportExecutionPlanDetectedGroup>(baseGroups);
            if (root.TryGetProperty("groups", out var groupsEl) && groupsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var g in groupsEl.EnumerateArray())
                {
                    var key = g.TryGetProperty("groupKey", out var k) ? k.GetString() : null;
                    if (key is null) continue;
                    var idx = updated.FindIndex(x => x.GroupKey == key);
                    if (idx < 0) continue;

                    var typeStr = g.TryGetProperty("proposedResourceType", out var t) ? t.GetString() : null;
                    ResourceCandidateType? type = typeStr is not null && Enum.TryParse<ResourceCandidateType>(typeStr, out var parsedType)
                        ? parsedType : updated[idx].ProposedResourceType;
                    var description = g.TryGetProperty("description", out var d) ? d.GetString() ?? updated[idx].Description : updated[idx].Description;
                    var conf = g.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var cd) ? Math.Clamp(cd, 0, 1) : updated[idx].Confidence;

                    updated[idx] = updated[idx] with { ProposedResourceType = type, Description = description, Confidence = conf };
                }
            }

            var ambiguous = root.TryGetProperty("ambiguousGroups", out var a) && a.ValueKind == JsonValueKind.Array
                ? a.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList()
                : new List<string>();
            var unsupported = root.TryGetProperty("unsupportedContentNotes", out var u) && u.ValueKind == JsonValueKind.Array
                ? u.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToList()
                : new List<string>();
            var confidence = root.TryGetProperty("structureConfidence", out var sc) && sc.TryGetDouble(out var scv) ? Math.Clamp(scv, 0, 1) : 0.5;
            var needsAnother = root.TryGetProperty("needsAnotherSamplingRound", out var na) && na.ValueKind == JsonValueKind.True;

            return (updated, ambiguous, unsupported, confidence, needsAnother);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ── Part F — structured file mapping preview (inline packages only) ────────────────────────

    private async Task<IReadOnlyList<ImportExecutionPlanStructuredMappingPreview>> BuildStructuredMappingPreviewsAsync(
        Guid packageId, CancellationToken ct)
    {
        var assets = await _db.ImportAssets
            .Where(a => a.ImportPackageId == packageId && StructuredMappingPreviewExtensions.Contains(a.FileExtension))
            .ToListAsync(ct);

        var previews = new List<ImportExecutionPlanStructuredMappingPreview>();
        foreach (var asset in assets)
        {
            try
            {
                await using var stream = await _storage.ReadAsync(asset.StorageKey, ct);
                using var reader = new StreamReader(stream);
                var fileText = await reader.ReadToEndAsync(ct);

                var mode = asset.FileExtension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ? ResourceImportMode.Csv
                    : asset.FileExtension.Equals(".jsonl", StringComparison.OrdinalIgnoreCase) ? ResourceImportMode.Jsonl
                    : ResourceImportMode.Json;

                var sample = _resourceImportService.ParseSample(fileText, mode, sampleSize: 5);
                var warnings = new List<string>();

                var mapping = new Dictionary<string, string>();
                var ignored = new List<string>();
                try
                {
                    var proposal = await _columnMappingService.ProposeMappingAsync(
                        new ResourceImportColumnMappingRequest(sample.Columns, sample.SampleRows), ct);
                    foreach (var s in proposal.Suggestions)
                    {
                        if (s.SuggestedField is not null) mapping[s.SourceColumn] = s.SuggestedField;
                        else ignored.Add(s.SourceColumn);
                    }
                    if (!proposal.Success)
                        warnings.Add("AI mapping suggestion was unavailable — columns will be read using their own names as-is.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Structured mapping preview: AI proposal failed for asset {AssetId} (non-blocking).", asset.Id);
                    warnings.Add("AI mapping suggestion was unavailable — columns will be read using their own names as-is.");
                }

                previews.Add(new ImportExecutionPlanStructuredMappingPreview(
                    asset.RelativePath, sample.Columns, mapping, ignored, sample.SampleRows.Count, warnings));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Structured mapping preview: could not parse asset {AssetId} (non-blocking).", asset.Id);
                previews.Add(new ImportExecutionPlanStructuredMappingPreview(
                    asset.RelativePath, Array.Empty<string>(), new Dictionary<string, string>(), Array.Empty<string>(), 0,
                    new[] { $"Could not parse this file for a mapping preview: {ex.Message}" }));
            }
        }

        return previews;
    }

    // ── Part 4 — volume / time / cost estimation ────────────────────────────────────────────

    private ImportExecutionPlanVolumeEstimate BuildVolumeEstimate(
        ImportPackageManifest manifest, List<ImportExecutionPlanDetectedGroup> groups)
    {
        var byExtension = manifest.Entries
            .GroupBy(e => e.FileExtension, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var audioEntries = manifest.Entries.Where(e => AudioExtensions.Contains(e.FileExtension)).ToList();
        var transcriptStems = manifest.Entries
            .Where(e => TranscriptExtensions.Contains(e.FileExtension))
            .Select(e => System.IO.Path.GetFileNameWithoutExtension(e.RelativePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var audioMissingTranscript = audioEntries
            .Count(e => !transcriptStems.Contains(System.IO.Path.GetFileNameWithoutExtension(e.RelativePath)));

        var imageCount = manifest.Entries.Count(e => ImageExtensions.Contains(e.FileExtension));

        // Candidate-count estimate is a coarse proxy (one candidate per non-metadata content
        // file) — no per-record row parsing happens during planning, per the pre-flight-only
        // constraint. Explicitly labeled as an estimate, never shown as an exact count.
        var expectedCandidateCount = manifest.Entries.Count(e =>
            AudioExtensions.Contains(e.FileExtension) ||
            StructuredDataExtensions.Contains(e.FileExtension) ||
            (TranscriptExtensions.Contains(e.FileExtension) && !AudioExtensions.Any(ext =>
                transcriptStems.Contains(System.IO.Path.GetFileNameWithoutExtension(e.RelativePath)))));
        if (expectedCandidateCount == 0) expectedCandidateCount = manifest.EntryCount;

        return new ImportExecutionPlanVolumeEstimate(
            manifest.EntryCount,
            byExtension,
            expectedCandidateCount,
            audioMissingTranscript,
            audioMissingTranscript * 5.0, // assumed average minutes/file — see AssumedAudioMinutesPerFile note below
            ExpectedTtsCandidates: 0, // TTS is opt-in only; default plan never assumes automatic TTS
            EstimatedTtsCharacters: 0,
            ExpectedImageAnalysisCount: imageCount,
            UnmatchedFileCount: manifest.UnsupportedEntries.Count + manifest.SuspiciousEntries.Count);
    }

    private ImportExecutionPlanTimeEstimate BuildTimeEstimate(ImportExecutionPlanVolumeEstimate volume)
    {
        var expectedMinutes = volume.ExpectedCandidateCount / Math.Max(_costOptions.AssumedCandidatesPerMinute, 0.1);
        var minMinutes = expectedMinutes * (1 - _costOptions.CostRangeUncertaintyFraction);
        var maxMinutes = expectedMinutes * (1 + _costOptions.CostRangeUncertaintyFraction);

        return new ImportExecutionPlanTimeEstimate(
            $"Approximately {minMinutes:N0}-{maxMinutes:N0} minutes of background processing once approved (excludes upload/queue time).",
            minMinutes, maxMinutes,
            $"Assumes ~{_costOptions.AssumedCandidatesPerMinute:N0} candidates processed per minute; a rough throughput assumption, not a measured rate.");
    }

    private async Task<ImportExecutionPlanCostEstimate> BuildCostEstimateAsync(
        ImportExecutionPlanVolumeEstimate volume, ImportProcessingMode mode, CancellationToken ct)
    {
        var breakdown = new List<ImportExecutionPlanCostBreakdownLine>();
        var assumptions = new List<string>();

        decimal aiCost = 0m;
        if (mode != ImportProcessingMode.Direct)
        {
            var pricing = await _pricingResolver.ResolveAsync(
                _costOptions.AssumedAiProviderName, _costOptions.AssumedAiModelName, ct);
            var inputPer1K = pricing?.InputPer1KTokens ?? 0m;
            var outputPer1K = pricing?.OutputPer1KTokens ?? 0m;

            var totalInputTokens = (long)volume.ExpectedCandidateCount * _costOptions.AssumedInputTokensPerCandidate;
            var totalOutputTokens = (long)volume.ExpectedCandidateCount * _costOptions.AssumedOutputTokensPerCandidate;
            aiCost = totalInputTokens / 1000m * inputPer1K + totalOutputTokens / 1000m * outputPer1K;
            assumptions.Add($"{volume.ExpectedCandidateCount:N0} candidates x ~{_costOptions.AssumedInputTokensPerCandidate + _costOptions.AssumedOutputTokensPerCandidate:N0} tokens each.");
        }
        breakdown.Add(new ImportExecutionPlanCostBreakdownLine("AI structuring/enrichment", aiCost));

        var sttCost = (decimal)volume.EstimatedAudioMinutesRequiringStt * _costOptions.SttCostPerMinute;
        breakdown.Add(new ImportExecutionPlanCostBreakdownLine("STT (transcription)", sttCost));
        if (volume.ExpectedAudioFilesRequiringStt > 0)
            assumptions.Add($"{volume.ExpectedAudioFilesRequiringStt:N0} audio file(s) without a matching transcript, ~5 minutes each assumed.");

        var ttsCost = 0m; // opt-in only, always $0 in the auto-generated plan
        breakdown.Add(new ImportExecutionPlanCostBreakdownLine("TTS (disabled by default)", ttsCost));

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

    private static List<string> BuildRisks(
        ImportPackageManifest manifest, List<string> ambiguousGroups, List<string> unsupportedNotes,
        ImportExecutionPlanVolumeEstimate volume)
    {
        var risks = new List<string>();
        if (ambiguousGroups.Count > 0)
            risks.Add($"{ambiguousGroups.Count} group(s) could not be confidently classified and may need manual review.");
        if (manifest.SuspiciousEntries.Count > 0)
            risks.Add($"{manifest.SuspiciousEntries.Count} file(s) were flagged suspicious during inspection and will be skipped.");
        if (manifest.UnsupportedEntries.Count > 0)
            risks.Add($"{manifest.UnsupportedEntries.Count} file(s) have an unrecognized type and will be skipped.");
        if (manifest.DuplicateChecksumEntries.Count > 0)
            risks.Add($"{manifest.DuplicateChecksumEntries.Count} file(s) share a checksum with another file — likely duplicates.");
        if (volume.ExpectedAudioFilesRequiringStt > 0)
            risks.Add($"{volume.ExpectedAudioFilesRequiringStt} audio file(s) require transcription — actual duration (and cost) is unknown until processing.");
        risks.AddRange(unsupportedNotes);
        return risks;
    }

    private static List<ImportExecutionPlanDecision> BuildProposedDecisions(ImportExecutionPlanVolumeEstimate volume)
    {
        var decisions = new List<ImportExecutionPlanDecision>
        {
            new("Speech-to-text",
                volume.ExpectedAudioFilesRequiringStt > 0 ? "Transcribe missing transcripts" : "No STT needed",
                volume.ExpectedAudioFilesRequiringStt > 0
                    ? $"{volume.ExpectedAudioFilesRequiringStt} audio file(s) have no matching transcript."
                    : "Every audio file already has a matching transcript."),
            new("Text-to-speech", "Do not generate TTS automatically",
                "TTS is opt-in only per policy — the administrator may enable it explicitly before approval."),
            new("Unsupported files", "Skip unsupported files", "Unrecognized file types are excluded from candidate creation, not treated as a fatal error."),
        };
        return decisions;
    }

    private static ImportExecutionPlanDto ToDto(ImportPackage package, ImportProfile plan, ImportExecutionPlanEstimate estimate) =>
        new(
            plan.Id, package.Id, plan.Version, plan.Status, package.ProcessingMode, package.ProcessingModeReason,
            estimate, plan.ApprovedCostCeiling, plan.CreatedAtUtc, plan.ApprovedAtUtc, plan.ApprovedByUserId,
            plan.RejectedAtUtc, plan.RejectionReason, plan.PauseReason, plan.ChangeReason,
            plan.ConcurrencyStamp, plan.Status is ImportProfileStatus.Draft or ImportProfileStatus.AwaitingApproval,
            ImportPlanDtoHelpers.DeserializeGroupInstructionsSafe(plan.ProfileJson));
}
