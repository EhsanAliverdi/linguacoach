using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class CefrAssessmentEndpointTests : IClassFixture<CefrAssessmentTestFactory>
{
    private readonly CefrAssessmentTestFactory _factory;

    public CefrAssessmentEndpointTests(CefrAssessmentTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AssessCefr_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assessment/cefr",
            new { studentSample = "Dear John," });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AssessCefr_WithSample_Returns200WithLevel()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"cefr_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var response = await client.PostAsJsonAsync("/api/assessment/cefr",
            new { studentSample = "Dear manager, I am writing to follow up on the approval of the submittal." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var level = body.GetProperty("level").GetString();
        Assert.True(new[] { "A1", "A2", "B1", "B2", "C1", "C2" }.Contains(level));
    }

    [Fact]
    public async Task AssessCefr_PersistsCefrLevelOnStudentProfile()
    {
        var email = $"cefrsave_{Guid.NewGuid():N}@test.com";
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(email);
        var client = ClientWithToken(token);

        await client.PostAsJsonAsync("/api/assessment/cefr",
            new { studentSample = "I am writing to enquire about the status of the document." });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);
        Assert.NotNull(profile.CefrLevel);
        Assert.True(new[] { "A1", "A2", "B1", "B2", "C1", "C2" }.Contains(profile.CefrLevel));
    }

    [Fact]
    public async Task AssessCefr_WritesAiUsageLog()
    {
        var email = $"cefrlog_{Guid.NewGuid():N}@test.com";
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(email);
        var client = ClientWithToken(token);

        await client.PostAsJsonAsync("/api/assessment/cefr",
            new { studentSample = "Please find attached the requested documents for your review." });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = await db.StudentProfiles.FirstAsync(p => p.UserId == userId);
        var log = db.AiUsageLogs.FirstOrDefault(l => l.StudentProfileId == profile.Id);
        Assert.NotNull(log);
        Assert.True(log.InputTokens > 0);
    }

    [Fact]
    public async Task AssessCefr_EmptySample_Returns400()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"cefrempty_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var response = await client.PostAsJsonAsync("/api/assessment/cefr",
            new { studentSample = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public sealed class CefrAssessmentTestFactory : ApiTestFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAiProvider));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddScoped<IAiProvider, FakeCefrAiProvider>();
        });
    }

    public async Task<(string Token, Guid UserId)> CreateOnboardedStudentAsync(string email)
    {
        await SeedPromptsAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoach.Persistence.LinguaCoachDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<LinguaCoach.Persistence.Identity.ApplicationUser>>();
        var tokenSvc = scope.ServiceProvider.GetRequiredService<LinguaCoach.Application.Auth.ITokenService>();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
            return (tokenSvc.GenerateToken(existing.Id, existing.Email!, existing.Role), existing.Id);

        var user = new LinguaCoach.Persistence.Identity.ApplicationUser
        {
            UserName = email, Email = email,
            Role = LinguaCoach.Domain.Enums.UserRole.Student,
            EmailConfirmed = true, MustChangePassword = false
        };
        await userManager.CreateAsync(user, "Student@1234");

        var pair = db.LanguagePairs
            .Include(lp => lp.SourceLanguage)
            .Include(lp => lp.TargetLanguage)
            .First();
        var career = db.CareerProfiles.First();

        var profile = new LinguaCoach.Domain.Entities.StudentProfile(user.Id);
        profile.SetLanguagePair(pair);
        profile.SetSessionPreference(30);
        profile.SetCareerProfile(career);
        profile.SetSkillFocus(LinguaCoach.Domain.Enums.SkillFocus.Writing);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        return (tokenSvc.GenerateToken(user.Id, user.Email!, user.Role), user.Id);
    }

    private async Task SeedPromptsAsync()
    {
        await EnsureCreatedAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoach.Persistence.LinguaCoachDbContext>();

        if (!db.AiPrompts.Any(p => p.Key == "cefr.assessment.v1"))
        {
            db.AiPrompts.Add(new LinguaCoach.Domain.Entities.AiPrompt(
                "cefr.assessment.v1",
                "Assess: {{studentSample}}. Return JSON: {\"level\":\"B1\",\"rationale\":\"\",\"strengths\":[],\"areasForImprovement\":[]}",
                maxInputTokens: 600, maxOutputTokens: 300));
            await db.SaveChangesAsync();
        }

        if (!db.CurriculumWordLists.Any())
        {
            var pair = db.LanguagePairs.First();
            var career = db.CareerProfiles.First();
            db.CurriculumWordLists.Add(new LinguaCoach.Domain.Entities.CurriculumWordList(
                career.Id, pair.Id, "approval", "Official permission", string.Empty, 1));
            await db.SaveChangesAsync();
        }
    }
}

internal sealed class FakeCefrAiProvider : IAiProvider
{
    public string ProviderName => "fake-cefr-provider";

    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        const string json = """
            {
              "level": "B1",
              "rationale": "نوشته شما سطح خوبی دارد و می‌توانید بهتر شوید.",
              "strengths": ["Clear intent", "Appropriate tone"],
              "areasForImprovement": ["Article usage"]
            }
            """;
        return Task.FromResult(new AiResponse(json, InputTokens: 300, OutputTokens: 100, CostUsd: 0.002m, ModelName: "fake-model"));
    }
}
