using System.IO.Compression;
using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.UsageGovernance;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Infrastructure.Speaking;
using LinguaCoach.Infrastructure.Storage;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase 4 (2026-07-15), Part 8 — the package processing pipeline. Proves the core acceptance
/// criteria: processing only ever runs against an Approved plan, extraction/candidate creation
/// actually happens (structured data via the existing pipeline, audio/transcript pairs directly),
/// and a projected cost overrun pauses rather than silently continuing past the ceiling.
/// </summary>
public sealed class ImportPackagePlanProcessingTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly FakeFileStorageService _storage = new();
    private readonly ImportPackageLimitsOptions _limits = new();
    private readonly ImportCostEstimationOptions _costOptions = new();

    private readonly ImportPackageUploadService _uploadService;
    private readonly ImportExecutionPlanGenerationService _planGenerationService;
    private readonly ImportExecutionPlanApprovalService _approvalService;
    private readonly ImportPackageProcessingService _processingService;
    private readonly FakeSpeechToTextService _sttService;

    public ImportPackagePlanProcessingTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _db.AiPrompts.Add(new AiPrompt(
            LinguaCoach.Persistence.Seed.DefaultAiSeeder.ImportPackagePlanReviewKey,
            "{{fileCount}} {{distinctExtensions}} {{detectedGroupsJson}} {{sampleMetadataJson}} {{samplingRound}} {{maxSamplingRounds}}"));
        _db.SaveChanges();

        var aiProvider = new SwappableFakeAiProvider();
        aiProvider.NextResponses.Enqueue("""{"groups":[],"ambiguousGroups":[],"unsupportedContentNotes":[],"structureConfidence":0.8,"needsAnotherSamplingRound":false}""");

        var inspector = new ZipPackageInspector(Options.Create(_limits));
        var modeDecision = new ImportProcessingModeDecisionService(Options.Create(_limits));
        var aiExecution = new AiExecutionService(
            _db, new FakeAiProviderResolver(aiProvider), new NeverCalledUsageQuotaService(), NullLogger<AiExecutionService>.Instance);
        var pricingResolver = new AiPricingResolver(_db, new ConfigurationBuilder().Build());
        var fingerprint = new ActivityContentFingerprintService();
        var resourceImportService = new ResourceImportService(_db, fingerprint);
        _sttService = new FakeSpeechToTextService(new ConfigurationBuilder().Build(), NullLogger<FakeSpeechToTextService>.Instance);

        var columnMappingService = new ResourceImportColumnMappingService(
            new DbPromptAiContextBuilder(_db), aiExecution, NullLogger<ResourceImportColumnMappingService>.Instance);

        var audioDurationResolver = new ImportAssetAudioDurationResolver(_storage, AudioProbe);

        _uploadService = new ImportPackageUploadService(_db, _storage, inspector, Options.Create(_limits));
        _planGenerationService = new ImportExecutionPlanGenerationService(
            _db, inspector, modeDecision, new DbPromptAiContextBuilder(_db), aiExecution, pricingResolver,
            new NoOpNotificationService(), _storage, resourceImportService, columnMappingService, audioDurationResolver,
            Options.Create(_limits), Options.Create(_costOptions), NullLogger<ImportExecutionPlanGenerationService>.Instance);
        _approvalService = new ImportExecutionPlanApprovalService(_db, pricingResolver, Options.Create(_costOptions));
        var profileResolver = new ApprovedImportProfileResolver(_db);
        var sttLedger = new ImportSttOperationLedger(_db);
        _processingService = new ImportPackageProcessingService(
            _db, _storage, resourceImportService, new NoOpBatchAnalysisService(), _sttService, fingerprint,
            pricingResolver, new NoOpNotificationService(), profileResolver, sttLedger, audioDurationResolver,
            Options.Create(_costOptions), NullLogger<ImportPackageProcessingService>.Instance);
    }

    /// <summary>Phase 4.4E — fake, in-process probe (no real ffprobe needed for these tests). The
    /// real <see cref="ImportAssetAudioDurationResolver"/> is used unmodified so reuse-by-checksum
    /// logic is exercised genuinely — only the actual measurement call is faked.</summary>
    private readonly FakeAudioDurationProbe AudioProbe = new();

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private async Task<Guid> SeedSourceAsync()
    {
        var source = new CefrResourceSource($"Test Source {Guid.NewGuid():N}", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport("test");
        _db.CefrResourceSources.Add(source);
        await _db.SaveChangesAsync();
        return source.Id;
    }

    private static byte[] BuildZip(params (string Name, byte[] Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }
        }
        return ms.ToArray();
    }

    private async Task<(Guid PackageId, Guid PlanId)> UploadConfirmAndGeneratePlanAsync(byte[] zipBytes)
    {
        var sourceId = await SeedSourceAsync();
        var uploadResult = await _uploadService.RequestUploadAsync(
            new RequestImportPackageUploadCommand(sourceId, "package.zip", zipBytes.Length));
        await _storage.SaveAsync(uploadResult.StorageKey, new MemoryStream(zipBytes), "application/zip");
        await _uploadService.ConfirmUploadAsync(new ConfirmImportPackageUploadCommand(uploadResult.ImportPackageId));
        var plan = await _planGenerationService.GenerateAsync(new GenerateImportExecutionPlanCommand(uploadResult.ImportPackageId));
        return (uploadResult.ImportPackageId, plan.PlanId);
    }

    private async Task ApprovePlanAsync(Guid packageId, Guid planId, decimal approvedCostCeiling)
    {
        var plan = await _db.ImportProfiles.FirstAsync(p => p.Id == planId);
        await _approvalService.ApproveAsync(new ApproveImportExecutionPlanCommand(
            packageId, planId, Guid.NewGuid(), approvedCostCeiling, plan.ConcurrencyStamp));
    }

    [Fact]
    public async Task ProcessPendingAsync_ignores_packages_without_an_approved_plan()
    {
        var zipBytes = BuildZip(("words.csv", System.Text.Encoding.UTF8.GetBytes("word,definition\nhello,greeting\n")));
        var (packageId, _) = await UploadConfirmAndGeneratePlanAsync(zipBytes);

        // Force status as if queued without an approved plan (should never happen via the real
        // approval service, but the processing service must defend against it regardless).
        var package = await _db.ImportPackages.FirstAsync(p => p.Id == packageId);
        package.MoveToStatus(ImportPackageStatus.Queued);
        await _db.SaveChangesAsync();

        var outcomes = await _processingService.ProcessPendingAsync(10);

        outcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task Approved_structured_data_package_creates_candidates_and_completes()
    {
        var zipBytes = BuildZip(("words.csv", System.Text.Encoding.UTF8.GetBytes("word,definition\nhello,greeting\nbye,farewell\n")));
        var (packageId, planId) = await UploadConfirmAndGeneratePlanAsync(zipBytes);

        await ApprovePlanAsync(packageId, planId, 50m);

        var outcomes = await _processingService.ProcessPendingAsync(10);

        outcomes.Should().ContainSingle(o => o.ImportPackageId == packageId && o.Completed);

        var package = await _db.ImportPackages.FirstAsync(p => p.Id == packageId);
        package.Status.Should().Be(ImportPackageStatus.ReadyForReview);
        package.CandidatesCreatedCount.Should().Be(2);

        var plan = await _db.ImportProfiles.FirstAsync(p => p.Id == planId);
        plan.Status.Should().Be(ImportProfileStatus.Completed);

        var assets = await _db.ImportAssets.Where(a => a.ImportPackageId == packageId).ToListAsync();
        assets.Should().ContainSingle(a => a.ProcessingState == ImportAssetProcessingState.Processed);
    }

    [Fact]
    public async Task Approved_audio_without_transcript_uses_STT_and_creates_a_Listening_candidate()
    {
        var zipBytes = BuildZip(("lesson/audio.mp3", new byte[100]));
        var (packageId, planId) = await UploadConfirmAndGeneratePlanAsync(zipBytes);

        await ApprovePlanAsync(packageId, planId, 50m);

        await _processingService.ProcessPendingAsync(10);

        var candidate = await _db.ResourceCandidates.FirstOrDefaultAsync(c => c.CandidateType == ResourceCandidateType.ListeningPassage);
        candidate.Should().NotBeNull();
        candidate!.NormalizedJson.Should().Contain(FakeSpeechToTextService.PlaceholderTranscript);
        candidate.TranscriptOrigin.Should().Be(MetadataOrigin.AITranscribed);
        candidate.AudioStorageKey.Should().NotBeNullOrEmpty();
    }

    // ── Phase 4.4E — real, persisted, reusable audio-duration measurement replaces the flat
    // five-minute assumption entirely. ──

    [Fact]
    public async Task Real_measured_duration_replaces_the_five_minute_assumption_in_STT_cost()
    {
        AudioProbe.NextDurationSeconds = 600m; // 10 minutes — double the old flat 5-minute assumption
        var zipBytes = BuildZip(("lesson/audio.mp3", new byte[100]));
        var (packageId, planId) = await UploadConfirmAndGeneratePlanAsync(zipBytes);
        await ApprovePlanAsync(packageId, planId, 50m);

        await _processingService.ProcessPendingAsync(10);

        var package = await _db.ImportPackages.FirstAsync(p => p.Id == packageId);
        var expectedCost = 10m * _costOptions.SttCostPerMinute; // 10 measured minutes, not 5 assumed
        package.AccruedCost.Should().Be(expectedCost);

        var asset = await _db.ImportAssets.FirstAsync(a => a.ImportPackageId == packageId && a.FileExtension == ".mp3");
        asset.AudioDurationSeconds.Should().Be(600m);
        asset.AudioDurationMeasurementStatus.Should().Be(ImportAudioDurationMeasurementStatus.Measured);
    }

    [Fact]
    public async Task Measured_duration_changes_the_cost_ceiling_calculation()
    {
        // 20 measured minutes at the default STT rate comfortably exceeds a $0.05 ceiling, even
        // though the old flat 5-minute assumption would not have.
        AudioProbe.NextDurationSeconds = 20m * 60m;
        var zipBytes = BuildZip(("lesson/audio.mp3", new byte[100]));
        var (packageId, planId) = await UploadConfirmAndGeneratePlanAsync(zipBytes);
        await ApprovePlanAsync(packageId, planId, 0.05m);

        await _processingService.ProcessPendingAsync(10);

        var plan = await _db.ImportProfiles.FirstAsync(p => p.Id == planId);
        plan.Status.Should().Be(ImportProfileStatus.PausedForCostApproval);
        _sttService.CallCount.Should().Be(0, "the ceiling must be checked against the real measured duration before the provider is ever called");
    }

    [Fact]
    public async Task Audio_duration_measurement_failure_fails_the_asset_clearly_without_calling_STT()
    {
        AudioProbe.NextFailureStatus = AudioDurationProbeStatus.UnsupportedOrCorrupt;
        AudioProbe.NextFailureMessage = "ffprobe exited with code 1: Invalid data found when processing input";
        var zipBytes = BuildZip(("lesson/audio.mp3", new byte[100]));
        var (packageId, planId) = await UploadConfirmAndGeneratePlanAsync(zipBytes);
        await ApprovePlanAsync(packageId, planId, 50m);

        await _processingService.ProcessPendingAsync(10);

        _sttService.CallCount.Should().Be(0, "duration must be known before STT is ever called — no silent five-minute fallback");
        var asset = await _db.ImportAssets.FirstAsync(a => a.ImportPackageId == packageId && a.FileExtension == ".mp3");
        asset.ProcessingState.Should().Be(ImportAssetProcessingState.Failed);
        asset.AudioDurationMeasurementStatus.Should().Be(ImportAudioDurationMeasurementStatus.Failed);
        asset.AudioDurationMeasurementError.Should().Contain("Invalid data");

        var candidate = await _db.ResourceCandidates.FirstOrDefaultAsync(c => c.CandidateType == ResourceCandidateType.ListeningPassage);
        candidate.Should().BeNull("no candidate may be created for audio whose duration could not be measured");
    }

    [Fact]
    public async Task Retry_does_not_remeasure_an_unchanged_audio_asset()
    {
        var zipBytes = BuildZip(("lesson/audio.mp3", new byte[100]));
        var (packageId, planId) = await UploadConfirmAndGeneratePlanAsync(zipBytes);
        await ApprovePlanAsync(packageId, planId, 50m);

        await _processingService.ProcessPendingAsync(10);
        AudioProbe.CallCount.Should().Be(1);

        // Simulate the crash-recovery window a real retry would hit (stage/asset progress not
        // yet checkpointed, but the measurement — already persisted in its own save — survives),
        // mirroring ImportPlanEditingAndCostAccountingTests' STT retry test.
        var package = await _db.ImportPackages.FirstAsync(p => p.Id == packageId);
        var asset = await _db.ImportAssets.FirstAsync(a => a.ImportPackageId == packageId && a.FileExtension == ".mp3");
        _db.Entry(asset).Property(nameof(ImportAsset.ProcessingState)).CurrentValue = ImportAssetProcessingState.Inspected;
        package.MoveToStatus(ImportPackageStatus.CreatingCandidates);
        _db.Entry(package).Property(nameof(ImportPackage.LastCompletedStageIndex)).CurrentValue = 0;
        await _db.SaveChangesAsync();

        await _processingService.ProcessPendingAsync(10);

        AudioProbe.CallCount.Should().Be(1, "the stored measurement must be reused, not remeasured, when the asset's content checksum is unchanged");
    }

    [Fact]
    public async Task Processing_pauses_for_cost_approval_when_projected_cost_exceeds_the_ceiling()
    {
        var zipBytes = BuildZip(
            ("a/audio1.mp3", new byte[100]), ("b/audio2.mp3", new byte[100]), ("c/audio3.mp3", new byte[100]));
        var (packageId, planId) = await UploadConfirmAndGeneratePlanAsync(zipBytes);

        // Approve with a near-zero ceiling so even one STT call trips it.
        await ApprovePlanAsync(packageId, planId, 0.0001m);

        var outcomes = await _processingService.ProcessPendingAsync(10);

        outcomes.Should().ContainSingle(o => o.ImportPackageId == packageId && o.PausedForCostApproval);

        var plan = await _db.ImportProfiles.FirstAsync(p => p.Id == planId);
        plan.Status.Should().Be(ImportProfileStatus.PausedForCostApproval);
        plan.PauseReason.Should().NotBeNullOrEmpty();

        var package = await _db.ImportPackages.FirstAsync(p => p.Id == packageId);
        package.Status.Should().Be(ImportPackageStatus.AwaitingMappingApproval);
    }

    private sealed class NoOpBatchAnalysisService : IResourceCandidateBatchAnalysisService
    {
        public Task<ResourceCandidateBatchAnalysisResult> AnalyzePendingForRunAsync(Guid resourceImportRunId, CancellationToken ct = default)
            => Task.FromResult(new ResourceCandidateBatchAnalysisResult(0, 0, 0, 0, false));
    }

    /// <summary>Phase 4.4E — fake <see cref="IAudioDurationProbe"/> returning a configurable
    /// duration (default 5 minutes, matching the previous flat assumption, so existing tests that
    /// don't care about the exact value keep working unchanged) or a configurable failure. Tracks
    /// call count so tests can prove reuse-by-checksum skips a remeasurement.</summary>
    private sealed class FakeAudioDurationProbe : IAudioDurationProbe
    {
        public decimal NextDurationSeconds { get; set; } = 300m; // 5 minutes, matches the old flat assumption
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
}
