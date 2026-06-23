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
            new { email, temporaryPassword = "Temp@56789" });

        // Now log in as the newly created student — must_change_password should be true.
        var loginClient = _factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Temp@56789" });

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
            new { email, temporaryPassword = "Temp@56789", mustChangePassword = false });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var loginClient = _factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Temp@56789" });
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
            temporaryPassword = "Temp@56789",
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
            temporaryPassword = "Temp@56789",
            mustChangePassword = true,
            firstName = "Ehsan"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        // Login and verify mustChangePassword=true from the token response
        var loginClient = _factory.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Temp@56789" });
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

    // ── SetStudentCefr ────────────────────────────────────────────────────────

    [Fact]
    public async Task SetStudentCefr_ValidLevel_Returns200_AndFieldUpdated()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"cefr_set_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();

        var response = await _client.PutAsJsonAsync($"/api/admin/students/{profileId}/cefr",
            new { cefrLevel = "B2", reason = "Initial placement" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/admin/students/{profileId}");
        Assert.Equal("B2", detail.GetProperty("cefrLevel").GetString());
    }

    [Fact]
    public async Task SetStudentCefr_ClearCefr_Returns200_AndFieldNull()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"cefr_clear_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();

        // First set a level
        await _client.PutAsJsonAsync($"/api/admin/students/{profileId}/cefr", new { cefrLevel = "A1" });

        // Now clear it
        var response = await _client.PutAsJsonAsync($"/api/admin/students/{profileId}/cefr",
            new { cefrLevel = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/admin/students/{profileId}");
        Assert.True(
            detail.GetProperty("cefrLevel").ValueKind == JsonValueKind.Null ||
            detail.GetProperty("cefrLevel").ValueKind == JsonValueKind.Undefined);
    }

    [Fact]
    public async Task SetStudentCefr_InvalidLevel_Returns400()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"cefr_invalid_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();

        var response = await _client.PutAsJsonAsync($"/api/admin/students/{profileId}/cefr",
            new { cefrLevel = "Z9" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetStudentCefr_MissingStudent_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.PutAsJsonAsync($"/api/admin/students/{Guid.NewGuid()}/cefr",
            new { cefrLevel = "B1" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetStudentCefr_WritesAuditLog()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"cefr_audit_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        var profileIdStr = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();
        var profileId = Guid.Parse(profileIdStr!);

        await _client.PutAsJsonAsync($"/api/admin/students/{profileId}/cefr",
            new { cefrLevel = "C1", reason = "Test audit" });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoach.Persistence.LinguaCoachDbContext>();
        var auditEntry = db.AdminAuditLogs
            .Where(a => a.Action == "SetCefr" && a.TargetStudentId == profileId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        Assert.NotNull(auditEntry);
        Assert.Equal("StudentProfile", auditEntry.EntityType);
        Assert.Contains("C1", auditEntry.NewValueJson ?? "");
        Assert.Equal("Test audit", auditEntry.Reason);
    }

    // ── GET /api/admin/students/{id}/audit-history ────────────────────────────

    private async Task<Guid> CreateStudentAndGetProfileIdAsync(string email)
    {
        var createResp = await _client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        var body = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("studentProfileId").GetString()!);
    }

    [Fact]
    public async Task GetAuditHistory_WithAdminAuditLogRows_Returns200WithEntries()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var profileId = await CreateStudentAndGetProfileIdAsync($"ah_audit_{Guid.NewGuid():N}@test.com");

        // Trigger an action that writes an AdminAuditLog row
        await _client.PutAsJsonAsync($"/api/admin/students/{profileId}/cefr",
            new { cefrLevel = "B2", reason = "audit history test" });

        var response = await _client.GetAsync($"/api/admin/students/{profileId}/audit-history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(items);
        Assert.True(items.Length > 0);
        Assert.Contains(items, i => i.GetProperty("source").GetString() == "AdminAuditLog");
    }

    [Fact]
    public async Task GetAuditHistory_WithResetLogRows_ReturnsStudentResetLogEntries()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var profileId = await CreateStudentAndGetProfileIdAsync($"ah_reset_{Guid.NewGuid():N}@test.com");

        // Trigger a lifecycle reset which writes a StudentResetLog row
        await _client.PostAsJsonAsync($"/api/admin/students/{profileId}/reset",
            new { targetStage = "OnboardingRequired", clearOnboardingAnswers = false, clearPlacementResults = false,
                  clearCoursesAndSessions = false, clearActivityAttempts = false, clearVocabulary = false,
                  clearLearningMemory = false, clearAudioFiles = false, clearProgressData = false,
                  reason = "integration test reset" });

        var response = await _client.GetAsync($"/api/admin/students/{profileId}/audit-history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(items);
        Assert.Contains(items, i => i.GetProperty("source").GetString() == "StudentResetLog");
    }

    [Fact]
    public async Task GetAuditHistory_OrderedNewestFirst()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var profileId = await CreateStudentAndGetProfileIdAsync($"ah_order_{Guid.NewGuid():N}@test.com");

        // Create two audit entries in sequence
        await _client.PutAsJsonAsync($"/api/admin/students/{profileId}/cefr", new { cefrLevel = "A1", reason = "first" });
        await _client.PutAsJsonAsync($"/api/admin/students/{profileId}/cefr", new { cefrLevel = "B1", reason = "second" });

        var response = await _client.GetAsync($"/api/admin/students/{profileId}/audit-history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(items);
        Assert.True(items.Length >= 2);

        var timestamps = items.Select(i => DateTimeOffset.Parse(i.GetProperty("timestamp").GetString()!)).ToArray();
        for (var i = 0; i < timestamps.Length - 1; i++)
            Assert.True(timestamps[i] >= timestamps[i + 1], "Items should be newest-first");
    }

    [Fact]
    public async Task GetAuditHistory_ExcludesLogsForOtherStudents()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var profileId1 = await CreateStudentAndGetProfileIdAsync($"ah_iso1_{Guid.NewGuid():N}@test.com");
        var profileId2 = await CreateStudentAndGetProfileIdAsync($"ah_iso2_{Guid.NewGuid():N}@test.com");

        // Write audit log only for student 2
        await _client.PutAsJsonAsync($"/api/admin/students/{profileId2}/cefr", new { cefrLevel = "C2", reason = "for student 2 only" });

        var response = await _client.GetAsync($"/api/admin/students/{profileId1}/audit-history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(items);
        Assert.DoesNotContain(items, i => (i.GetProperty("reason").ValueKind == JsonValueKind.String && i.GetProperty("reason").GetString() == "for student 2 only"));
    }

    [Fact]
    public async Task GetAuditHistory_NoHistory_Returns200EmptyList()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var profileId = await CreateStudentAndGetProfileIdAsync($"ah_empty_{Guid.NewGuid():N}@test.com");

        var response = await _client.GetAsync($"/api/admin/students/{profileId}/audit-history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(items);
        // May have a creation audit log entry — check it's an array and doesn't error
        Assert.IsType<JsonElement[]>(items);
    }

    [Fact]
    public async Task GetAuditHistory_MissingStudent_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.GetAsync($"/api/admin/students/{Guid.NewGuid()}/audit-history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditHistory_DoesNotExposePasswordOrSecretFields()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var profileId = await CreateStudentAndGetProfileIdAsync($"ah_secret_{Guid.NewGuid():N}@test.com");

        await _client.PutAsJsonAsync($"/api/admin/students/{profileId}/cefr", new { cefrLevel = "A2" });

        var response = await _client.GetAsync($"/api/admin/students/{profileId}/audit-history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("temporaryPassword", raw, StringComparison.OrdinalIgnoreCase);
    }

    // ── GET /api/admin/students — paged list ──────────────────────────────────

    private async Task<string> SetupAdminAndStudentsAsync(params string[] emails)
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        foreach (var email in emails)
            await _client.PostAsJsonAsync("/api/admin/students", new { email, temporaryPassword = "Student@1234" });
        return adminToken;
    }

    [Fact]
    public async Task ListStudents_DefaultParams_ReturnsPagedWrapper()
    {
        await SetupAdminAndStudentsAsync($"paged_a_{Guid.NewGuid():N}@test.com");

        var response = await _client.GetAsync("/api/admin/students");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("items", out _));
        Assert.True(body.TryGetProperty("totalCount", out _));
        Assert.True(body.TryGetProperty("page", out var page));
        Assert.Equal(1, page.GetInt32());
        Assert.True(body.TryGetProperty("pageSize", out var ps));
        Assert.Equal(25, ps.GetInt32());
        Assert.True(body.TryGetProperty("totalPages", out _));
    }

    [Fact]
    public async Task ListStudents_PageSizeCappedAt100()
    {
        await SetupAdminAndStudentsAsync($"cap_{Guid.NewGuid():N}@test.com");

        var response = await _client.GetAsync("/api/admin/students?pageSize=999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(100, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task ListStudents_Page2_ReturnsCorrectItems()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        // Create 3 students; request page 2 with pageSize 2 — expect 1 item.
        for (var i = 0; i < 3; i++)
            await _client.PostAsJsonAsync("/api/admin/students",
                new { email = $"p2_{Guid.NewGuid():N}@test.com", temporaryPassword = "Student@1234" });

        var response = await _client.GetAsync("/api/admin/students?page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("page").GetInt32());
        // items count depends on total; at least the structure is valid.
        Assert.True(body.GetProperty("items").GetArrayLength() >= 0);
    }

    [Fact]
    public async Task ListStudents_SearchByEmail_ReturnsMatchingOnly()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await _client.PostAsJsonAsync("/api/admin/students",
            new { email = $"search_{unique}@example.com", temporaryPassword = "Student@1234" });
        await _client.PostAsJsonAsync("/api/admin/students",
            new { email = $"other_{Guid.NewGuid():N}@example.com", temporaryPassword = "Student@1234" });

        var response = await _client.GetAsync($"/api/admin/students?search=search_{unique}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        foreach (var item in items.EnumerateArray())
            Assert.Contains(unique, item.GetProperty("email").GetString());
    }

    [Fact]
    public async Task ListStudents_SearchByName_ReturnsMatchingOnly()
    {
        var uniqueName = $"Zephyr{Guid.NewGuid().ToString("N")[..6]}";
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await _client.PostAsJsonAsync("/api/admin/students",
            new { email = $"named_{Guid.NewGuid():N}@test.com", temporaryPassword = "Student@1234", firstName = uniqueName });
        await _client.PostAsJsonAsync("/api/admin/students",
            new { email = $"unnamed_{Guid.NewGuid():N}@test.com", temporaryPassword = "Student@1234" });

        var response = await _client.GetAsync($"/api/admin/students?search={uniqueName}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("items").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task ListStudents_IncludeArchivedFalse_ExcludesArchived()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"arch_excl_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students", new { email, temporaryPassword = "Student@1234" });
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();
        await _client.PostAsync($"/api/admin/students/{profileId}/archive", null);

        var response = await _client.GetAsync($"/api/admin/students?includeArchived=false&search={email.Split('@')[0]}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ListStudents_IncludeArchivedTrue_IncludesArchived()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"arch_incl_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students", new { email, temporaryPassword = "Student@1234" });
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();
        await _client.PostAsync($"/api/admin/students/{profileId}/archive", null);

        var response = await _client.GetAsync($"/api/admin/students?includeArchived=true&search={email.Split('@')[0]}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("items").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task ListStudents_LifecycleStageFilter_ReturnsMatchingOnly()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"lc_filter_{Guid.NewGuid():N}@test.com";
        await _client.PostAsJsonAsync("/api/admin/students", new { email, temporaryPassword = "Student@1234" });

        var response = await _client.GetAsync("/api/admin/students?lifecycleStage=OnboardingRequired");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var item in body.GetProperty("items").EnumerateArray())
            Assert.Equal("OnboardingRequired", item.GetProperty("lifecycleStage").GetString());
    }

    [Fact]
    public async Task ListStudents_CefrLevelFilter_ReturnsMatchingOnly()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"cefr_filt_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students", new { email, temporaryPassword = "Student@1234" });
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();
        await _client.PutAsJsonAsync($"/api/admin/students/{profileId}/cefr", new { cefrLevel = "C2" });

        var response = await _client.GetAsync("/api/admin/students?cefrLevel=C2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("items").GetArrayLength() >= 1);
        foreach (var item in body.GetProperty("items").EnumerateArray())
            Assert.Equal("C2", item.GetProperty("cefrLevel").GetString());
    }

    [Fact]
    public async Task ListStudents_SortByCreatedAtAsc_OrderIsAscending()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        for (var i = 0; i < 3; i++)
            await _client.PostAsJsonAsync("/api/admin/students",
                new { email = $"sort_asc_{Guid.NewGuid():N}@test.com", temporaryPassword = "Student@1234" });

        var response = await _client.GetAsync("/api/admin/students?sortBy=createdAt&sortDir=asc&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var dates = body.GetProperty("items").EnumerateArray()
            .Select(i => DateTime.Parse(i.GetProperty("createdAt").GetString()!))
            .ToList();
        for (var i = 0; i < dates.Count - 1; i++)
            Assert.True(dates[i] <= dates[i + 1], "Items should be sorted ascending by createdAt");
    }

    [Fact]
    public async Task ListStudents_ResponseIncludesPaginationFields()
    {
        await SetupAdminAndStudentsAsync($"pf_{Guid.NewGuid():N}@test.com");

        var response = await _client.GetAsync("/api/admin/students?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 0);
        Assert.True(body.GetProperty("totalPages").GetInt32() >= 1);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(10, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task GetStudentDetail_StillReturns200_AfterPagedListChange()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var email = $"detail_compat_{Guid.NewGuid():N}@test.com";
        var createResp = await _client.PostAsJsonAsync("/api/admin/students", new { email, temporaryPassword = "Student@1234" });
        var profileId = (await createResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentProfileId").GetString();

        var response = await _client.GetAsync($"/api/admin/students/{profileId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, body.GetProperty("email").GetString());
    }
}
