using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Placement;
using LinguaCoach.Infrastructure.Placement;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Placement Assessment MVP — integration tests for all PlacementController endpoints.
/// Uses the deterministic FakePlacementEvaluator (no live AI).
/// </summary>
public sealed class PlacementEndpointTests : IClassFixture<PlacementTestFactory>
{
    private readonly PlacementTestFactory _factory;

    public PlacementEndpointTests(PlacementTestFactory factory) => _factory = factory;

    // ── Auth ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Status_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/placement/status");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Start_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/placement/start", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Status / start / resume ─────────────────────────────────────────────────

    [Fact]
    public async Task Status_NewStudent_ReturnsNotStartedFirstSection()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pl_status_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/placement/status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NotStarted", body.GetProperty("status").GetString());
        Assert.Equal("self_check", body.GetProperty("currentSectionKey").GetString());
        Assert.Equal(6, body.GetProperty("totalSections").GetInt32());
    }

    [Fact]
    public async Task Start_SetsInProgressAndLifecyclePlacementInProgress()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"pl_start_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.PostAsync("/api/placement/start", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("InProgress", body.GetProperty("status").GetString());
        Assert.Equal("PlacementInProgress", body.GetProperty("lifecycleStage").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        Assert.Equal(StudentLifecycleStage.PlacementInProgress, profile.LifecycleStage);
    }

    [Fact]
    public async Task Current_HidesCorrectOptions()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pl_current_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        await client.PostAsync("/api/placement/start", null);

        // Advance to the vocab_grammar section which has correctOption metadata.
        await SaveSelfCheckAsync(client);

        var resp = await client.GetAsync("/api/placement/current");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var raw = await resp.Content.ReadAsStringAsync();

        // correctOption must never be exposed to the student.
        Assert.DoesNotContain("correctOption", raw, StringComparison.OrdinalIgnoreCase);

        var body = JsonSerializer.Deserialize<JsonElement>(raw);
        Assert.Equal("vocab_grammar", body.GetProperty("section").GetProperty("key").GetString());
    }

    // ── Save answers + resume ───────────────────────────────────────────────────

    [Fact]
    public async Task SaveAnswers_AdvancesToNextSection()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pl_save_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        await client.PostAsync("/api/placement/start", null);

        var status = await SaveSelfCheckAsync(client);
        Assert.Equal("vocab_grammar", status.GetProperty("currentSectionKey").GetString());
        Assert.Equal("InProgress", status.GetProperty("status").GetString());
    }

    [Fact]
    public async Task SaveAnswers_UnknownSection_Returns400()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pl_badsec_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        await client.PostAsync("/api/placement/start", null);

        var resp = await client.PostAsJsonAsync("/api/placement/answers", new
        {
            sectionKey = "not_a_section",
            answers = new[] { new { questionKey = "x", responseText = "y", selectedOption = (string?)null } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Resume_ReturnsCurrentSectionAfterPartialProgress()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pl_resume_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        await client.PostAsync("/api/placement/start", null);
        await SaveSelfCheckAsync(client);

        // Simulate "returning" — a fresh status read should still point at vocab_grammar.
        var resp = await client.GetAsync("/api/placement/status");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("vocab_grammar", body.GetProperty("currentSectionKey").GetString());
    }

    // ── Complete + result ───────────────────────────────────────────────────────

    [Fact]
    public async Task Complete_EvaluatesAndAdvancesLifecycleToCourseReady()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"pl_complete_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        await CompleteFullPlacementAsync(client, allCorrect: true);

        var resp = await client.PostAsync("/api/placement/complete", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("isCompleted").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("estimatedOverallLevel").GetString()));
        Assert.True(result.GetProperty("skillLevels").GetArrayLength() > 0);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        Assert.Equal(StudentLifecycleStage.CourseReady, profile.LifecycleStage);
        Assert.False(string.IsNullOrWhiteSpace(profile.CefrLevel));

        // Placement result is persisted on the assessment row.
        var assessment = db.PlacementAssessments.First(a => a.StudentProfileId == profile.Id);
        Assert.Equal(PlacementStatus.Completed, assessment.Status);
        Assert.False(string.IsNullOrWhiteSpace(assessment.ResultJson));
        Assert.False(string.IsNullOrWhiteSpace(assessment.OverallEstimatedLevel));
    }

    [Fact]
    public async Task Complete_SeedsSkillProfileFromPlacement()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"pl_skills_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // Answer everything WRONG so skills land below B1 and get marked weak.
        await CompleteFullPlacementAsync(client, allCorrect: false);
        await client.PostAsync("/api/placement/complete", null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var skills = db.StudentSkillProfiles.Where(s => s.StudentProfileId == profile.Id).ToList();
        Assert.NotEmpty(skills);
        Assert.Contains(skills, s => s.IsWeak);
    }

    [Fact]
    public async Task GetResult_BeforeCompletion_Returns400()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pl_noresult_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        await client.PostAsync("/api/placement/start", null);

        var resp = await client.GetAsync("/api/placement/result");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetResult_AfterCompletion_ReturnsResult()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pl_getresult_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);
        await CompleteFullPlacementAsync(client, allCorrect: true);
        await client.PostAsync("/api/placement/complete", null);

        var resp = await client.GetAsync("/api/placement/result");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isCompleted").GetBoolean());
    }

    // ── Ownership ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Placement_IsIsolatedPerStudent()
    {
        var (tokenA, _) = await _factory.CreateOnboardedStudentAsync($"pl_iso_a_{Guid.NewGuid():N}@test.com");
        var (tokenB, userB) = await _factory.CreateOnboardedStudentAsync($"pl_iso_b_{Guid.NewGuid():N}@test.com");

        var clientA = ClientWithToken(tokenA);
        await CompleteFullPlacementAsync(clientA, allCorrect: true);
        await clientA.PostAsync("/api/placement/complete", null);

        // Student B has not started — their status must still be NotStarted.
        var clientB = ClientWithToken(tokenB);
        var resp = await clientB.GetAsync("/api/placement/status");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NotStarted", body.GetProperty("status").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profileB = db.StudentProfiles.First(p => p.UserId == userB);
        Assert.False(db.PlacementAssessments.Any(a => a.StudentProfileId == profileB.Id));
    }

    // ── Onboarding sets PlacementRequired ───────────────────────────────────────

    [Fact]
    public async Task Onboarding_Completion_SetsLifecyclePlacementRequired()
    {
        var (_, userId) = await _factory.CreateOnboardedStudentAsync($"pl_onboard_{Guid.NewGuid():N}@test.com");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);

        // CreateOnboardedStudentAsync completes onboarding but does not start placement.
        Assert.True(profile.LifecycleStage is StudentLifecycleStage.PlacementRequired);
    }

    [Fact]
    public async Task Status_AfterOnboardingCompletion_ReturnsLifecyclePlacementRequired()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"pl_status_after_onboard_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/placement/status");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PlacementRequired", body.GetProperty("lifecycleStage").GetString());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static async Task<JsonElement> SaveSelfCheckAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/placement/answers", new
        {
            sectionKey = "self_check",
            answers = new[]
            {
                new { questionKey = "confidence_email", responseText = (string?)null, selectedOption = "3" },
                new { questionKey = "self_level", responseText = (string?)null, selectedOption = "B1" },
            }
        });
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task CompleteFullPlacementAsync(HttpClient client, bool allCorrect)
    {
        await client.PostAsync("/api/placement/start", null);

        // self_check
        await client.PostAsJsonAsync("/api/placement/answers", new
        {
            sectionKey = "self_check",
            answers = new[] { new { questionKey = "confidence_email", responseText = (string?)null, selectedOption = "3" } }
        });

        // vocab_grammar — correct or wrong answers
        await client.PostAsJsonAsync("/api/placement/answers", new
        {
            sectionKey = "vocab_grammar",
            answers = new[]
            {
                Mcq("vg1", allCorrect ? "Could you" : "Send"),
                Mcq("vg2", allCorrect ? "attached" : "attach"),
                Mcq("vg3", allCorrect ? "attended" : "attend"),
                Mcq("vg4", allCorrect ? "make" : "do"),
                Mcq("vg5", allCorrect ? "My apologies" : "Oops"),
                Mcq("vg6", allCorrect ? "He doesn't have time today." : "He don't have time today."),
            }
        });

        // reading
        await client.PostAsJsonAsync("/api/placement/answers", new
        {
            sectionKey = "reading",
            answers = new[]
            {
                Mcq("rd1", allCorrect ? "The team lead, Sara" : "A client"),
                Mcq("rd2", allCorrect ? "Send their updated task list" : "Call the client"),
                Mcq("rd3", allCorrect ? "Tell Sara as soon as possible" : "Wait until Friday"),
            }
        });

        // listening
        await client.PostAsJsonAsync("/api/placement/answers", new
        {
            sectionKey = "listening",
            answers = new[]
            {
                Mcq("ls1", allCorrect ? "To move the meeting to a later time" : "Coffee"),
                Mcq("ls2", allCorrect ? "2 pm" : "9 am"),
                Mcq("ls3", allCorrect ? "The updated numbers" : "Nothing"),
            }
        });

        // writing
        await client.PostAsJsonAsync("/api/placement/answers", new
        {
            sectionKey = "writing",
            answers = new[]
            {
                new { questionKey = "wr1", responseText = allCorrect
                    ? "Hi, thank you for reminding me. The report is almost ready, but I need one more day to finish the final checks. I will send it to you by tomorrow at noon. Please let me know if the client needs anything sooner. Best regards."
                    : "no", selectedOption = (string?)null }
            }
        });

        // speaking
        await client.PostAsJsonAsync("/api/placement/answers", new
        {
            sectionKey = "speaking",
            answers = new[]
            {
                new { questionKey = "sp1", responseText = allCorrect
                    ? "In my job I usually prepare documents and check them for the team. I work closely with my manager and colleagues every day. The most difficult part is explaining delays politely when something is late."
                    : "ok", selectedOption = (string?)null }
            }
        });
    }

    private static object Mcq(string key, string option)
        => new { questionKey = key, responseText = (string?)null, selectedOption = option };

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

/// <summary>
/// Test factory that wires the deterministic placement evaluator (no live AI) and a fake
/// AI provider for any other AI dependency. Reuses ActivityTestFactory's onboarded-student helper.
/// </summary>
public sealed class PlacementTestFactory : ActivityTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var existing = services.Where(d => d.ServiceType == typeof(IPlacementEvaluator)).ToList();
            foreach (var d in existing) services.Remove(d);
            services.AddScoped<IPlacementEvaluator, FakePlacementEvaluator>();
        });
    }
}
