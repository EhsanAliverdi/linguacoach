using System.IO.Compression;
using System.Security.Cryptography;
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

    /// <summary>Phase 4.8 — a stable per-process identifier for the durable claim/lease (see
    /// <c>ImportPackage.Claim</c>). Not a Guid regenerated per call — stable for the process's
    /// lifetime so admin diagnostics/logs can attribute a stuck claim to a specific node/process.</summary>
    private static readonly string WorkerId = $"{Environment.MachineName}:{Environment.ProcessId}";

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
    private readonly IImportAssetAudioDurationResolver _audioDurationResolver;
    private readonly ImportCostEstimationOptions _costOptions;
    private readonly ImportPackageLimitsOptions _limits;
    private readonly TimeSpan _leaseDuration;
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
        IImportAssetAudioDurationResolver audioDurationResolver,
        IOptions<ImportCostEstimationOptions> costOptions,
        IOptions<ImportPackageLimitsOptions> limits,
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
        _audioDurationResolver = audioDurationResolver;
        _costOptions = costOptions.Value;
        _limits = limits.Value;
        _leaseDuration = TimeSpan.FromMinutes(_limits.ClaimLeaseDurationMinutes);
        _logger = logger;
    }

    public async Task<IReadOnlyList<ImportPackageProcessingOutcome>> ProcessPendingAsync(int maxPackages, CancellationToken ct = default)
    {
        // Ordered client-side (not via OrderBy in the query) — SQLite (used by every test project
        // per this codebase's convention) cannot translate ORDER BY on a DateTimeOffset column.
        var candidates = (await _db.ImportPackages
            .Where(p => p.Status == ImportPackageStatus.Queued
                     || p.Status == ImportPackageStatus.Extracting
                     || p.Status == ImportPackageStatus.Mapping
                     || p.Status == ImportPackageStatus.CreatingCandidates)
            .Where(p => p.ApprovedImportProfileId != null)
            .ToListAsync(ct))
            .OrderBy(p => p.StartedAtUtc)
            .ToList();

        // Phase 4.8 — each candidate must be atomically claimed (see TryClaimAsync) before this
        // worker may touch it; a package another live worker already holds a lease on, or that
        // fails the claim due to a concurrent winner, is skipped this pass rather than processed
        // twice. Up to maxPackages *successfully claimed* packages are processed per call — a
        // skipped-due-to-claim-conflict package does not count against the budget.
        var outcomes = new List<ImportPackageProcessingOutcome>();
        foreach (var package in candidates)
        {
            if (outcomes.Count >= maxPackages) break;
            ct.ThrowIfCancellationRequested();

            if (!await TryClaimAsync(package, DateTimeOffset.UtcNow, ct))
            {
                _logger.LogInformation(
                    "ImportPackageProcessingService: package {PackageId} is claimed by another worker — skipping this pass.",
                    package.Id);
                continue;
            }

            try
            {
                outcomes.Add(await ProcessOnePackageAsync(package, ct));
            }
            finally
            {
                await ReleaseClaimAsync(package, ct);
            }
        }
        return outcomes;
    }

    /// <summary>Attempts to acquire the processing lease via a database-level optimistic
    /// concurrency check — see <c>ImportPackage.Claim</c> and the entity's <c>ConcurrencyStamp</c>
    /// doc comment. Two workers racing to claim the same package can both pass the in-memory
    /// <c>IsClaimable</c> check, but only one's <c>SaveChangesAsync</c> can win; the loser gets
    /// <see cref="DbUpdateConcurrencyException"/>, reloads the now-current row, and returns false.</summary>
    private async Task<bool> TryClaimAsync(ImportPackage package, DateTimeOffset nowUtc, CancellationToken ct)
    {
        if (!package.IsClaimable(nowUtc))
            return false;

        package.Claim(WorkerId, nowUtc, _leaseDuration);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await _db.Entry(package).ReloadAsync(ct);
            return false;
        }
    }

    /// <summary>Releases the lease after a processing pass (success, pause, or failure) so the
    /// package does not have to sit out the full lease duration before it can be picked up again
    /// (e.g. immediately after an admin resolves a cost-approval pause). Safe to call even if this
    /// worker's own claim was already superseded — a concurrency conflict here just means someone
    /// else's lease is now authoritative, which is not this worker's problem to fix.</summary>
    private async Task ReleaseClaimAsync(ImportPackage package, CancellationToken ct)
    {
        if (!string.Equals(package.ClaimedByWorkerId, WorkerId, StringComparison.Ordinal))
            return;

        package.ReleaseClaim();
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another process's expiry-recovery claim already superseded ours — nothing to release.
        }
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

            // Phase 4.8 — renew the lease between stages so a slow/large package's second stage
            // does not run past the original claim's expiry and invite a second worker to steal it.
            package.RenewClaim(WorkerId, DateTimeOffset.UtcNow, _leaseDuration);
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
            // Phase 4.8 — revalidate the whole-archive checksum against what was recorded at
            // upload/inspection time before trusting a single byte of it. A storage object mutated
            // between inspection and extraction (or a stale/tampered manifest) is rejected here
            // rather than silently extracted.
            if (!string.IsNullOrEmpty(package.ArchiveChecksum))
            {
                seekable.Position = 0;
                var actualChecksum = await ComputeStreamChecksumAsync(seekable, ct);
                if (!string.Equals(actualChecksum, package.ArchiveChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ImportPackageInspectionException(
                        $"Archive checksum mismatch at extraction time for package '{package.Id}': expected " +
                        $"{package.ArchiveChecksum}, computed {actualChecksum}. The stored archive may have " +
                        "changed since inspection — extraction aborted.");
                }
            }
            seekable.Position = 0;

            using var zip = new ZipArchive(seekable, ZipArchiveMode.Read, leaveOpen: true);
            var filesInspected = 0;

            // Phase 4.8 — entry-count ceiling re-checked against the live archive (not just the
            // stored manifest) before extracting anything.
            var liveEntryCount = zip.Entries.Count(e => !string.IsNullOrEmpty(e.Name));
            if (liveEntryCount > _limits.MaxEntryCount)
            {
                throw new ImportPackageInspectionException(
                    $"Archive contains {liveEntryCount:N0} entries at extraction time for package '{package.Id}', " +
                    $"exceeding the configured limit of {_limits.MaxEntryCount:N0}. Extraction aborted.");
            }

            var extractedPathsThisPass = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in manifest.Entries.Where(e => !e.IsSuspicious))
            {
                ct.ThrowIfCancellationRequested();
                filesInspected++;
                if (alreadyExtractedSet.Contains(entry.RelativePath)) continue;

                var zipEntry = zip.GetEntry(entry.RelativePath) ?? zip.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').TrimStart('/') == entry.RelativePath);
                if (zipEntry is null) continue;

                // Phase 4.8 — reapply the full hardened safety validator (path traversal, per-file
                // size, compression ratio, nested-archive rejection, bounded zip-bomb-guarded
                // checksum) against the live entry, never trusting the stored manifest's
                // IsSuspicious flag alone. Also rejects a duplicate normalized path within the
                // archive being extracted right now, even if the manifest didn't flag one (a
                // manifest built from a since-mutated archive object could disagree with reality).
                var revalidated = ZipEntrySafetyValidator.Validate(zipEntry, _limits);
                if (revalidated.IsSuspicious)
                {
                    _logger.LogWarning(
                        "ImportPackageProcessingService: entry '{Path}' failed extraction-time safety revalidation for package {PackageId}: {Reason}",
                        entry.RelativePath, package.Id, revalidated.Reason);
                    continue;
                }
                if (!extractedPathsThisPass.Add(revalidated.RelativePath))
                {
                    _logger.LogWarning(
                        "ImportPackageProcessingService: duplicate archive path '{Path}' rejected at extraction time for package {PackageId}.",
                        revalidated.RelativePath, package.Id);
                    continue;
                }

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

    /// <summary>Phase 4.8 — whole-stream SHA-256 used to revalidate an archive's checksum at
    /// extraction time. The stream's total length is already bounded by the upload/inspection-time
    /// <c>MaxCompressedSizeBytes</c> check, so no additional read cap is needed here.</summary>
    private static async Task<string> ComputeStreamChecksumAsync(Stream stream, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
    }

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
                // Phase 4.6 — the audio's measured duration, threaded onto the candidate below
                // regardless of which branch supplies the transcript. Best-effort: a measurement
                // failure here never blocks candidate creation (unlike the STT branch, where a
                // failed measurement blocks — because that branch's failure also means no cost
                // basis exists to safely proceed with billing).
                decimal? resolvedDurationSeconds = null;

                if (matchingTranscript is not null)
                {
                    await using var transcriptStream = await _storage.ReadAsync(matchingTranscript.StorageKey, ct);
                    using var reader = new StreamReader(transcriptStream);
                    transcriptText = await reader.ReadToEndAsync(ct);
                    matchingTranscript.MarkProcessed();

                    try
                    {
                        var supplementaryMeasurement = await _audioDurationResolver.ResolveAsync(audio, ct);
                        if (supplementaryMeasurement.Success)
                        {
                            resolvedDurationSeconds = supplementaryMeasurement.DurationSeconds;
                            if (!supplementaryMeasurement.WasReused)
                                await _db.SaveChangesAsync(ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ImportPackageProcessingService: could not measure audio duration for {File} (supplied-transcript path) — continuing without it.", audio.RelativePath);
                    }
                }
                else
                {
                    // Phase 4.4E — real, persisted, reusable duration measurement replaces the
                    // flat five-minute assumption entirely. A measurement failure must not
                    // silently fall back to an assumption — it fails this specific audio asset
                    // clearly (no STT call, no cost, no candidate) rather than billing off an
                    // invented number.
                    var measurement = await _audioDurationResolver.ResolveAsync(audio, ct);
                    if (!measurement.Success)
                    {
                        audio.MarkFailed(measurement.ErrorMessage ?? "Could not measure audio duration.");
                        await _db.SaveChangesAsync(ct); // persists the measurement failure and the asset's failed state together
                        candidatesFailed++;
                        continue;
                    }
                    if (!measurement.WasReused)
                        await _db.SaveChangesAsync(ct); // persist the fresh measurement durably before billing off it

                    resolvedDurationSeconds = measurement.DurationSeconds;
                    var measuredMinutes = measurement.DurationSeconds!.Value / 60m;
                    var sttCostIfCharged = measuredMinutes * _costOptions.SttCostPerMinute;
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
                    var claim = await _sttLedger.ClaimAsync(package.Id, plan.Id, audio.Id, logicalKey, "openai", measuredMinutes, ct);

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
                    var candidate = CreateListeningCandidate(run.Id, audio, transcriptText, origin, resolvedDurationSeconds);
                    audio.MarkProcessed();

                    // Part D/Part 8 — preserve the candidate↔asset relationship so a later
                    // Resource Bank publish (and Lesson generation reading it) can trace the
                    // published item back to its source audio/transcript files.
                    // Phase 4.5 — every linked asset must belong to this same package; a candidate
                    // can never reference an asset staged under a different ImportPackage.
                    ImportAssetProvenanceGuard.EnsureAssetBelongsToPackage(audio, package.Id);
                    _db.ImportCandidateAssetLinks.Add(new ImportCandidateAssetLink(candidate.Id, audio.Id, ImportAssetRole.Audio));
                    if (matchingTranscript is not null)
                    {
                        ImportAssetProvenanceGuard.EnsureAssetBelongsToPackage(matchingTranscript, package.Id);
                        _db.ImportCandidateAssetLinks.Add(new ImportCandidateAssetLink(candidate.Id, matchingTranscript.Id, ImportAssetRole.Transcript));
                    }

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

    private ResourceCandidate CreateListeningCandidate(
        Guid runId, ImportAsset audio, string? transcriptText, MetadataOrigin transcriptOrigin, decimal? audioDurationSeconds = null)
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
        if (audioDurationSeconds is not null)
            candidate.SetAudioDuration(audioDurationSeconds);
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
