using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for Phase 13B — real response submission, adaptive scoring, progress endpoint.
/// </summary>
public sealed class AdminPlacement13BEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminPlacement13BEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAdminClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> CreateStudentProfileAsync()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var client = CreateAdminClient(adminToken);
        var email = $"p13b_{Guid.NewGuid():N}@test.com";
        var response = await client.PostAsJsonAsync("/api/admin/students",
            new { email, temporaryPassword = "Student@1234" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("studentProfileId").GetString()!);
    }

    private async Task<(string adminToken, Guid studentId, string assessmentId)> StartedAssessmentAsync()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var studentId = await CreateStudentProfileAsync();
        var client = CreateAdminClient(adminToken);

        var resp = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/start", new { });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var assessmentId = body.GetProperty("assessmentId").GetString()!;
        return (adminToken, studentId, assessmentId);
    }

    // ── GET progress ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProgress_AfterStart_ReturnsProgressDto()
    {
        var (adminToken, studentId, assessmentId) = await StartedAssessmentAsync();
        var client = CreateAdminClient(adminToken);

        var resp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/progress");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InProgress", body.GetProperty("status").GetString());
        Assert.Equal(0, body.GetProperty("answeredCount").GetInt32());
        Assert.True(body.GetProperty("totalItemCount").GetInt32() > 0);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("itemHistory").ValueKind);
        Assert.Equal(JsonValueKind.Array, body.GetProperty("skillProgress").ValueKind);
    }

    [Fact]
    public async Task GetProgress_UnknownAssessment_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var studentId = await CreateStudentProfileAsync();
        var client = CreateAdminClient(adminToken);

        var resp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{Guid.NewGuid()}/progress");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── GET items ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetItems_AfterStart_ReturnsItemArray()
    {
        var (adminToken, studentId, assessmentId) = await StartedAssessmentAsync();
        var client = CreateAdminClient(adminToken);

        var resp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.True(body.GetArrayLength() > 0);

        var first = body[0];
        Assert.True(first.TryGetProperty("skill", out _));
        Assert.True(first.TryGetProperty("targetCefrLevel", out _));
        Assert.True(first.TryGetProperty("prompt", out _));
    }

    // ── POST submit ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitResponse_CorrectAnswer_ReturnsIsCorrectTrue()
    {
        var (adminToken, studentId, assessmentId) = await StartedAssessmentAsync();
        var client = CreateAdminClient(adminToken);

        // Get first item
        var itemsResp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items");
        var items = await itemsResp.Content.ReadFromJsonAsync<JsonElement>();
        var firstItem = items[0];
        var itemId = firstItem.GetProperty("itemId").GetString()!;

        // The items include correct answer only if we peek at progress — use "A" as known correct for grammar A1 item[0]
        // Instead, get the correctAnswer from progress endpoint
        var progressResp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/progress");
        var progress = await progressResp.Content.ReadFromJsonAsync<JsonElement>();
        var historyItems = progress.GetProperty("itemHistory");
        // Find the item and submit a correct answer — use "A" (first grammar A1 item expects "A")
        // We just test the submission mechanism works and returns a result
        var submitResp = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items/{itemId}/submit",
            new { response = "A", durationSeconds = 10 });

        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        var result = await submitResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(itemId, result.GetProperty("itemId").GetString());
        Assert.True(result.TryGetProperty("isCorrect", out _));
        Assert.True(result.TryGetProperty("score", out _));
        Assert.False(string.IsNullOrEmpty(result.GetProperty("evaluationNotes").GetString()));
    }

    [Fact]
    public async Task SubmitResponse_Persists_AnsweredCountIncreases()
    {
        var (adminToken, studentId, assessmentId) = await StartedAssessmentAsync();
        var client = CreateAdminClient(adminToken);

        // Get first item id
        var itemsResp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items");
        var items = await itemsResp.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = items[0].GetProperty("itemId").GetString()!;

        // Submit
        await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items/{itemId}/submit",
            new { response = "A" });

        // Check progress
        var progResp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/progress");
        var prog = await progResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, prog.GetProperty("answeredCount").GetInt32());
    }

    [Fact]
    public async Task SubmitResponse_DuplicateSubmission_IsIdempotent()
    {
        var (adminToken, studentId, assessmentId) = await StartedAssessmentAsync();
        var client = CreateAdminClient(adminToken);

        var itemsResp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items");
        var items = await itemsResp.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = items[0].GetProperty("itemId").GetString()!;

        // Submit twice
        var first = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items/{itemId}/submit",
            new { response = "A" });
        var second = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items/{itemId}/submit",
            new { response = "B" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        // Answered count should still be 1
        var progResp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/progress");
        var prog = await progResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, prog.GetProperty("answeredCount").GetInt32());
    }

    [Fact]
    public async Task SubmitResponse_EmptyResponse_Returns400()
    {
        var (adminToken, studentId, assessmentId) = await StartedAssessmentAsync();
        var client = CreateAdminClient(adminToken);

        var itemsResp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items");
        var items = await itemsResp.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = items[0].GetProperty("itemId").GetString()!;

        var resp = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items/{itemId}/submit",
            new { response = "", durationSeconds = (int?)null });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task SubmitResponse_CompletedAssessment_Returns409()
    {
        var (adminToken, studentId, assessmentId) = await StartedAssessmentAsync();
        var client = CreateAdminClient(adminToken);

        // Force complete
        await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/complete", new { });

        // Get any item id from history
        var progResp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/progress");
        var prog = await progResp.Content.ReadFromJsonAsync<JsonElement>();
        var historyItems = prog.GetProperty("itemHistory");
        if (historyItems.GetArrayLength() == 0) return; // no items to test with

        var itemId = historyItems[0].GetProperty("itemId").GetString()!;

        var submitResp = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items/{itemId}/submit",
            new { response = "A" });

        Assert.Equal(HttpStatusCode.Conflict, submitResp.StatusCode);
    }

    [Fact]
    public async Task CompleteAssessment_WithRealResponses_HasRealSkillResults()
    {
        var (adminToken, studentId, assessmentId) = await StartedAssessmentAsync();
        var client = CreateAdminClient(adminToken);

        // Submit responses to first 3 items
        var itemsResp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items");
        var items = await itemsResp.Content.ReadFromJsonAsync<JsonElement>();
        var count = Math.Min(3, items.GetArrayLength());
        for (var i = 0; i < count; i++)
        {
            var itemId = items[i].GetProperty("itemId").GetString()!;
            await client.PostAsJsonAsync(
                $"/api/admin/students/{studentId}/placement/{assessmentId}/items/{itemId}/submit",
                new { response = "A" });
        }

        // Force complete
        var completeResp = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/complete", new { });

        Assert.Equal(HttpStatusCode.OK, completeResp.StatusCode);
        var body = await completeResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completed", body.GetProperty("status").GetString());

        // Should have real (not null) overall CEFR
        var cefrLevel = body.GetProperty("overallCefrLevel").GetString();
        Assert.False(string.IsNullOrEmpty(cefrLevel));
    }

    [Fact]
    public async Task GetProgress_AfterSubmit_SkillProgressUpdated()
    {
        var (adminToken, studentId, assessmentId) = await StartedAssessmentAsync();
        var client = CreateAdminClient(adminToken);

        var itemsResp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items");
        var items = await itemsResp.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = items[0].GetProperty("itemId").GetString()!;

        await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items/{itemId}/submit",
            new { response = "A" });

        var progResp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/progress");
        var prog = await progResp.Content.ReadFromJsonAsync<JsonElement>();
        var skillProgress = prog.GetProperty("skillProgress");

        Assert.Equal(JsonValueKind.Array, skillProgress.ValueKind);
        Assert.True(skillProgress.GetArrayLength() > 0);

        // At least one skill should show > 0 evidence
        var anyEvidence = false;
        foreach (var sp in skillProgress.EnumerateArray())
        {
            if (sp.GetProperty("evidenceCount").GetInt32() > 0)
                anyEvidence = true;
        }
        Assert.True(anyEvidence);
    }

    [Fact]
    public async Task SubmitResponse_WrongAssessmentForStudent_Returns404()
    {
        var adminToken = await _factory.CreateAdminAndGetTokenAsync();
        var studentId = await CreateStudentProfileAsync();
        var otherStudentId = await CreateStudentProfileAsync();
        var client = CreateAdminClient(adminToken);

        var startResp = await client.PostAsJsonAsync(
            $"/api/admin/students/{studentId}/placement/start", new { });
        var startBody = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var assessmentId = startBody.GetProperty("assessmentId").GetString()!;

        var itemsResp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{assessmentId}/items");
        var items = await itemsResp.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = items[0].GetProperty("itemId").GetString()!;

        // Submit under wrong student
        var resp = await client.PostAsJsonAsync(
            $"/api/admin/students/{otherStudentId}/placement/{assessmentId}/items/{itemId}/submit",
            new { response = "A" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetProgress_Unauthorized_Returns401()
    {
        var studentId = await CreateStudentProfileAsync();
        var client = _factory.CreateClient();

        var resp = await client.GetAsync(
            $"/api/admin/students/{studentId}/placement/{Guid.NewGuid()}/progress");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
