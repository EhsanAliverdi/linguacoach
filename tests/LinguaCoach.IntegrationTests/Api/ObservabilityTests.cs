using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Infrastructure.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// T-Sprint6: Observability — correlation IDs, diagnostics endpoints, auth protection.
/// </summary>
public sealed class ObservabilityTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ObservabilityTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    // ── Correlation ID ────────────────────────────────────────────────────────

    [Fact]
    public async Task Request_WithoutCorrelationId_ResponseIncludesGeneratedId()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/health");

        Assert.True(resp.Headers.TryGetValues("X-Correlation-ID", out var values));
        var cid = values.First();
        Assert.False(string.IsNullOrWhiteSpace(cid));
        Assert.True(cid.Length >= 8, "Correlation ID should be at least 8 chars");
    }

    [Fact]
    public async Task Request_WithIncomingCorrelationId_SameIdReturnedInResponse()
    {
        var client = _factory.CreateClient();
        var incoming = "test-cid-abc123";
        client.DefaultRequestHeaders.Add("X-Correlation-ID", incoming);

        var resp = await client.GetAsync("/health");

        Assert.True(resp.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.Equal(incoming, values.First());
    }

    // ── Diagnostics endpoints — admin only ───────────────────────────────────

    [Fact]
    public async Task DiagnosticsStatus_WithNoAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/admin/diagnostics/status");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task DiagnosticsEvents_WithNoAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/admin/diagnostics/events");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task DiagnosticsStatus_WithStudentToken_Returns403()
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<LinguaCoach.Persistence.Identity.ApplicationUser>>();
        var tokenSvc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();

        var email = $"student_diag_{Guid.NewGuid():N}@test.com";
        var user = new LinguaCoach.Persistence.Identity.ApplicationUser
        {
            UserName = email, Email = email,
            Role = LinguaCoach.Domain.Enums.UserRole.Student,
            EmailConfirmed = true, MustChangePassword = false,
        };
        await userManager.CreateAsync(user, "Student@1234");
        var token = tokenSvc.GenerateToken(user.Id, user.Email!, user.Role);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/admin/diagnostics/status");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task DiagnosticsStatus_WithAdminToken_Returns200WithSafeFields()
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<LinguaCoach.Persistence.Identity.ApplicationUser>>();
        var tokenSvc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();

        var email = $"admin_diag_{Guid.NewGuid():N}@test.com";
        var admin = new LinguaCoach.Persistence.Identity.ApplicationUser
        {
            UserName = email, Email = email,
            Role = LinguaCoach.Domain.Enums.UserRole.Admin,
            EmailConfirmed = true, MustChangePassword = false,
        };
        await userManager.CreateAsync(admin, "Admin@1234");
        var token = tokenSvc.GenerateToken(admin.Id, admin.Email!, admin.Role);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/admin/diagnostics/status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // Must have required fields
        Assert.True(body.TryGetProperty("environment", out _));
        Assert.True(body.TryGetProperty("version", out _));
        Assert.True(body.TryGetProperty("serverTimeUtc", out _));
        Assert.True(body.TryGetProperty("database", out var db));
        Assert.True(db.TryGetProperty("reachable", out _));
        Assert.True(body.TryGetProperty("ai", out var ai));

        // Must NOT return API key — only providerConfigured boolean
        Assert.False(ai.TryGetProperty("apiKey", out _), "API key must never be returned");
        Assert.False(ai.TryGetProperty("key", out _), "API key must never be returned");
        Assert.True(ai.TryGetProperty("providerConfigured", out _));
    }

    [Fact]
    public async Task DiagnosticsEvents_WithAdminToken_Returns200()
    {
        await _factory.EnsureCreatedAsync();
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<LinguaCoach.Persistence.Identity.ApplicationUser>>();
        var tokenSvc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();

        var email = $"admin_events_{Guid.NewGuid():N}@test.com";
        var admin = new LinguaCoach.Persistence.Identity.ApplicationUser
        {
            UserName = email, Email = email,
            Role = LinguaCoach.Domain.Enums.UserRole.Admin,
            EmailConfirmed = true, MustChangePassword = false,
        };
        await userManager.CreateAsync(admin, "Admin@1234");
        var token = tokenSvc.GenerateToken(admin.Id, admin.Email!, admin.Role);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.GetAsync("/api/admin/diagnostics/events");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }

    // ── In-memory buffer unit-style tests ────────────────────────────────────

    [Fact]
    public void DiagnosticEventBuffer_RollsOverAtCapacity()
    {
        var buffer = new DiagnosticEventBuffer(capacity: 5, enabled: true);

        for (int i = 0; i < 10; i++)
        {
            buffer.Add(new DiagnosticEvent(
                DateTimeOffset.UtcNow, "Information", "Test", $"Message {i}",
                null, null, null, null, null));
        }

        Assert.True(buffer.Count <= 5, $"Buffer exceeded capacity: count={buffer.Count}");
    }

    [Fact]
    public void DiagnosticEventBuffer_WhenDisabled_DoesNotStore()
    {
        var buffer = new DiagnosticEventBuffer(capacity: 500, enabled: false);
        buffer.Add(new DiagnosticEvent(
            DateTimeOffset.UtcNow, "Information", "Test", "Message",
            null, null, null, null, null));

        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void DiagnosticEventBuffer_QueryByCorrelationId_ReturnsMatchingEvents()
    {
        var buffer = new DiagnosticEventBuffer(capacity: 500, enabled: true);

        buffer.Add(new DiagnosticEvent(DateTimeOffset.UtcNow, "Information", "Cat", "Msg A", "cid-abc", null, null, null, null));
        buffer.Add(new DiagnosticEvent(DateTimeOffset.UtcNow, "Information", "Cat", "Msg B", "cid-xyz", null, null, null, null));
        buffer.Add(new DiagnosticEvent(DateTimeOffset.UtcNow, "Information", "Cat", "Msg C", "cid-abc", null, null, null, null));

        var results = buffer.Query(correlationId: "cid-abc");

        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.Equal("cid-abc", e.CorrelationId));
    }

    [Fact]
    public void DiagnosticEventBuffer_QueryByLevel_ReturnsMatchingEvents()
    {
        var buffer = new DiagnosticEventBuffer(capacity: 500, enabled: true);

        buffer.Add(new DiagnosticEvent(DateTimeOffset.UtcNow, "Information", "Cat", "Info msg", null, null, null, null, null));
        buffer.Add(new DiagnosticEvent(DateTimeOffset.UtcNow, "Warning", "Cat", "Warn msg", null, null, null, null, null));
        buffer.Add(new DiagnosticEvent(DateTimeOffset.UtcNow, "Error", "Cat", "Error msg", null, null, null, null, null));

        var results = buffer.Query(level: "Warning");

        Assert.Single(results);
        Assert.Equal("Warning", results[0].Level);
    }
}
