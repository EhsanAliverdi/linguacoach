using FluentAssertions;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.UsageGovernance;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase E2 — AI analysis of a staged ResourceCandidate. Uses SQLite in-memory (matches
/// ResourceImportServiceTests' Phase E1 convention) plus a hand-rolled swappable fake AI
/// provider/resolver (matches PracticeGymTemplateGenerationJobTests' fake-provider convention) —
/// never calls a real AI provider.
/// </summary>
public sealed class ResourceCandidateAnalysisServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly SwappableFakeAiProvider _provider = new();
    private readonly ResourceCandidateAnalysisService _sut;

    public ResourceCandidateAnalysisServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _db.AiPrompts.Add(new AiPrompt(
            ResourceCandidateAnalysisService.AnalyzePromptKey,
            "Classify: {{candidateType}} {{canonicalText}} {{normalizedJson}} {{languageCode}} {{sourceName}} {{sourceLicense}} {{rawContext}}"));
        _db.SaveChanges();

        var aiExecution = new AiExecutionService(
            _db, new FakeAiProviderResolver(_provider), new NeverCalledUsageQuotaService(), NullLogger<AiExecutionService>.Instance);
        var aiLedger = new ImportAiEnrichmentOperationLedger(_db);
        var pricingResolver = new AiPricingResolver(_db, new ConfigurationBuilder().Build());

        _sut = new ResourceCandidateAnalysisService(
            _db, new DbPromptAiContextBuilder(_db), aiExecution, aiLedger, pricingResolver,
            Options.Create(new ImportCostEstimationOptions()), NullLogger<ResourceCandidateAnalysisService>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private ResourceCandidate SeedCandidate(
        ResourceCandidateValidationStatus status = ResourceCandidateValidationStatus.NeedsReview,
        decimal approvedCostCeiling = 100m, bool billable = false)
    {
        var source = new CefrResourceSource("Test Source", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport("cleared for test");
        _db.CefrResourceSources.Add(source);
        _db.SaveChanges();

        // Phase 4.2 — AI analysis requires the candidate's run to trace back to an ImportPackage
        // with an approved Import Execution Plan.
        var package = new ImportPackage(source.Id, "test-package", DateTimeOffset.UtcNow);
        _db.ImportPackages.Add(package);
        _db.SaveChanges();
        // Phase 4.4D — durable ledgering/cost-ceiling enforcement only applies to the billable
        // AI-structuring modes; most existing tests exercise AnalyzeAsync directly against a
        // package with no ProcessingMode set (never billable in production), matching pre-4.4D
        // behavior. Tests exercising the ledger/ceiling path opt in via billable: true.
        if (billable)
            package.SetProcessingMode(ImportProcessingMode.FullAiAssisted, "test");
        _db.SaveChanges();
        var plan = new ImportProfile(
            package.Id, 1, profileJson: "{}", sampleAssetIds: Array.Empty<Guid>(),
            estimatedCandidateCount: 1, createdAtUtc: DateTimeOffset.UtcNow);
        plan.SubmitForApproval();
        plan.Approve(null, DateTimeOffset.UtcNow, approvedCostCeiling);
        _db.ImportProfiles.Add(plan);
        package.ApproveProfile(plan.Id);
        _db.SaveChanges();

        var run = new ResourceImportRun(
            source.Id, ResourceImportMode.Csv, "test.csv", "hash123", DateTimeOffset.UtcNow,
            importPackageId: package.Id);
        _db.ResourceImportRuns.Add(run);
        _db.SaveChanges();

        var raw = new ResourceRawRecord(run.Id, "rawhash123", "en", "row", rawJson: """{"word":"hello"}""");
        raw.MarkParsed();
        _db.ResourceRawRecords.Add(raw);
        _db.SaveChanges();

        var fingerprint = new ActivityContentFingerprintService().ComputeFingerprint(
            new LinguaCoach.Application.Activity.ActivityContentFingerprintRequest(
                """{"word":"hello"}""", LinguaCoach.Application.Activity.ActivityContentShape.Unknown, null, "hello"));

        var candidate = new ResourceCandidate(
            raw.Id, ResourceCandidateType.VocabularyEntry, "hello", """{"word":"hello"}""", "en",
            "hello", fingerprint, status);
        _db.ResourceCandidates.Add(candidate);
        _db.SaveChanges();

        return candidate;
    }

    private void SeedPricingOverride(decimal inputPer1K = 0.01m, decimal outputPer1K = 0.03m)
    {
        _db.AiModelPricingOverrides.Add(new AiModelPricingOverride(
            "openai", "gpt-4o-mini", inputPer1K, outputPer1K, "USD",
            DateTime.UtcNow.AddDays(-1), null, null, null));
        _db.SaveChanges();
    }

    private const string ValidAiJson = """
        {
          "cefrLevel": "A1",
          "cefrConfidence": 0.9,
          "primarySkill": "vocabulary",
          "subskill": "vocabulary.receptive",
          "difficultyBand": 1,
          "contextTags": ["greeting"],
          "focusTags": ["everyday"],
          "grammarTags": [],
          "vocabularyTags": ["greetings"],
          "pronunciationTags": [],
          "activitySuitabilityTags": ["phrase_match"],
          "safetyTags": [],
          "qualityScore": 0.85,
          "needsHumanReview": false,
          "qualityIssues": [],
          "suggestedActivityUses": ["flashcard"],
          "searchText": "hello greeting"
        }
        """;

    [Fact]
    public async Task Successful_analysis_stores_ai_metadata_on_the_candidate()
    {
        var candidate = SeedCandidate();
        _provider.NextResponses.Enqueue(ValidAiJson);

        var result = await _sut.AnalyzeAsync(candidate.Id);

        result.Success.Should().BeTrue();
        result.Output.Should().NotBeNull();
        result.Output!.CefrLevel.Should().Be("A1");

        var reloaded = await _db.ResourceCandidates.FirstAsync(c => c.Id == candidate.Id);
        reloaded.AiAnalysisJson.Should().NotBeNullOrWhiteSpace();
        reloaded.CefrLevel.Should().Be("A1");
        reloaded.CefrConfidence.Should().Be(0.9);
        reloaded.PrimarySkill.Should().Be("vocabulary");
        reloaded.QualityScore.Should().Be(0.85);
    }

    [Fact]
    public async Task Invalid_json_response_is_retried_once_then_succeeds()
    {
        var candidate = SeedCandidate();
        _provider.NextResponses.Enqueue("this is not json");
        _provider.NextResponses.Enqueue(ValidAiJson);

        var result = await _sut.AnalyzeAsync(candidate.Id);

        result.Success.Should().BeTrue();
        _provider.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Invalid_json_response_after_retry_fails_gracefully_without_throwing()
    {
        var candidate = SeedCandidate(ResourceCandidateValidationStatus.Pending);
        _provider.NextResponses.Enqueue("not json at all");
        _provider.NextResponses.Enqueue("still not json");

        var result = await _sut.AnalyzeAsync(candidate.Id);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        var reloaded = await _db.ResourceCandidates.FirstAsync(c => c.Id == candidate.Id);
        // Original candidate data must be left completely intact.
        reloaded.CanonicalText.Should().Be("hello");
        reloaded.AiAnalysisJson.Should().BeNull();
        // Pending -> promoted to NeedsReview so it's safely re-analyzable/reviewable later.
        reloaded.ValidationStatus.Should().Be(ResourceCandidateValidationStatus.NeedsReview);
    }

    [Fact]
    public async Task Ai_provider_unavailable_fails_gracefully_without_throwing()
    {
        var candidate = SeedCandidate(ResourceCandidateValidationStatus.Pending);
        _provider.ThrowUnavailable = true;

        var result = await _sut.AnalyzeAsync(candidate.Id);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("unavailable");

        var reloaded = await _db.ResourceCandidates.FirstAsync(c => c.Id == candidate.Id);
        reloaded.CanonicalText.Should().Be("hello");
        reloaded.AiAnalysisJson.Should().BeNull();
        reloaded.ValidationStatus.Should().Be(ResourceCandidateValidationStatus.NeedsReview);
    }

    [Fact]
    public async Task Analyze_is_idempotent_and_overwrites_prior_analysis()
    {
        var candidate = SeedCandidate();
        _provider.NextResponses.Enqueue(ValidAiJson);
        await _sut.AnalyzeAsync(candidate.Id);

        _provider.NextResponses.Enqueue(ValidAiJson.Replace("\"A1\"", "\"B1\""));
        await _sut.AnalyzeAsync(candidate.Id);

        (await _db.ResourceCandidates.CountAsync()).Should().Be(1);
        var reloaded = await _db.ResourceCandidates.FirstAsync(c => c.Id == candidate.Id);
        reloaded.CefrLevel.Should().Be("B1");
    }

    [Fact]
    public async Task Unrecognized_candidate_returns_not_found_result()
    {
        var result = await _sut.AnalyzeAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Candidate not found.");
    }

    // ── Phase 4.4D — durable AI operation ledger, fail-closed pricing, cost-ceiling enforcement.
    // These tests set billable: true (FullAiAssisted processing mode) — the only mode in which
    // AnalyzeAsync's ledger/pricing/ceiling machinery activates, matching production exactly
    // (ImportPackageProcessingService never routes a Direct-mode package through AI enrichment). ──

    [Fact]
    public async Task Identical_successful_AI_operation_is_reused_after_retry_no_second_provider_call_no_duplicate_cost()
    {
        SeedPricingOverride();
        var candidate = SeedCandidate(billable: true);
        _provider.NextResponses.Enqueue(ValidAiJson);

        var first = await _sut.AnalyzeAsync(candidate.Id);
        first.Success.Should().BeTrue();
        _provider.CallCount.Should().Be(1);

        var packageAfterFirst = await _db.ImportPackages.FirstAsync();
        var costAfterFirst = packageAfterFirst.AccruedCost;
        costAfterFirst.Should().BeGreaterThan(0);

        // Simulate a retry (e.g. the batch service re-selecting this candidate on a later pass).
        var second = await _sut.AnalyzeAsync(candidate.Id);

        second.Success.Should().BeTrue();
        _provider.CallCount.Should().Be(1, "the provider must not be called again for an already-succeeded operation");

        var packageAfterSecond = await _db.ImportPackages.FirstAsync();
        packageAfterSecond.AccruedCost.Should().Be(costAfterFirst, "cost must not be accrued twice for the same logical operation");

        (await _db.ImportAiEnrichmentOperations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Missing_pricing_prevents_the_provider_call()
    {
        // No AiModelPricingOverride seeded and no configuration — pricing cannot resolve.
        var candidate = SeedCandidate(billable: true);
        _provider.NextResponses.Enqueue(ValidAiJson);

        Func<Task> act = () => _sut.AnalyzeAsync(candidate.Id);

        await act.Should().ThrowAsync<ImportPricingUnavailableException>();
        _provider.CallCount.Should().Be(0, "the provider must never be called when required pricing is unresolved");
    }

    [Fact]
    public async Task Cost_ceiling_blocks_the_next_AI_call_before_execution()
    {
        SeedPricingOverride(inputPer1K: 10m, outputPer1K: 10m); // deliberately expensive
        var candidate = SeedCandidate(billable: true, approvedCostCeiling: 0.01m); // far below one call's estimated cost
        _provider.NextResponses.Enqueue(ValidAiJson);

        var result = await _sut.AnalyzeAsync(candidate.Id);

        result.Success.Should().BeFalse();
        result.CeilingReached.Should().BeTrue();
        result.PauseReason.Should().NotBeNullOrWhiteSpace();
        _provider.CallCount.Should().Be(0, "the provider must never be called once the projected cost would exceed the ceiling");

        var package = await _db.ImportPackages.FirstAsync();
        package.AccruedCost.Should().Be(0, "no cost may be accrued for a call that was never made");
    }
}

/// <summary>Fake IAiProvider that returns queued responses in order, or throws if configured to
/// simulate total provider unavailability.</summary>
internal sealed class SwappableFakeAiProvider : IAiProvider
{
    public Queue<string> NextResponses { get; } = new();
    public bool ThrowUnavailable { get; set; }
    public int CallCount { get; private set; }

    public string ProviderName => "fake-provider";

    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        CallCount++;
        if (ThrowUnavailable)
            throw new AiUnavailableException("Simulated AI provider unavailable.");

        var response = NextResponses.Count > 0 ? NextResponses.Dequeue() : "{}";
        return Task.FromResult(new AiResponse(response, InputTokens: 50, OutputTokens: 50, CostUsd: 0.0001m, "fake-model", ProviderName));
    }
}

internal sealed class FakeAiProviderResolver : IAiProviderResolver
{
    private readonly IAiProvider _provider;
    public FakeAiProviderResolver(IAiProvider provider) => _provider = provider;

    public AiProviderPair ResolveLlm(string featureKey, string categoryKey) =>
        new(new AiProviderSelection(_provider, _provider.ProviderName, "fake-model"), Fallback: null);

    public AiTtsProviderSelection ResolveTts(string featureKey, string categoryKey) =>
        new("fake", "fake", "fake");
}

/// <summary>Never invoked in these tests — every call passes a null studentProfileId to
/// AiExecutionService, which skips all quota check/record calls in that case.</summary>
internal sealed class NeverCalledUsageQuotaService : IUsageQuotaService
{
    public Task<QuotaDecision> CheckAsync(Guid studentProfileId, string featureKey, long estimatedUnits = 1, decimal? estimatedCost = null, CancellationToken ct = default)
        => throw new InvalidOperationException("Should not be called when studentProfileId is null.");

    public Task RecordAsync(UsageEvent usageEvent, CancellationToken ct = default)
        => throw new InvalidOperationException("Should not be called when studentProfileId is null.");

    public Task<UsageSummary> GetUsageSummaryAsync(Guid studentProfileId, DateOnly from, DateOnly to, CancellationToken ct = default)
        => throw new InvalidOperationException("Should not be called when studentProfileId is null.");

    public Task<UsagePolicy?> GetEffectivePolicyAsync(Guid studentProfileId, CancellationToken ct = default)
        => throw new InvalidOperationException("Should not be called when studentProfileId is null.");
}
