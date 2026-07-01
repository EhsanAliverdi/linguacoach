using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 12B review scaffold dry-run endpoint and mastery validation.
/// Verifies: auth guards, read-only behavior, correct response shape.
/// </summary>
public sealed class ReviewScaffoldDryRunTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ReviewScaffoldDryRunTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── Auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DryRun_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .GetAsync("/api/admin/readiness-pool/review-scaffold/dry-run");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DryRun_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync(
            $"dryrun403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/readiness-pool/review-scaffold/dry-run");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MasteryValidation_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .GetAsync("/api/admin/mastery/validation-summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MasteryValidation_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync(
            $"mastery403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/mastery/validation-summary");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Dry-run returns expected shape ────────────────────────────────────────

    [Fact]
    public async Task DryRun_AsAdmin_Returns200WithExpectedShape()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/readiness-pool/review-scaffold/dry-run");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        foreach (var field in new[]
        {
            "generationEnabled", "dryRunOnly", "status",
            "studentsConsidered", "studentsEligibleForReview",
            "estimatedReviewOnlyConversions", "blockedDuplicates",
            "blockedInactiveObjectives", "estimatedNetNewReviewItems",
            "requireAdminReview", "maxScaffoldItemsPerStudentPerDay",
            "scaffoldAllowedSources", "allowTodayLessonInsertion",
            "minimumConfidenceForReviewNeed", "adminReviewRequiredCount",
            "generatedTodayCount",
            "warnings", "generatedAt"
        })
        {
            Assert.True(body.TryGetProperty(field, out _), $"Missing field: {field}");
        }
    }

    // ── Dry-run reports Phase 19A safe config defaults ────────────────────────

    [Fact]
    public async Task DryRun_ReportsSafeConfigDefaults()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/readiness-pool/review-scaffold/dry-run");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.True(body.GetProperty("requireAdminReview").GetBoolean());
        Assert.False(body.GetProperty("allowTodayLessonInsertion").GetBoolean());
        Assert.Equal("Medium", body.GetProperty("minimumConfidenceForReviewNeed").GetString());
        var sources = body.GetProperty("scaffoldAllowedSources").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        // .NET config binding appends config-bound array items to the class default rather than
        // replacing it (same known quirk already present for PlacementAssessmentOptions.SkillsToAssess),
        // so assert containment/exclusion rather than exact count.
        Assert.All(sources, s => Assert.Equal("PracticeGym", s));
        Assert.DoesNotContain("TodayLesson", sources);
    }

    // ── Dry-run reports Disabled status when flag is off ─────────────────────

    [Fact]
    public async Task DryRun_FlagDisabled_ReportsDisabledStatus()
    {
        // Default config has EnableReviewScaffoldGeneration=false
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/readiness-pool/review-scaffold/dry-run");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.False(body.GetProperty("generationEnabled").GetBoolean());
        Assert.Equal("Disabled", body.GetProperty("status").GetString());
    }

    // ── Dry-run does NOT mutate the database ──────────────────────────────────

    [Fact]
    public async Task DryRun_DoesNotMutateDatabase()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        // Count items before dry-run.
        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var countBefore = await dbBefore.StudentActivityReadinessItems.CountAsync();

        // Run the dry-run endpoint.
        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/readiness-pool/review-scaffold/dry-run");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Count items after — must be identical.
        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var countAfter = await dbAfter.StudentActivityReadinessItems.CountAsync();

        Assert.Equal(countBefore, countAfter);
    }

    // ── Mastery validation returns expected shape ─────────────────────────────

    [Fact]
    public async Task MasteryValidation_AsAdmin_Returns200WithExpectedShape()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/mastery/validation-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        foreach (var field in new[]
        {
            "totalStudentsEvaluated", "totalObjectivesEvaluated",
            "countInsufficientEvidence", "countMastered",
            "countNeedsReview", "countNeedsPractice", "countAtRisk",
            "masteredExcludedFromNewLearning",
            "warnings", "generatedAt"
        })
        {
            Assert.True(body.TryGetProperty(field, out _), $"Missing field: {field}");
        }
    }

    // ── Mastery validation does NOT mutate the database ───────────────────────

    [Fact]
    public async Task MasteryValidation_DoesNotMutateDatabase()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var eventCountBefore = await dbBefore.Set<StudentLearningEvent>().CountAsync();
        var itemCountBefore  = await dbBefore.StudentActivityReadinessItems.CountAsync();

        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/mastery/validation-summary");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var eventCountAfter = await dbAfter.Set<StudentLearningEvent>().CountAsync();
        var itemCountAfter  = await dbAfter.StudentActivityReadinessItems.CountAsync();

        Assert.Equal(eventCountBefore, eventCountAfter);
        Assert.Equal(itemCountBefore, itemCountAfter);
    }

    // ── Mastery validation counts are non-negative ────────────────────────────

    [Fact]
    public async Task MasteryValidation_AllCountsAreNonNegative()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/mastery/validation-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        foreach (var field in new[]
        {
            "totalStudentsEvaluated", "totalObjectivesEvaluated",
            "countInsufficientEvidence", "countMastered",
            "countNeedsReview", "countNeedsPractice", "countAtRisk",
            "masteredExcludedFromNewLearning"
        })
        {
            var val = body.GetProperty(field).GetInt32();
            Assert.True(val >= 0, $"Field {field} was negative: {val}");
        }
    }

    // ── Dry-run counts are non-negative ───────────────────────────────────────

    [Fact]
    public async Task DryRun_AllCountsAreNonNegative()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/readiness-pool/review-scaffold/dry-run");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        foreach (var field in new[]
        {
            "studentsConsidered", "studentsEligibleForReview",
            "estimatedReviewOnlyConversions", "blockedDuplicates",
            "blockedInactiveObjectives", "estimatedNetNewReviewItems"
        })
        {
            var val = body.GetProperty(field).GetInt32();
            Assert.True(val >= 0, $"Field {field} was negative: {val}");
        }
    }

    // ── Pending-review list: auth guards ──────────────────────────────────────

    [Fact]
    public async Task PendingReview_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .GetAsync("/api/admin/readiness-pool/review-scaffold/pending-review");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PendingReview_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync(
            $"pendingreview403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/readiness-pool/review-scaffold/pending-review");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PendingReview_AsAdmin_Returns200WithArrayShape()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/readiness-pool/review-scaffold/pending-review");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    [Fact]
    public async Task PendingReview_DoesNotMutateDatabase()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var countBefore = await dbBefore.StudentActivityReadinessItems.CountAsync();

        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/readiness-pool/review-scaffold/pending-review");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var countAfter = await dbAfter.StudentActivityReadinessItems.CountAsync();

        Assert.Equal(countBefore, countAfter);
    }
}
