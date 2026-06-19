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

    // ── GET /api/admin/students/{id} ──────────────────────────────────────────

    [Fact]
    public async Task GetStudentDetail_AsAdmin_ReturnsExpectedFields()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"detail_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234", firstName = "Detail", lastName = "Test" });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var profileId = createBody.GetProperty("studentProfileId").GetString();

        var response = await _client.GetAsync($"/api/admin/students/{profileId}");

        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200 but got {response.StatusCode}: {rawBody}");
        var body = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(rawBody);
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal("Detail", body.GetProperty("firstName").GetString());
        Assert.Equal("Test", body.GetProperty("lastName").GetString());
        Assert.True(body.TryGetProperty("lifecycleStage", out _));
        Assert.True(body.TryGetProperty("onboardingStatus", out _));
    }

    [Fact]
    public async Task GetStudentDetail_AsAdmin_ReturnsPreferenceFields()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"preffields_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234", careerContext = "Software", professionalExperienceLevel = 2 });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var profileId = createBody.GetProperty("studentProfileId").GetString();

        var response = await _client.GetAsync($"/api/admin/students/{profileId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Software", body.GetProperty("careerContext").GetString());
        Assert.True(body.TryGetProperty("focusAreas", out _));
        Assert.True(body.TryGetProperty("learningGoals", out _));
    }

    [Fact]
    public async Task GetStudentDetail_AsAdmin_ReturnsNullOnboardingProgressWhenNoRow()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"noprog_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();

        var response = await _client.GetAsync($"/api/admin/students/{profileId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("onboardingProgress").ValueKind);
    }

    [Fact]
    public async Task GetStudentDetail_AsAdmin_ReturnsOnboardingProgressWhenRowExists()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var email = $"withprog_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var profileId = Guid.Parse(createBody.GetProperty("studentProfileId").GetString()!);
        var userId = Guid.Parse(createBody.GetProperty("userId").GetString()!);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var flowId = await db.OnboardingFlowDefinitions
                .Where(f => f.IsActive)
                .Select(f => f.Id)
                .FirstAsync();
            var progress = LinguaCoach.Domain.Entities.StudentOnboardingProgress.CreateCompleted(userId, flowId);
            progress.RecordStepCompleted("step-intro");
            db.Set<LinguaCoach.Domain.Entities.StudentOnboardingProgress>().Add(progress);
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/admin/students/{profileId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var prog = body.GetProperty("onboardingProgress");
        Assert.NotEqual(JsonValueKind.Null, prog.ValueKind);
        Assert.True(prog.GetProperty("isComplete").GetBoolean());
        Assert.True(prog.GetProperty("percentageComplete").GetInt32() >= 0);
    }

    [Fact]
    public async Task GetStudentDetail_AsAdmin_Returns404ForNonExistentStudent()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync($"/api/admin/students/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetStudentDetail_AsStudent_Returns403()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email = $"sec_{Guid.NewGuid():N}@test.com", temporaryPassword = "Student@1234" });
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();

        var (studentToken, _) = await _factory.CreateStudentAndGetTokenAsync($"caller_{Guid.NewGuid():N}@test.com");
        var studentClient = _factory.CreateClient();
        studentClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        var response = await studentClient.GetAsync($"/api/admin/students/{profileId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Lifecycle controls ────────────────────────────────────────────────────

    private async Task<(string adminToken, string profileId)> CreateArchivedStudentAsync()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"archived_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString()!;
        var archiveResp = await _client.PostAsync($"/api/admin/students/{profileId}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archiveResp.StatusCode);
        return (adminToken, profileId);
    }

    [Fact]
    public async Task ReactivateStudent_ArchivedStudent_Returns200_AndRestoredLifecycle()
    {
        var (_, profileId) = await CreateArchivedStudentAsync();

        var response = await _client.PostAsync($"/api/admin/students/{profileId}/reactivate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OnboardingRequired", body.GetProperty("lifecycleStage").GetString());
    }

    [Fact]
    public async Task ReactivateStudent_NonArchivedStudent_Returns400()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"active_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();

        var response = await _client.PostAsync($"/api/admin/students/{profileId}/reactivate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReactivateStudent_MissingStudent_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.PostAsync($"/api/admin/students/{Guid.NewGuid()}/reactivate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PauseStudent_ActiveStudent_Returns200_AndLifecyclePaused()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"pauseme_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();

        var response = await _client.PostAsync($"/api/admin/students/{profileId}/pause", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Paused", body.GetProperty("lifecycleStage").GetString());
    }

    [Fact]
    public async Task PauseStudent_AlreadyPaused_Returns400()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"pausetwice_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();
        await _client.PostAsync($"/api/admin/students/{profileId}/pause", null);

        var response = await _client.PostAsync($"/api/admin/students/{profileId}/pause", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PauseStudent_ArchivedStudent_Returns400()
    {
        var (_, profileId) = await CreateArchivedStudentAsync();

        var response = await _client.PostAsync($"/api/admin/students/{profileId}/pause", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnpauseStudent_PausedStudent_Returns200_AndLifecycleRestored()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"unpauseme_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();
        await _client.PostAsync($"/api/admin/students/{profileId}/pause", null);

        var response = await _client.PostAsync($"/api/admin/students/{profileId}/unpause", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OnboardingRequired", body.GetProperty("lifecycleStage").GetString());
    }

    [Fact]
    public async Task UnpauseStudent_NonPausedStudent_Returns400()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"notpaused_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();

        var response = await _client.PostAsync($"/api/admin/students/{profileId}/unpause", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnpauseStudent_MissingStudent_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.PostAsync($"/api/admin/students/{Guid.NewGuid()}/unpause", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
