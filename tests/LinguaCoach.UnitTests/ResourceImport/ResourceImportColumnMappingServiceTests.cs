using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.UsageGovernance;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase K1 — AI-assisted import column-mapping proposal. Reuses
/// ResourceCandidateAnalysisServiceTests' fake-provider infrastructure (SwappableFakeAiProvider/
/// FakeAiProviderResolver/NeverCalledUsageQuotaService) — never calls a real AI provider.
/// </summary>
public sealed class ResourceImportColumnMappingServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly SwappableFakeAiProvider _provider = new();
    private readonly ResourceImportColumnMappingService _sut;

    public ResourceImportColumnMappingServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _db.AiPrompts.Add(new AiPrompt(
            ResourceImportColumnMappingService.ProposeMappingPromptKey,
            "Map: {{columns}} {{recognizedFields}} {{sampleRowsJson}}"));
        _db.SaveChanges();

        var aiExecution = new AiExecutionService(
            _db, new FakeAiProviderResolver(_provider), new NeverCalledUsageQuotaService(), NullLogger<AiExecutionService>.Instance);

        _sut = new ResourceImportColumnMappingService(
            new DbPromptAiContextBuilder(_db), aiExecution, NullLogger<ResourceImportColumnMappingService>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private static readonly IReadOnlyList<IReadOnlyDictionary<string, string?>> SampleRows = new[]
    {
        new Dictionary<string, string?> { ["headword"] = "abandon", ["CEFR"] = "B1", ["pos"] = "verb" },
    };

    [Fact]
    public async Task Valid_ai_response_returns_recognized_field_suggestions()
    {
        _provider.NextResponses.Enqueue("""
            {"mapping": {
              "headword": {"field": "word", "confidence": 0.95},
              "CEFR": {"field": "cefrlevel", "confidence": 0.9},
              "pos": {"field": null, "confidence": 0.1}
            }}
            """);

        var result = await _sut.ProposeMappingAsync(
            new ResourceImportColumnMappingRequest(new[] { "headword", "CEFR", "pos" }, SampleRows));

        result.Success.Should().BeTrue();
        result.Suggestions.Should().HaveCount(3);
        result.Suggestions.Should().Contain(s => s.SourceColumn == "headword" && s.SuggestedField == "word");
        result.Suggestions.Should().Contain(s => s.SourceColumn == "CEFR" && s.SuggestedField == "cefrlevel");
        result.Suggestions.Should().Contain(s => s.SourceColumn == "pos" && s.SuggestedField == null);
    }

    [Fact]
    public async Task Unrecognized_suggested_field_is_dropped_to_null_never_trusted()
    {
        _provider.NextResponses.Enqueue("""
            {"mapping": { "headword": {"field": "not_a_real_field", "confidence": 0.99} }}
            """);

        var result = await _sut.ProposeMappingAsync(
            new ResourceImportColumnMappingRequest(new[] { "headword" }, SampleRows));

        result.Success.Should().BeTrue();
        result.Suggestions.Single().SuggestedField.Should().BeNull();
    }

    [Fact]
    public async Task Invalid_json_response_is_retried_once_then_succeeds()
    {
        _provider.NextResponses.Enqueue("this is not json");
        _provider.NextResponses.Enqueue("""{"mapping": { "headword": {"field": "word"} }}""");

        var result = await _sut.ProposeMappingAsync(
            new ResourceImportColumnMappingRequest(new[] { "headword" }, SampleRows));

        result.Success.Should().BeTrue();
        _provider.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Ai_provider_unavailable_fails_gracefully_without_throwing()
    {
        _provider.ThrowUnavailable = true;

        var result = await _sut.ProposeMappingAsync(
            new ResourceImportColumnMappingRequest(new[] { "headword" }, SampleRows));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("unavailable");
        result.Suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task Empty_columns_returns_failure_without_calling_ai()
    {
        var result = await _sut.ProposeMappingAsync(
            new ResourceImportColumnMappingRequest(Array.Empty<string>(), SampleRows));

        result.Success.Should().BeFalse();
        _provider.CallCount.Should().Be(0);
    }
}
