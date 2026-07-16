using System.IO.Compression;
using System.Text.Json;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Phase 4 (2026-07-15), Part 8 — package processing pipeline. Only ever touches a package
// that already has an Approved plan (package.ApprovedImportProfileId set); everything here is
// the "execution" half of the plan/approval gate built earlier this phase. Two checkpointed
// stages: Extract (materialize ImportAsset rows + copy each file to its own storage object) and
// Map/CreateCandidates (structured files reuse the existing IResourceImportService pipeline;
// audio/transcript pairs become Listening candidates directly, using real STT for missing
// transcripts). Cost is tracked as it accrues and compared against the plan's approved ceiling
// before each STT call and each AI-enrichment batch — a projected overspend pauses the plan
// rather than continuing past the ceiling. ──

internal sealed class ImportPackageProcessingService : IImportPackageProcessingService
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".m4a", ".ogg" };
    private static readonly HashSet<string> TranscriptExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".md" };
    private static readonly HashSet<string> CsvExtensions = new(StringComparer.OrdinalIgnoreCase) { ".csv" };
    private static readonly HashSet<string> JsonExtensions = new(StringComparer.OrdinalIgnoreCase) { ".json" };
    private static readonly HashSet<string> JsonlExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jsonl" };

    private const int StageExtract = 0;
    private const int StageMapAndCreateCandidates = 1;

    private readonly LinguaCoachDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IResourceImportService _resourceImportService;
    private readonly IResourceCandidateBatchAnalysisService _batchAnalysisService;
    private readonly ISpeechToTextService _sttService;
    private readonly IActivityContentFingerprintService _fingerprint;
    private readonly IAiPricingResolver _pricingResolver;
    private readonly INotificationService _notifications;
    private readonly IApprovedImportProfileResolver _profileResolver;
    private readonly IImportSttOperationLedger _sttLedger;
    private readonly ImportCostEstimationOptions _costOptions;
    private readonly ILogger<ImportPackageProcessingService> _logger;

    public ImportPackageProcessingService(
        LinguaCoachDbContext db,
        IFileStorageService storage,
        IResourceImportService resourceImportService,
        IResourceCandidateBatchAnalysisService batchAnalysisService,
        ISpeechToTextService sttService,
        IActivityContentFingerprintService fingerprint,
        IAiPricingResolver pricingResolver,
        INotificationService notifications,
        IApprovedImportProfileResolver profileResolver,
        IImportSttOperationLedger sttLedger,
        IOptions<ImportCostEstimationOptions> costOptions,
        ILogger<ImportPackageProcessingService> logger)
    {
        _db = db;
        _storage = storage;
        _resourceImportService = resourceImportService;
        _batchAnalysisService = batchAnalysisService;
        _sttService = sttService;
        _fingerprint = fingerprint;
        _pricingResolver = pricingResolver;
        _notifications = notifications;
        _profileResolver = profileResolver;
        _sttLedger = sttLedger;
        _costOptions = costOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ImportPackageProcessingOutcome>> ProcessPendingAsync(int maxPackages, CancellationToken ct = default)
    {
        // Ordered client-side (not via OrderBy in the query) — SQLite (used by every test project
        // per this codebase's convention) cannot translate ORDER BY on a DateTimeOffset column.
        var packages = (await _db.ImportPackages
            .Where(p => p.Status == ImportPackageStatus.Queued
                     || p.Status == ImportPackageStatus.Extracting
                     || p.Status == ImportPackageStatus.Mapping
                     || p.Status == ImportPackageStatus.CreatingCandidates)
            .Where(p => p.ApprovedImportProfileId != null)
            .ToListAsync(ct))
            .OrderBy(p => p.StartedAtUtc)
            .Take(maxPackages)
            .ToList();

        var outcomes = new List<ImportPackageProcessingOutcome>();
        foreach (var package in packages)
        {
            outcomes.Add(await ProcessOnePackageAsync(package, ct));
        }
        return outcomes;
    }

    private async Task<ImportPackageProcessingOutcome> ProcessOnePackageAsync(ImportPackage package, CancellationToken ct)
    {
        var plan = await _db.ImportProfiles.FirstOrDefaultAsync(p => p.Id == package.ApprovedImportProfileId, ct);
        if (plan is null || plan.Status is not (ImportProfileStatus.Approved or ImportProfileStatus.Executing))
        {
            // Not actually approved (or already paused) — never process without a live-approved plan.
            return new ImportPackageProcessingOutcome(package.Id, false, false, "No approved/executing plan for this package.");
        }

        if (plan.Status == ImportProfileStatus.Approved)
            plan.MarkExecuting();
        package.MoveToStatus(ImportPackageStatus.Extracting);
        await _db.SaveChangesAsync(ct);

        try
        {
            // Phase 4.3 — resolved once per processing pass, before Extract even runs (the resolver
            // validates against the manifest, which exists before extraction), so a malformed or
            // incomplete approved plan is rejected deterministically before any file is touched.
            var approvedProfile = await _profileResolver.ResolveAsync(package.Id, ct);

            if (package.LastCompletedStageIndex < StageExtract)
            {
                // Phase 4.2 — a package submitted via IImportPackageSubmissionService (pasted
                // text / loose files) has no archive; its ImportAsset rows were already created
                // and stored directly at submission time, so there is nothing to extract.
                if (!string.IsNullOrEmpty(package.ArchiveStorageKey))
                    await ExtractAssetsAsync(package, ct);

                package.CheckpointStage(StageExtract);
                await _db.SaveChangesAsync(ct);
            }

            package.MoveToStatus(ImportPackageStatus.CreatingCandidates);
            await _db.SaveChangesAsync(ct);

            if (package.LastCompletedStageIndex < StageMapAndCreateCandidates)
            {
                var pauseReason = await MapAndCreateCandidatesAsync(package, plan, approvedProfile, ct);
                if (pauseReason is not null)
                {
                    plan.PauseForCostApproval(pauseReason);
                    package.MoveToStatus(ImportPackageStatus.AwaitingMappingApproval);
                    await _db.SaveChangesAsync(ct);
                    await NotifyAsync(package, "Import processing paused — cost re-approval required",
                        pauseReason, NotificationSeverity.Warning, ct);
                    return new ImportPackageProcessingOutcome(package.Id, false, true, pauseReason);
                }

                package.CheckpointStage(StageMapAndCreateCandidates);
                await _db.SaveChangesAsync(ct);
            }

            package.Complete(DateTimeOffset.UtcNow);
            plan.MarkCompleted();
            await _db.SaveChangesAsync(ct);

            await NotifyAsync(package,
                package.Status == ImportPackageStatus.CompletedWithWarnings
                    ? "Import completed with warnings"
                    : "Import ready for review",
                $"\"{package.OriginalArchiveFileName}\" finished processing — " +
                $"{package.CandidatesCreatedCount:N0} candidate(s) created, {package.CandidatesFailedCount:N0} failed. " +
                "Review and approve/reject candidates in the usual import review workflow.",
                package.CandidatesFailedCount > 0 ? NotificationSeverity.Warning : NotificationSeverity.Success, ct);
                // Phase 4.2 fix — the prior deep link (`/admin/content/import/runs?packageId=...`)
                // did not match any Angular route (that route requires a :runId path segment, not
                // a query param) and silently fell through to the wildcard redirect. Falls back to
                // NotifyAsync's default (the plan page), which is always a valid route.

            return new ImportPackageProcessingOutcome(package.Id, true, false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportPackageProcessingService: package {PackageId} failed.", package.Id);
            package.MarkFailed($"Processing failed: {ex.GetType().Name}: {ex.Message}", DateTimeOffset.UtcNow);
            plan.MarkFailed($"Processing failed: {ex.Message}");
            await _db.SaveChangesAsync(ct);
            await NotifyAsync(package, "Import processing failed", ex.Message, NotificationSeverity.Error, ct);
            return new ImportPackageProcessingOutcome(package.Id, false, false, ex.Message);
        }
    }

    private async Task NotifyAsync(
        ImportPackage package, string title, string body, NotificationSeverity severity, CancellationToken ct,
        string? deepLinkUrl = null)
    {
        if (package.CreatedByUserId is not { } recipient) return;

        await _notifications.QueueInAppAsync(
            recipient, title, body, NotificationCategory.Admin, severity,
            deepLinkUrl: deepLinkUrl ?? $"/admin/content/import/packages/{package.Id}/plan", ct: ct);
    }

    // ── Stage 0 — extraction ────────────────────────────────────────────────────────────────

    private async Task ExtractAssetsAsync(ImportPackage package, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(package.ManifestJson) || string.IsNullOrEmpty(package.ArchiveStorageKey))
            throw new InvalidOperationException("Package has no manifest/archive to extract.");

        var manifest = JsonSerializer.Deserialize<ImportPackageManifest>(package.ManifestJson)!;

        var alreadyExtracted = await _db.ImportAssets
            .Where(a => a.ImportPackageId == package.Id)
            .Select(a => a.RelativePath)
            .ToListAsync(ct);
        var alreadyExtractedSet = new HashSet<string>(alreadyExtracted, StringComparer.OrdinalIgnoreCase);

        await using var archiveStream = await _storage.ReadAsync(package.ArchiveStorageKey, ct);
        Stream seekable = archiveStream;
        MemoryStream? bufferedCopy = null;
        if (!archiveStream.CanSeek)
        {
            bufferedCopy = new MemoryStream();
            await archiveStream.CopyToAsync(bufferedCopy, ct);
            bufferedCopy.Position = 0;
            seekable = bufferedCopy;
        }

        try
        {
            using var zip = new ZipArchive(seekable, ZipArchiveMode.Read, leaveOpen: true);
            var filesInspected = 0;

            foreach (var entry in manifest.Entries.Where(e => !e.IsSuspicious))
            {
                ct.ThrowIfCancellationRequested();
                filesInspected++;
                if (alreadyExtractedSet.Contains(entry.RelativePath)) continue;

                var zipEntry = zip.GetEntry(entry.RelativePath) ?? zip.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').TrimStart('/') == entry.RelativePath);
                if (zipEntry is null) continue;

                var storageKey = _storage.GenerateKey(package.CefrResourceSourceId.ToString(), "import-package-assets", entry.FileExtension);
                await using (var entryStream = zipEntry.Open())
                {
                    await _storage.SaveAsync(storageKey, entryStream, entry.DetectedMimeType ?? "application/octet-stream", ct);
                }

                var mediaType = DetectMediaType(entry.FileExtension);
                var asset = new ImportAsset(
                    package.Id, entry.FileName, entry.RelativePath, storageKey,
                    entry.DetectedMimeType ?? "application/octet-stream", mediaType, entry.FileExtension,
                    entry.UncompressedSizeBytes, entry.Checksum, DateTimeOffset.UtcNow, entry.CompressedSizeBytes);
                asset.MarkInspected();
                _db.ImportAssets.Add(asset);
            }

            package.UpdateProgress(filesProcessedCount: filesInspected);
        }
        finally
        {
            bufferedCopy?.Dispose();
        }
    }

    private static ImportAssetMediaType DetectMediaType(string extension) => ImportAssetClassification.Classify(extension).MediaType;

    // ── Stage 1 — mapping + candidate creation, cost-ceiling enforced ──────────────────────

    /// <summary>Returns a non-null pause reason if the approved cost ceiling was reached before
    /// all work completed; otherwise null (stage fully completed).</summary>
    private async Task<string?> MapAndCreateCandidatesAsync(
        ImportPackage package, ImportProfile plan, ApprovedImportExecutionProfile approvedProfile, CancellationToken ct)
    {
        var ceiling = plan.ApprovedCostCeiling ?? decimal.MaxValue;
        var tolerance = (decimal)_costOptions.CostCeilingToleranceFraction;
        // Phase 4.4 (Workstream B2/B9) — seeded from the package's durable AccruedCost, not zero:
        // a retry after a crash mid-package must not lose track of what was already spent, and the
        // ceiling check below must compare against real persisted spend, not an ephemeral total
        // that resets every processing pass.
        var runningCost = package.AccruedCost;
        var candidatesCreated = 0;
        var candidatesFailed = 0;

        var assets = await _db.ImportAssets
            .Where(a => a.ImportPackageId == package.Id && a.ProcessingState != ImportAssetProcessingState.Processed)
            .ToListAsync(ct);

        var assetsByFolder = assets.GroupBy(a => ImportExecutionGroupKey.ForRelativePath(a.RelativePath));

        // Phase 4.3 — routing, resource-type, and column mapping now come exclusively from the
        // approved plan's per-group instructions (see ApprovedImportProfileResolver), not from
        // this service's own extension/heuristic inference. Reuse the existing single-file
        // candidate-staging pipeline for structured data files — no need to reimplement CSV/
        // JSON/JSONL parsing/dedup/field-inference here.
        foreach (var asset in assets.Where(a =>
            CsvExtensions.Contains(a.FileExtension) || JsonExtensions.Contains(a.FileExtension) || JsonlExtensions.Contains(a.FileExtension)))
        {
            ct.ThrowIfCancellationRequested();

            var instruction = approvedProfile.ResolveForRelativePath(asset.RelativePath)
                ?? throw new ApprovedImportProfileResolutionException(
                    $"Approved plan for package '{package.Id}' has no instruction covering asset '{asset.RelativePath}'.");

            if (!instruction.Included)
            {
                asset.MarkProcessed();
                continue;
            }

            try
            {
                await using var fileStream = await _storage.ReadAsync(asset.StorageKey, ct);
                var mode = CsvExtensions.Contains(asset.FileExtension) ? ResourceImportMode.Csv
                    : JsonlExtensions.Contains(asset.FileExtension) ? ResourceImportMode.Jsonl
                    : ResourceImportMode.Json;
                var columnRenames = instruction.FieldMappings.Count > 0 ? instruction.FieldMappings : null;
                var result = await _resourceImportService.ImportAsync(new ResourceImportRequest(
                    package.CefrResourceSourceId, fileStream, asset.OriginalFileName, mode,
                    DefaultCandidateType: instruction.ResourceType,
                    ColumnRenames: columnRenames, ImportPackageId: package.Id), ct);

                candidatesCreated += result.SucceededCount;
                candidatesFailed += result.RejectedCount;
                asset.MarkProcessed();

                if (package.ProcessingMode != ImportProcessingMode.Direct && result.SucceededCount > 0)
                {
                    // Phase 4.4D — cost is no longer pre-accrued for the whole batch here. Each
                    // candidate's AI operation is claimed, ceiling-checked, and (on success)
                    // ledgered + accrued individually inside ResourceCandidateAnalysisService — the
                    // exact same "one save, never drifts apart" discipline the STT path already
                    // uses, just moved to the actual per-operation call site.
                    var analysisResult = await _batchAnalysisService.AnalyzePendingForRunAsync(result.RunId, ct);
                    // Re-sync the local running total with whatever AI enrichment actually
                    // accrued (per-candidate, inside ResourceCandidateAnalysisService) so the STT
                    // ceiling check later in this same pass compares against the true total.
                    runningCost = package.AccruedCost;
                    if (analysisResult.CeilingReached)
                    {
                        package.UpdateProgress(candidatesCreatedCount: candidatesCreated, candidatesFailedCount: candidatesFailed);
                        return analysisResult.PauseReason
                            ?? $"Projected cost reached the approved ${ceiling:N2} ceiling before AI enrichment for run {result.RunId} completed.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportPackageProcessingService: structured file {File} failed for package {PackageId}.", asset.OriginalFileName, package.Id);
                asset.MarkFailed(ex.Message);
                candidatesFailed++;
            }
        }

        // Audio/transcript pairing per folder — direct candidate creation (no generic row shape fits this).
        var run = await GetOrCreateAudioImportRunAsync(package, ct);

        foreach (var folderGroup in assetsByFolder)
        {
            var audioAssets = folderGroup.Where(a => AudioExtensions.Contains(a.FileExtension)).ToList();
            if (audioAssets.Count == 0) continue;

            var instruction = approvedProfile.ResolveForRelativePath(audioAssets[0].RelativePath)
                ?? throw new ApprovedImportProfileResolutionException(
                    $"Approved plan for package '{package.Id}' has no instruction covering asset '{audioAssets[0].RelativePath}'.");

            if (!instruction.Included)
            {
                foreach (var skipped in folderGroup) skipped.MarkProcessed();
                continue;
            }

            // Defense-in-depth — ApprovedImportProfileResolver already rejects an audio group
            // routed to anything but ListeningPassage against the manifest before execution
            // reaches here; this only guards a plan approved before that check existed.
            if (instruction.ResourceType is not null && instruction.ResourceType != ResourceCandidateType.ListeningPassage)
                throw new ApprovedImportProfileResolutionException(
                    $"Approved plan for package '{package.Id}', group '{instruction.GroupKey}': audio content routed to " +
                    $"'{instruction.ResourceType}' is an unsupported route.");

            var transcriptAssets = folderGroup.Where(a => TranscriptExtensions.Contains(a.FileExtension)).ToList();

            foreach (var audio in audioAssets)
            {
                ct.ThrowIfCancellationRequested();
                var stem = System.IO.Path.GetFileNameWithoutExtension(audio.RelativePath);
                var matchingTranscript = transcriptAssets.FirstOrDefault(t =>
                    string.Equals(System.IO.Path.GetFileNameWithoutExtension(t.RelativePath), stem, StringComparison.OrdinalIgnoreCase));

                string? transcriptText = null;
                var origin = MetadataOrigin.SourceMetadata;

                if (matchingTranscript is not null)
                {
                    await using var transcriptStream = await _storage.ReadAsync(matchingTranscript.StorageKey, ct);
                    using var reader = new StreamReader(transcriptStream);
                    transcriptText = await reader.ReadToEndAsync(ct);
                    matchingTranscript.MarkProcessed();
                }
                else
                {
                    const decimal assumedMinutes = 5m; // flat per-file assumption, see plan estimate's documented limitation
                    var sttCostIfCharged = assumedMinutes * _costOptions.SttCostPerMinute;
                    var projectedSttCost = runningCost + sttCostIfCharged;
                    if (projectedSttCost > ceiling * (1 + tolerance))
                    {
                        package.UpdateProgress(candidatesCreatedCount: candidatesCreated, candidatesFailedCount: candidatesFailed);
                        return $"Projected cost reached ${projectedSttCost:N2}, at or above the approved ${ceiling:N2} ceiling, before transcribing {audio.RelativePath}.";
                    }

                    // Phase 4.4 (Workstream B3/B4) — claim (or reuse, if this exact audio content
                    // already succeeded under this or a prior package attempt) a durable ledger
                    // entry before ever calling the provider, so a crash/retry can never re-call
                    // STT — or accrue cost — for content it already transcribed.
                    var logicalKey = ImportSttOperationKey.Compute(package.Id, audio.Id, audio.Checksum);
                    var claim = await _sttLedger.ClaimAsync(package.Id, plan.Id, audio.Id, logicalKey, "openai", assumedMinutes, ct);

                    if (claim.Outcome == ImportSttClaimOutcome.AlreadySucceeded)
                    {
                        transcriptText = claim.Operation.TranscriptText;
                        origin = MetadataOrigin.AITranscribed;
                        // No cost re-accrual and no provider call — this exact operation already
                        // succeeded and was already charged once.
                    }
                    else
                    {
                        await using var audioStream = await _storage.ReadAsync(audio.StorageKey, ct);
                        var sttResult = await _sttService.TranscribeAsync(
                            audioStream, new SpeechToTextOptions(audio.MimeType, "en"), ct);

                        if (sttResult.Success && !string.IsNullOrWhiteSpace(sttResult.Transcript))
                        {
                            await _sttLedger.MarkSucceededAsync(
                                claim.Operation, sttResult.Transcript, sttCostIfCharged, "USD",
                                _costOptions.SttCostPerMinute, sttResult.Provider, ct);
                            package.AccrueCost(sttCostIfCharged, "USD");
                            await _db.SaveChangesAsync(ct); // ledger row + package cost, one save — never drifts apart
                            runningCost = projectedSttCost;
                            transcriptText = sttResult.Transcript;
                            origin = MetadataOrigin.AITranscribed;
                        }
                        else
                        {
                            // Workstream B13 ("failed operations") — a failed call's actual
                            // provider-side usage/charge is unknown to this codebase (OpenAI's
                            // Whisper SDK result exposes no usage on failure); documented as a
                            // known limitation rather than guessed at — no cost is accrued for it.
                            await _sttLedger.MarkFailedAsync(
                                claim.Operation, sttResult.FailureReason ?? "STT provider returned no transcript.", ct);
                        }
                    }
                }

                try
                {
                    var candidate = CreateListeningCandidate(run.Id, audio, transcriptText, origin);
                    audio.MarkProcessed();

                    // Part D/Part 8 — preserve the candidate↔asset relationship so a later
                    // Resource Bank publish (and Lesson generation reading it) can trace the
                    // published item back to its source audio/transcript files.
                    _db.ImportCandidateAssetLinks.Add(new ImportCandidateAssetLink(candidate.Id, audio.Id, ImportAssetRole.Audio));
                    if (matchingTranscript is not null)
                        _db.ImportCandidateAssetLinks.Add(new ImportCandidateAssetLink(candidate.Id, matchingTranscript.Id, ImportAssetRole.Transcript));

                    candidatesCreated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ImportPackageProcessingService: could not create Listening candidate for {File}.", audio.RelativePath);
                    audio.MarkFailed(ex.Message);
                    candidatesFailed++;
                }
            }
        }

        if (run is not null)
        {
            run.Complete(candidatesCreated + candidatesFailed, candidatesCreated, candidatesFailed, 0, DateTimeOffset.UtcNow, null);
        }

        package.UpdateProgress(candidatesCreatedCount: candidatesCreated, candidatesFailedCount: candidatesFailed);
        return null;
    }

    private async Task<ResourceImportRun> GetOrCreateAudioImportRunAsync(ImportPackage package, CancellationToken ct)
    {
        var existing = await _db.ResourceImportRuns
            .Where(r => r.ImportPackageId == package.Id && r.FileName == "listening-assets")
            .FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;

        var run = new ResourceImportRun(
            package.CefrResourceSourceId, ResourceImportMode.Json, "listening-assets",
            Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, importPackageId: package.Id);
        _db.ResourceImportRuns.Add(run);
        await _db.SaveChangesAsync(ct);
        return run;
    }

    private ResourceCandidate CreateListeningCandidate(Guid runId, ImportAsset audio, string? transcriptText, MetadataOrigin transcriptOrigin)
    {
        var title = System.IO.Path.GetFileNameWithoutExtension(audio.RelativePath);
        var normalizedJson = JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["title"] = title,
            ["transcript"] = transcriptText,
        });

        var rawRecord = new ResourceRawRecord(
            runId, audio.Checksum, "en", "audio-asset", rawJson: normalizedJson);
        rawRecord.MarkParsed();
        _db.ResourceRawRecords.Add(rawRecord);

        var fingerprint = _fingerprint.ComputeFingerprint(new ActivityContentFingerprintRequest(
            normalizedJson, ActivityContentShape.Unknown, null, title));

        var candidate = new ResourceCandidate(
            rawRecord.Id, ResourceCandidateType.ListeningPassage, title, normalizedJson, "en",
            title.ToLowerInvariant(), fingerprint, ResourceCandidateValidationStatus.NeedsReview);
        candidate.AttachAudio(audio.StorageKey, audio.MimeType);
        if (!string.IsNullOrWhiteSpace(transcriptText))
        {
            if (transcriptOrigin == MetadataOrigin.AITranscribed)
                candidate.SetGeneratedTranscript(transcriptText, confidence: null, providerName: "openai", modelName: "whisper-1");
            else
                candidate.SetSuppliedTranscript(transcriptOrigin);
        }

        _db.ResourceCandidates.Add(candidate);
        return candidate;
    }
}
