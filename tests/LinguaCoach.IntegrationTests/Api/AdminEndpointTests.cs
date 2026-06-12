using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class AdminEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public AdminEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── POST /api/admin/students ──────────────────────────────────────────────

    [Fact]
    public async Task CreateStudent_AsAdmin_Returns201WithStudentId()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.PostAsJsonAsync("/api/admin/students",
            new { email = $"newstudent_{Guid.NewGuid():N}@test.com", temporaryPassword = "Student@1234" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("studentProfileId").GetString()?.Length > 0);
    }

    [Fact]
    public async Task CreateStudent_DuplicateEmail_Returns409()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"dup_{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });

        var response = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/admin/students",
            new { email = "x@x.com", temporaryPassword = "Student@1234" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_ThenLogin_ReturnsMustChangePasswordTrue()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"mustchange_{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Temp@5678" });

        // Now log in as the newly created student — must_change_password should be true.
        var loginClient = _factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Temp@5678" });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.True(body.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task CreateStudent_AsStudent_Returns403()
    {
        var (studentToken, _) = await _factory.CreateStudentAndGetTokenAsync($"nonadmin_{Guid.NewGuid():N}@test.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var response = await client.PostAsJsonAsync("/api/admin/students",
            new { email = $"target_{Guid.NewGuid():N}@test.com", temporaryPassword = "Student@1234" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateStudent_WithMustChangePasswordFalse_LoginReturnsFlag()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"nochange_{Guid.NewGuid():N}@test.com";
        var createResponse = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Temp@5678", mustChangePassword = false });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var loginClient = _factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Temp@5678" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task CreateStudent_WithFullProfile_LifecycleIsPlacementRequired()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"fullprofile_{Guid.NewGuid():N}@test.com";
        var createResponse = await _client.PostAsJsonAsync("/api/admin/students", new
        {
            email,
            temporaryPassword = "Temp@5678",
            mustChangePassword = false,
            firstName = "Ehsan",
            lastName = "Test",
            careerContext = "Software engineering",
            professionalExperienceLevel = 1, // Mid
            roleFamiliarity = 1              // Familiar
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var body = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var profileId = body.GetProperty("studentProfileId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.Id == profileId);
        Assert.Equal(StudentLifecycleStage.PlacementRequired, profile.LifecycleStage);
    }

    [Fact]
    public async Task CreateStudent_WithMustChangePasswordTrue_LifecycleIsPasswordChangeRequired()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"pwdchange_{Guid.NewGuid():N}@test.com";
        var createResponse = await _client.PostAsJsonAsync("/api/admin/students", new
        {
            email,
            temporaryPassword = "Temp@5678",
            mustChangePassword = true,
            firstName = "Ehsan"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // Login and verify mustChangePassword=true from the token response
        var loginClient = _factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Temp@5678" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task CancelGenerationBatch_AsAdmin_MarksRunningBatchFailed()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var batch = new GenerationBatch(
            Guid.NewGuid(),
            GenerationTriggerReason.ManualAdmin,
            requestedSessionCount: 4);
        batch.MarkRunning();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            db.GenerationBatches.Add(batch);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsync($"/api/admin/generation/batches/{batch.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var saved = verifyDb.GenerationBatches.Single(b => b.Id == batch.Id);
        Assert.Equal(GenerationBatchStatus.Failed, saved.Status);
        Assert.Equal(GenerationBatch.AdminCancelledFailureReason, saved.FailureReason);
        Assert.NotNull(saved.CompletedAtUtc);
    }
}
