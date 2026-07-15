using System.IO.Compression;
using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.UsageGovernance;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Infrastructure.Storage;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Mandatory Import Execution Plan addendum (2026-07-15) — end-to-end (in-process, fake AI/
/// storage) coverage of upload -> manifest -> automatic plan generation -> approval. Proves the
/// core acceptance criteria: no processing-mode/candidate work happens before an explicit
/// administrator approval, and a rejected or unapproved plan can never reach Queued/Executing.
/// </summary>
public sealed class ImportPackagePlanFlowTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly FakeFileStorageService _storage = new();
    private readonly ImportPackageLimitsOptions _limits = new();
    private readonly ImportCostEstimationOptions _costOptions = new();
    private readonly SwappableFakeAiProvider _aiProvider = new();

    private readonly ImportPackageUploadService _uploadService;
    private readonly ImportExecutionPlanGenerationService _planGenerationService;
    private readonly ImportExecutionPlanApprovalService _approvalService;

    public ImportPackagePlanFlowTests()
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

        var inspector = new ZipPackageInspector(Options.Create(_limits));
        var modeDecision = new ImportProcessingModeDecisionService(Options.Create(_limits));
        var aiExecution = new AiExecutionService(
            _db, new FakeAiProviderResolver(_aiProvider), new NeverCalledUsageQuotaService(), NullLogger<AiExecutionService>.Instance);
        var pricingResolver = new AiPricingResolver(_db, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        _uploadService = new ImportPackageUploadService(_db, _storage, inspector, Options.Create(_limits));
        _planGenerationService = new ImportExecutionPlanGenerationService(
            _db, inspector, modeDecision, new DbPromptAiContextBuilder(_db), aiExecution, pricingResolver,
            new NoOpNotificationService(),
            Options.Create(_limits), Options.Create(_costOptions), NullLogger<ImportExecutionPlanGenerationService>.Instance);
        _approvalService = new ImportExecutionPlanApprovalService(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private const string AiReviewResponse = """
        {
          "groups": [],
          "ambiguousGroups": [],
          "unsupportedContentNotes": [],
          "structureConfidence": 0.8,
          "needsAnotherSamplingRound": false
        }
        """;

    private async Task<Guid> SeedSourceAsync()
    {
        var source = new CefrResourceSource($"Test Source {Guid.NewGuid():N}", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport("test");
        _db.CefrResourceSources.Add(source);
        await _db.SaveChangesAsync();
        return source.Id;
    }

    private static byte[] BuildSmallCsvZip()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("words.csv");
            using var entryStream = entry.Open();
            var bytes = System.Text.Encoding.UTF8.GetBytes("word,definition\nhello,greeting\n");
            entryStream.Write(bytes, 0, bytes.Length);
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task RequestUploadAsync_rejects_a_declared_size_above_the_configured_limit()
    {
        var sourceId = await SeedSourceAsync();
        var command = new RequestImportPackageUploadCommand(sourceId, "big.zip", _limits.MaxCompressedSizeBytes + 1);

        var act = async () => await _uploadService.RequestUploadAsync(command);

        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }

    [Fact]
    public async Task Full_flow_upload_confirm_plan_generate_and_approve_reaches_Queued()
    {
        var sourceId = await SeedSourceAsync();
        var zipBytes = BuildSmallCsvZip();

        var uploadResult = await _uploadService.RequestUploadAsync(
            new RequestImportPackageUploadCommand(sourceId, "words.zip", zipBytes.Length));

        // Simulate the client's direct PUT to storage completing.
        await _storage.SaveAsync(uploadResult.StorageKey, new MemoryStream(zipBytes), "application/zip");

        var manifestSummary = await _uploadService.ConfirmUploadAsync(new ConfirmImportPackageUploadCommand(uploadResult.ImportPackageId));
        manifestSummary.IsAccepted.Should().BeTrue();

        _aiProvider.NextResponses.Enqueue(AiReviewResponse);
        var plan = await _planGenerationService.GenerateAsync(new GenerateImportExecutionPlanCommand(uploadResult.ImportPackageId));

        plan.Status.Should().Be(ImportProfileStatus.AwaitingApproval);
        plan.Estimate.Cost.ExpectedCost.Should().BeGreaterThanOrEqualTo(0);

        var package = await _db.ImportPackages.FirstAsync(p => p.Id == uploadResult.ImportPackageId);
        package.Status.Should().Be(ImportPackageStatus.AwaitingMappingApproval);
        package.ApprovedImportProfileId.Should().BeNull();

        var approved = await _approvalService.ApproveAsync(new ApproveImportExecutionPlanCommand(
            uploadResult.ImportPackageId, plan.PlanId, Guid.NewGuid(), ApprovedCostCeiling: 50m));

        approved.Status.Should().Be(ImportProfileStatus.Approved);

        var packageAfterApproval = await _db.ImportPackages.FirstAsync(p => p.Id == uploadResult.ImportPackageId);
        packageAfterApproval.Status.Should().Be(ImportPackageStatus.Queued);
        packageAfterApproval.ApprovedImportProfileId.Should().Be(plan.PlanId);
    }

    [Fact]
    public async Task Rejected_plan_cannot_be_approved_afterward()
    {
        var sourceId = await SeedSourceAsync();
        var zipBytes = BuildSmallCsvZip();

        var uploadResult = await _uploadService.RequestUploadAsync(
            new RequestImportPackageUploadCommand(sourceId, "words.zip", zipBytes.Length));
        await _storage.SaveAsync(uploadResult.StorageKey, new MemoryStream(zipBytes), "application/zip");
        await _uploadService.ConfirmUploadAsync(new ConfirmImportPackageUploadCommand(uploadResult.ImportPackageId));

        _aiProvider.NextResponses.Enqueue(AiReviewResponse);
        var plan = await _planGenerationService.GenerateAsync(new GenerateImportExecutionPlanCommand(uploadResult.ImportPackageId));

        await _approvalService.RejectAsync(new RejectImportExecutionPlanCommand(
            uploadResult.ImportPackageId, plan.PlanId, Guid.NewGuid(), "Too expensive."));

        var act = async () => await _approvalService.ApproveAsync(new ApproveImportExecutionPlanCommand(
            uploadResult.ImportPackageId, plan.PlanId, Guid.NewGuid(), 50m));

        await act.Should().ThrowAsync<ImportExecutionPlanNotApprovableException>();

        var package = await _db.ImportPackages.FirstAsync(p => p.Id == uploadResult.ImportPackageId);
        package.Status.Should().Be(ImportPackageStatus.Failed);
    }

    [Fact]
    public async Task Package_with_no_manifest_cannot_have_a_plan_generated()
    {
        var sourceId = await SeedSourceAsync();
        var package = new ImportPackage(sourceId, "pending.zip", DateTimeOffset.UtcNow);
        _db.ImportPackages.Add(package);
        await _db.SaveChangesAsync();

        var act = async () => await _planGenerationService.GenerateAsync(new GenerateImportExecutionPlanCommand(package.Id));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }
}
