using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class SpeakingSessionEndpointTests : IClassFixture<SpeakingTestFactory>
{
    private readonly SpeakingTestFactory _factory;

    public SpeakingSessionEndpointTests(SpeakingTestFactory factory)
    {
        _factory = factory;
    }

    // ── POST /api/speaking/sessions ───────────────────────────────────────────

    [Fact]
    public async Task CreateSession_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/speaking/sessions",
            new { scenarioId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateSession_ValidScenario_Returns200WithFirstQuestion()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"speak_{Guid.NewGuid():N}@t.com");
        var client = ClientWithToken(token);
        var scenarioId = await _factory.GetScenarioIdAsync();

        var response = await client.PostAsJsonAsync("/api/speaking/sessions",
            new { scenarioId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, body.GetProperty("sessionId").GetGuid());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("firstAiQuestion").GetString()));
        Assert.True(body.GetProperty("maxTurns").GetInt32() > 0);
    }

    [Fact]
    public async Task CreateSession_PersistsSessionInDb()
    {
        var email = $"speakdb_{Guid.NewGuid():N}@t.com";
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(email);
        var client = ClientWithToken(token);
        var scenarioId = await _factory.GetScenarioIdAsync();

        var response = await client.PostAsJsonAsync("/api/speaking/sessions", new { scenarioId });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = body.GetProperty("sessionId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var session = db.SpeakingSessions.FirstOrDefault(s => s.Id == sessionId);

        Assert.NotNull(session);
        Assert.Equal(SpeakingSessionStatus.InProgress, session.Status);
        Assert.Equal(profile.Id, session.StudentProfileId);
    }

    // ── POST /api/speaking/sessions/{id}/turns ────────────────────────────────

    [Fact]
    public async Task SubmitTurn_ValidTranscript_Returns200WithFeedback()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"turn_{Guid.NewGuid():N}@t.com");
        var client = ClientWithToken(token);
        var sessionId = await CreateSessionAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/speaking/sessions/{sessionId}/turns",
            new { userTranscript = "Good morning. I am calling to follow up on a pending document approval." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("turnNumber").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("aiReply").GetString()));
    }

    [Fact]
    public async Task SubmitTurn_EmptyTranscript_Returns400()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"empty_{Guid.NewGuid():N}@t.com");
        var client = ClientWithToken(token);
        var sessionId = await CreateSessionAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/speaking/sessions/{sessionId}/turns",
            new { userTranscript = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitTurn_WritesAiUsageLog()
    {
        var email = $"speaklog_{Guid.NewGuid():N}@t.com";
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(email);
        var client = ClientWithToken(token);
        var sessionId = await CreateSessionAsync(client);

        await client.PostAsJsonAsync(
            $"/api/speaking/sessions/{sessionId}/turns",
            new { userTranscript = "I wanted to follow up on the approval." });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        // At least 2 AI calls: one for first question + one for this turn
        var logs = db.AiUsageLogs.Where(l => l.StudentProfileId == profile.Id).ToList();
        Assert.True(logs.Count >= 2);
    }

    [Fact]
    public async Task FullSessionFlow_CompletesAfterMaxTurns()
    {
        var email = $"fullflow_{Guid.NewGuid():N}@t.com";
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(email);
        var client = ClientWithToken(token);
        var scenarioId = await _factory.GetScenarioIdAsync();

        // Create session
        var createResp = await client.PostAsJsonAsync("/api/speaking/sessions", new { scenarioId });
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = createBody.GetProperty("sessionId").GetGuid();
        var maxTurns = createBody.GetProperty("maxTurns").GetInt32();

        // Submit all turns
        bool sessionComplete = false;
        for (var i = 0; i < maxTurns; i++)
        {
            var turnResp = await client.PostAsJsonAsync(
                $"/api/speaking/sessions/{sessionId}/turns",
                new { userTranscript = $"Turn {i + 1}: I wanted to follow up on the pending approval." });
            Assert.Equal(HttpStatusCode.OK, turnResp.StatusCode);
            var turnBody = await turnResp.Content.ReadFromJsonAsync<JsonElement>();
            sessionComplete = turnBody.GetProperty("sessionComplete").GetBoolean();
        }

        Assert.True(sessionComplete, "Session should be complete after maxTurns turns");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var session = await db.SpeakingSessions.FirstAsync(s => s.Id == sessionId);

        Assert.Equal(SpeakingSessionStatus.Completed, session.Status);
        Assert.NotNull(session.OverallScore);

        var summary = db.UserLearningSummaries.FirstOrDefault(s => s.StudentProfileId == profile.Id);
        Assert.NotNull(summary);
        Assert.False(string.IsNullOrEmpty(summary.RecentProgress));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreateSessionAsync(HttpClient client)
    {
        var scenarioId = await _factory.GetScenarioIdAsync();
        var resp = await client.PostAsJsonAsync("/api/speaking/sessions", new { scenarioId });
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("sessionId").GetGuid();
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public sealed class SpeakingTestFactory : ApiTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAiProvider));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddScoped<IAiProvider, FakeSpeakingAiProvider>();
        });
    }

    public async Task<Guid> GetScenarioIdAsync()
    {
        await SeedAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        return db.SpeakingScenarios.Select(s => s.Id).First();
    }

    public async Task<(string Token, Guid UserId)> CreateOnboardedStudentAsync(string email)
    {
        await SeedAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<LinguaCoach.Persistence.Identity.ApplicationUser>>();
        var tokenSvc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
            return (tokenSvc.GenerateToken(existing.Id, existing.Email!, existing.Role), existing.Id);

        var user = new LinguaCoach.Persistence.Identity.ApplicationUser
        {
            UserName = email, Email = email,
            Role = UserRole.Student, EmailConfirmed = true, MustChangePassword = false
        };
        await userManager.CreateAsync(user, "Student@1234");

        var pair = db.LanguagePairs.Include(lp => lp.SourceLanguage).Include(lp => lp.TargetLanguage).First();
        var track = db.LearningTracks.First();
        var career = db.CareerProfiles.First();

        var profile = new LinguaCoach.Domain.Entities.StudentProfile(user.Id);
        profile.SetLanguagePair(pair);
        profile.SetLearningTrack(track);
        profile.SetCareerProfile(career);
        profile.SetSkillFocus(SkillFocus.Writing);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        return (tokenSvc.GenerateToken(user.Id, user.Email!, user.Role), user.Id);
    }

    private bool _seeded;

    private async Task SeedAsync()
    {
        if (_seeded) return;
        _seeded = true;
        await EnsureCreatedAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        if (!db.AiPrompts.Any(p => p.Key == "speaking.turn.v1"))
        {
            db.AiPrompts.Add(new LinguaCoach.Domain.Entities.AiPrompt(
                "speaking.turn.v1",
                "Speaking: {{userTranscript}}. Return JSON: {\"aiReply\":\"Let's continue.\",\"grammarScore\":70,\"vocabularyScore\":65,\"fluencyScore\":68,\"feedback\":\"خوب بود!\",\"mistakes\":[],\"turnSummary\":\"Student introduced themselves.\"}",
                maxInputTokens: 700, maxOutputTokens: 400));
            await db.SaveChangesAsync();
        }

        if (!db.SpeakingScenarios.Any())
        {
            var pair = db.LanguagePairs.First();
            var career = db.CareerProfiles.First();
            db.SpeakingScenarios.Add(new LinguaCoach.Domain.Entities.SpeakingScenario(
                career.Id, pair.Id,
                "Document approval call",
                "Practice following up on a document approval.",
                2,  // 2 turns for fast tests
                "I wanted to follow up",
                "Professional tone and clarity",
                "B1"));
            await db.SaveChangesAsync();
        }
    }
}

internal sealed class FakeSpeakingAiProvider : IAiProvider
{
    public string ProviderName => "fake-speaking-provider";

    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        const string json = """
            {
              "aiReply": "Thank you. Could you tell me which document you are referring to?",
              "pronunciationScore": null,
              "grammarScore": 72,
              "vocabularyScore": 68,
              "fluencyScore": 70,
              "feedback": "خوب بود! جمله‌بندی شما واضح بود.",
              "mistakes": ["Missing article before 'document'"],
              "turnSummary": "Student introduced the purpose of the call."
            }
            """;
        return Task.FromResult(new AiResponse(json, InputTokens: 350, OutputTokens: 150, CostUsd: 0.003m, ModelName: "fake-model"));
    }
}
