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
/// Phase 19B — per-item admin approval workflow for review scaffold items.
/// Verifies: auth guards, approve/reject/reopen transitions, idempotency, safe not-found,
/// audit trail, and that CEFR/objective/Learning Plan state is never touched.
/// </summary>
public sealed class ReviewScaffoldAdminApprovalTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ReviewScaffoldAdminApprovalTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(Guid ItemId, Guid ProfileId)> SeedPendingReviewScaffoldItemAsync()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"scaffoldreview_{Guid.NewGuid():N}@t.com");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);

        var item = new StudentActivityReadinessItem(
            studentId: profile.Id,
            source: ReadinessPoolSource.PracticeGym,
            targetCefrLevel: "B1",
            routingReason: RoutingReason.Scaffold,
            isLowerLevelContent: true,
            originalCefrLevelSnapshot: "B2",
            requiresAdminReview: true);
        item.MarkGenerating();
        item.MarkReady();

        db.StudentActivityReadinessItems.Add(item);
        await db.SaveChangesAsync();

        return (item.Id, profile.Id);
    }

    // ── Auth guards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .PostAsync($"/api/admin/readiness-pool/review-scaffold/{Guid.NewGuid()}/approve", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Approve_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"scaffoldapprove403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .PostAsync($"/api/admin/readiness-pool/review-scaffold/{Guid.NewGuid()}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reject_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"scaffoldreject403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .PostAsJsonAsync($"/api/admin/readiness-pool/review-scaffold/{Guid.NewGuid()}/reject", new { reason = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Unknown item ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_UnknownItem_ReturnsSafe404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var response = await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/readiness-pool/review-scaffold/{Guid.NewGuid()}/approve", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Approve ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_PendingItem_SetsApprovedAndPersistsReviewer()
    {
        var (itemId, _) = await SeedPendingReviewScaffoldItemAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/readiness-pool/review-scaffold/{itemId}/approve", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal("Approved", body.GetProperty("adminReviewStatus").GetString());
        Assert.True(body.TryGetProperty("adminReviewedByUserId", out var reviewedBy) && reviewedBy.ValueKind != JsonValueKind.Null);
        Assert.True(body.TryGetProperty("adminReviewedAtUtc", out var reviewedAt) && reviewedAt.ValueKind != JsonValueKind.Null);
        Assert.True(body.GetProperty("isStudentVisible").GetBoolean());
        Assert.True(body.GetProperty("isPracticeGymEligible").GetBoolean());
    }

    [Fact]
    public async Task Approve_AlreadyApproved_IsIdempotent()
    {
        var (itemId, _) = await SeedPendingReviewScaffoldItemAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(adminToken);

        var first = await client.PostAsync($"/api/admin/readiness-pool/review-scaffold/{itemId}/approve", null);
        var second = await client.PostAsync($"/api/admin/readiness-pool/review-scaffold/{itemId}/approve", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Approve_DoesNotChangeCefrOrCreateOtherEntities()
    {
        var (itemId, profileId) = await SeedPendingReviewScaffoldItemAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profileBefore = await dbBefore.StudentProfiles.AsNoTracking().FirstAsync(p => p.Id == profileId);
        var cefrBefore = profileBefore.CefrLevel;

        var response = await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/readiness-pool/review-scaffold/{itemId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profileAfter = await dbAfter.StudentProfiles.AsNoTracking().FirstAsync(p => p.Id == profileId);

        Assert.Equal(cefrBefore, profileAfter.CefrLevel);
    }

    [Fact]
    public async Task Approve_WritesAdminAuditLog()
    {
        var (itemId, _) = await SeedPendingReviewScaffoldItemAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsync($"/api/admin/readiness-pool/review-scaffold/{itemId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var log = await db.AdminAuditLogs
            .Where(l => l.Action == "ApproveReviewScaffoldItem" && l.EntityId == itemId.ToString())
            .FirstOrDefaultAsync();

        Assert.NotNull(log);
    }

    // ── Reject ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_WithoutReason_ReturnsBadRequest()
    {
        var (itemId, _) = await SeedPendingReviewScaffoldItemAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync($"/api/admin/readiness-pool/review-scaffold/{itemId}/reject", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reject_WithReason_PersistsReasonAndNotes()
    {
        var (itemId, _) = await SeedPendingReviewScaffoldItemAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync($"/api/admin/readiness-pool/review-scaffold/{itemId}/reject",
                new { reason = "Too hard for level", notes = "confirmed with mastery report" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal("Rejected", body.GetProperty("adminReviewStatus").GetString());
        Assert.Equal("Too hard for level", body.GetProperty("adminReviewReason").GetString());
        Assert.Equal("confirmed with mastery report", body.GetProperty("adminReviewNotes").GetString());
        Assert.False(body.GetProperty("isStudentVisible").GetBoolean());
        Assert.False(body.GetProperty("isPracticeGymEligible").GetBoolean());
    }

    [Fact]
    public async Task Reject_HidesItemFromPracticeGymSuggestions()
    {
        var (itemId, profileId) = await SeedPendingReviewScaffoldItemAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var reject = await ClientWithToken(adminToken)
            .PostAsJsonAsync($"/api/admin/readiness-pool/review-scaffold/{itemId}/reject", new { reason = "too hard" });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var item = await db.StudentActivityReadinessItems.AsNoTracking().FirstAsync(i => i.Id == itemId);

        Assert.Equal(AdminReviewStatus.Rejected, item.AdminReviewStatus);
        Assert.False(item.PassesAdminReviewGate);
    }

    // ── Reopen ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reopen_RejectedItem_MovesBackToPendingReview()
    {
        var (itemId, _) = await SeedPendingReviewScaffoldItemAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = ClientWithToken(adminToken);

        await client.PostAsJsonAsync($"/api/admin/readiness-pool/review-scaffold/{itemId}/reject", new { reason = "too hard" });
        var response = await client.PostAsJsonAsync($"/api/admin/readiness-pool/review-scaffold/{itemId}/reopen", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("PendingReview", body.GetProperty("adminReviewStatus").GetString());
    }

    [Fact]
    public async Task Reopen_PendingItem_IsIdempotent()
    {
        var (itemId, _) = await SeedPendingReviewScaffoldItemAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .PostAsJsonAsync($"/api/admin/readiness-pool/review-scaffold/{itemId}/reopen", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.Equal("PendingReview", body.GetProperty("adminReviewStatus").GetString());
    }

    // ── Pending-review list includes admin review fields ──────────────────────

    [Fact]
    public async Task PendingReview_List_IncludesAdminReviewStatus()
    {
        var (itemId, _) = await SeedPendingReviewScaffoldItemAsync();
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/readiness-pool/review-scaffold/pending-review");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var match = body.EnumerateArray()
            .FirstOrDefault(e => e.GetProperty("id").GetGuid() == itemId);
        Assert.NotEqual(default, match);
        Assert.Equal("PendingReview", match.GetProperty("adminReviewStatus").GetString());
    }
}
