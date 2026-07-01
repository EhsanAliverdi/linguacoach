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
/// Phase 19C — admin monitoring for the Practice Gym review scaffold pilot.
/// Verifies: auth guards, pilot flag/count reporting, and safe defaults with pilot off.
/// </summary>
public sealed class ReviewScaffoldPilotSummaryTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ReviewScaffoldPilotSummaryTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task PilotSummary_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient()
            .GetAsync("/api/admin/readiness-pool/review-scaffold/pilot-summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PilotSummary_AsStudent_Returns403()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync(
            $"pilotsummary403_{Guid.NewGuid():N}@t.com");
        var response = await ClientWithToken(token)
            .GetAsync("/api/admin/readiness-pool/review-scaffold/pilot-summary");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PilotSummary_DefaultConfig_ReportsPilotDisabledAndTodayInsertionDisabled()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/readiness-pool/review-scaffold/pilot-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.False(body.GetProperty("practiceGymPilotEnabled").GetBoolean());
        Assert.False(body.GetProperty("allowTodayLessonInsertion").GetBoolean());
    }

    [Fact]
    public async Task PilotSummary_ApprovedItem_CountedAsApprovedButNotStudentVisible_WhenPilotOff()
    {
        var (_, userId) = await _factory.CreateStudentAndGetTokenAsync($"pilotcount_{Guid.NewGuid():N}@t.com");
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);

            var item = new StudentActivityReadinessItem(
                studentId: profile.Id,
                source: ReadinessPoolSource.PracticeGym,
                targetCefrLevel: "B1",
                routingReason: RoutingReason.Review,
                isLowerLevelContent: true,
                requiresAdminReview: true);
            item.MarkGenerating();
            item.MarkReady();
            item.ApproveAdminReview(Guid.NewGuid());

            db.StudentActivityReadinessItems.Add(item);
            await db.SaveChangesAsync();
        }

        var response = await ClientWithToken(adminToken)
            .GetAsync("/api/admin/readiness-pool/review-scaffold/pilot-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.True(body.GetProperty("approvedCount").GetInt32() >= 1);
        // Pilot is off by default in this environment, so nothing is student-visible yet.
        Assert.Equal(0, body.GetProperty("studentVisibleCount").GetInt32());
    }
}
