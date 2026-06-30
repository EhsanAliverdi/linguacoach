using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 17C writing signal application endpoints.
/// Verifies auth, admin-only access, summary shape, and safety invariants.
/// No real AI calls. No mastery/CEFR/objective changes verified.
/// </summary>
public sealed class WritingSignalApplicationTests
    : IClassFixture<WritingEvaluationEnabledTestFactory>
{
    private readonly WritingEvaluationEnabledTestFactory _factory;

    public WritingSignalApplicationTests(WritingEvaluationEnabledTestFactory factory)
    {
        _factory = factory;
    }

    // ── applied-signals-summary: 401 for anonymous ───────────────────────────

    [Fact]
    public async Task GetAppliedSignalsSummary_Anonymous_Returns401()
    {
        var resp = await _factory.CreateClient()
            .GetAsync("/api/admin/writing-evaluation/applied-signals-summary");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── applied-signals-summary: 403 for student ─────────────────────────────

    [Fact]
    public async Task GetAppliedSignalsSummary_Student_Returns403()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"ws_student_{Guid.NewGuid():N}@t.com");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/admin/writing-evaluation/applied-signals-summary");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── applied-signals-summary: 200 for admin with correct shape ────────────

    [Fact]
    public async Task GetAppliedSignalsSummary_Admin_Returns200WithShape()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await client.GetAsync("/api/admin/writing-evaluation/applied-signals-summary");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // Config status fields present
        body.GetProperty("masteryIntegrationEnabled").ValueKind.Should().Be(JsonValueKind.False,
            "ApplyMasterySignals is false by default");
        body.GetProperty("cefrUpdateAllowed").ValueKind.Should().Be(JsonValueKind.False,
            "CEFR updates are structurally disabled");
        body.GetProperty("objectiveCompletionAllowed").ValueKind.Should().Be(JsonValueKind.False,
            "objective completion is structurally disabled");
    }

    // ── signal-safety-summary: 401 for anonymous ─────────────────────────────

    [Fact]
    public async Task GetSignalSafetySummary_Anonymous_Returns401()
    {
        var resp = await _factory.CreateClient()
            .GetAsync("/api/admin/writing-evaluation/signal-safety-summary");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── signal-safety-summary: 403 for student ───────────────────────────────

    [Fact]
    public async Task GetSignalSafetySummary_Student_Returns403()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"ws_safety_student_{Guid.NewGuid():N}@t.com");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/admin/writing-evaluation/signal-safety-summary");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── signal-safety-summary: invariants confirmed ───────────────────────────

    [Fact]
    public async Task GetSignalSafetySummary_Admin_ConfirmsInvariants()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await client.GetAsync("/api/admin/writing-evaluation/signal-safety-summary");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("cefrUpdatesDisabled").GetBoolean().Should().BeTrue(
            "CEFR updates from writing AI are permanently disabled");
        body.GetProperty("objectiveCompletionsDisabled").GetBoolean().Should().BeTrue(
            "objective completion from writing AI is permanently disabled");
        body.GetProperty("learningPlanAutoRegenDisabled").GetBoolean().Should().BeTrue(
            "Learning Plan regeneration from writing AI is permanently disabled");
        body.GetProperty("invariantViolationsDetected").GetBoolean().Should().BeFalse(
            "no invariant violations should be present");
    }

    // ── per-student writing evaluations: 401 for anonymous ───────────────────

    [Fact]
    public async Task GetStudentWritingEvaluations_Anonymous_Returns401()
    {
        var resp = await _factory.CreateClient()
            .GetAsync($"/api/admin/students/{Guid.NewGuid()}/writing-evaluations");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── per-student writing evaluations: 403 for student ─────────────────────

    [Fact]
    public async Task GetStudentWritingEvaluations_Student_Returns403()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync(
            $"ws_student2_{Guid.NewGuid():N}@t.com");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync($"/api/admin/students/{Guid.NewGuid()}/writing-evaluations");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── per-student writing evaluations: 200 for admin ───────────────────────

    [Fact]
    public async Task GetStudentWritingEvaluations_Admin_Returns200()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var resp = await client.GetAsync($"/api/admin/students/{Guid.NewGuid()}/writing-evaluations");

        // Returns 200 with empty array for unknown student
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }
}
