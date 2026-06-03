using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.Ai;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// Integration tests for POST /api/writing/exercise/submit and GET /api/writing/exercise.
/// Uses ApiTestFactory with a fake IAiProvider — no real OpenAI call is made.
/// </summary>
public sealed class WritingExerciseEndpointTests : IClassFixture<WritingExerciseTestFactory>
{
    private readonly WritingExerciseTestFactory _factory;

    public WritingExerciseEndpointTests(WritingExerciseTestFactory factory)
    {
        _factory = factory;
    }

    // ── GET /api/writing/exercise ─────────────────────────────────────────────

    [Fact]
    public async Task GetExercise_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/writing/exercise");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetExercise_AuthenticatedStudent_WithCompletedOnboarding_Returns200()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync();
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/writing/exercise");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("scenarioTitle").GetString()));
        Assert.True(body.GetProperty("targetVocabulary").GetArrayLength() > 0);
    }

    // ── POST /api/writing/exercise/submit ─────────────────────────────────────

    [Fact]
    public async Task Submit_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "Dear John," });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Submit_WithValidDraft_Returns200AndFeedback()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync();
        var client = ClientWithToken(token);

        var response = await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "Dear John, I wanted to follow up on the submittal." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("submissionId").GetString()?.Length > 0);
        Assert.True(body.GetProperty("overallScore").GetDouble() >= 0);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("correctedEmail").GetString()));
    }

    [Fact]
    public async Task Submit_WithValidDraft_WritesAiUsageLog()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"usagelog_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "Dear manager, please approve the document." });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var studentProfile = db.StudentProfiles.First(p => p.UserId == userId);
        var log = db.AiUsageLogs.FirstOrDefault(l => l.StudentProfileId == studentProfile.Id);
        Assert.NotNull(log);
        Assert.Equal("fake-provider", log.ProviderName);
        Assert.True(log.InputTokens > 0);
    }

    [Fact]
    public async Task Submit_WithEmptyDraft_Returns400()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync();
        var client = ClientWithToken(token);

        var response = await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Submit_PersistsWritingSubmission()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"persist_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "I wanted to follow up on the approval." });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var submission = db.WritingSubmissions.FirstOrDefault(s => s.StudentProfileId == profile.Id);
        Assert.NotNull(submission);
        Assert.Contains("follow up", submission.OriginalText);
    }

    [Fact]
    public async Task Submit_UpdatesVocabularyEntries_ForWordsUsedInDraft()
    {
        var email = $"vocab_{Guid.NewGuid():N}@test.com";
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(email);
        var client = ClientWithToken(token);

        // "approval" and "submittal" are used in draft; all presented words get entries (exposure or usage)
        await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "Dear manager, please approve the submittal for approval." });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var entries = db.VocabularyEntries.Where(v => v.StudentProfileId == profile.Id).ToList();

        // All presented words get entries (up to 5 new from curriculum + any review/mastered)
        Assert.NotEmpty(entries);

        var approvalEntry = entries.FirstOrDefault(v => v.Word.ToLower() == "approval");
        Assert.NotNull(approvalEntry);
        Assert.Equal(1, approvalEntry.UsageCount);
        Assert.Equal(1, approvalEntry.CorrectCount);

        var submittedEntry = entries.FirstOrDefault(v => v.Word.ToLower() == "submittal");
        Assert.NotNull(submittedEntry);
        Assert.Equal(1, submittedEntry.UsageCount);
        Assert.Equal(1, submittedEntry.CorrectCount);
    }

    [Fact]
    public async Task Submit_CreatesExposureEntries_ForWordsNotUsedInDraft()
    {
        var email = $"vocabneg_{Guid.NewGuid():N}@test.com";
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(email);
        var client = ClientWithToken(token);

        // Draft contains none of the lesson vocabulary words
        await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "Hello, just writing to check in." });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var entries = db.VocabularyEntries.Where(v => v.StudentProfileId == profile.Id).ToList();

        // All presented curriculum words get exposure entries even if not used in the draft
        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.Equal(0, e.UsageCount));
        Assert.All(entries, e => Assert.True(e.ExposureCount > 0));
    }

    [Fact]
    public async Task Submit_UpsertsUserLearningSummary()
    {
        var email = $"summary_{Guid.NewGuid():N}@test.com";
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(email);
        var client = ClientWithToken(token);

        await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "I wanted to follow up on the pending approval." });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var summary = db.UserLearningSummaries.FirstOrDefault(s => s.StudentProfileId == profile.Id);

        Assert.NotNull(summary);
        Assert.Contains("writing exercise", summary.RecentProgress.ToLower());
        // FakeAiProvider returns overallScore=68 and 1 grammar issue
        Assert.Contains("68", summary.RecentProgress);
        Assert.Contains("Grammar issues: 1", summary.RecentWeaknesses);
    }

    [Fact]
    public async Task Submit_Twice_ProducesSingleLearningSummaryRow()
    {
        var email = $"upsert_{Guid.NewGuid():N}@test.com";
        var (token, userId) = await _factory.CreateOnboardedStudentAsync(email);
        var client = ClientWithToken(token);

        await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "First draft about pending approval." });
        await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "Second draft about approval submittal." });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);
        var summaries = db.UserLearningSummaries.Where(s => s.StudentProfileId == profile.Id).ToList();

        Assert.Single(summaries);
        Assert.False(string.IsNullOrEmpty(summaries[0].RecentProgress));
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public sealed class WritingExerciseRateLimitTests : IClassFixture<WritingExerciseRateLimitTestFactory>
{
    private readonly WritingExerciseRateLimitTestFactory _factory;

    public WritingExerciseRateLimitTests(WritingExerciseRateLimitTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Submit_WhenUserExceedsLimit_Returns429WithHelpfulMessage()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"limited_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var first = await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "Dear manager, please review the pending approval." });
        var second = await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "Dear manager, please review the pending approval again." });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("wait", body.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Submit_RateLimit_IsPartitionedByUser()
    {
        var (firstToken, _) = await _factory.CreateOnboardedStudentAsync($"limited_a_{Guid.NewGuid():N}@test.com");
        var (secondToken, _) = await _factory.CreateOnboardedStudentAsync($"limited_b_{Guid.NewGuid():N}@test.com");

        var firstClient = ClientWithToken(firstToken);
        var secondClient = ClientWithToken(secondToken);

        var firstUserResponse = await firstClient.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "Dear manager, please review the pending approval." });
        var secondUserResponse = await secondClient.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "Dear manager, please review the pending approval." });

        Assert.Equal(HttpStatusCode.OK, firstUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondUserResponse.StatusCode);
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public sealed class WritingExerciseRateLimitTestFactory : WritingExerciseTestFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("WritingAi:RateLimit:PermitLimit", "1");
        builder.UseSetting("WritingAi:RateLimit:WindowMinutes", "10");
        base.ConfigureWebHost(builder);
    }
}

public sealed class WritingExerciseMissingAiConfigTests : IClassFixture<WritingExerciseMissingAiConfigTestFactory>
{
    private readonly WritingExerciseMissingAiConfigTestFactory _factory;

    public WritingExerciseMissingAiConfigTests(WritingExerciseMissingAiConfigTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Submit_WhenConfiguredProviderApiKeyMissing_ReturnsControlledUnavailableResponse()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"missing_ai_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var response = await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "Dear manager, could you please review the latest revision?" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ai_unavailable", body.GetProperty("code").GetString());
        Assert.Equal("AI feedback is not configured or is temporarily unavailable.", body.GetProperty("error").GetString());
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public sealed class WritingExerciseMissingAiConfigTestFactory : WritingExerciseTestFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("AI:WritingFeedback:Provider", "Gemini");
        builder.UseSetting("AI:WritingFeedback:Model", "gemini-2.5-flash");
        builder.UseSetting("Gemini:ApiKey", "");
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(d => d.ServiceType == typeof(IAiProviderResolver)).ToList();
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);
            services.AddScoped<IAiProviderResolver, AiProviderResolver>();
        });
    }
}

public sealed class WritingExerciseMalformedAiResponseTests : IClassFixture<WritingExerciseMalformedAiResponseTestFactory>
{
    private readonly WritingExerciseMalformedAiResponseTestFactory _factory;

    public WritingExerciseMalformedAiResponseTests(WritingExerciseMalformedAiResponseTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Submit_WhenProviderReturnsMalformedJson_ReturnsControlledValidationFailure()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"bad_ai_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var response = await client.PostAsJsonAsync("/api/writing/exercise/submit",
            new { draftText = "Dear manager, could you please review the latest revision?" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ai_validation_failed", body.GetProperty("code").GetString());
        Assert.Equal("AI feedback is not configured or is temporarily unavailable.", body.GetProperty("error").GetString());
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public sealed class WritingExerciseMalformedAiResponseTestFactory : WritingExerciseTestFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            var providerDescriptors = services.Where(d => d.ServiceType == typeof(IAiProvider)).ToList();
            foreach (var descriptor in providerDescriptors)
                services.Remove(descriptor);
            services.AddScoped<IAiProvider, MalformedFakeAiProvider>();

            var resolverDescriptors = services.Where(d => d.ServiceType == typeof(IAiProviderResolver)).ToList();
            foreach (var resolverDescriptor in resolverDescriptors)
                services.Remove(resolverDescriptor);
            services.AddScoped<IAiProviderResolver, FakeAiProviderResolver>();
        });
    }
}

/// <summary>
/// Test factory that replaces the real IAiProvider with a deterministic fake.
/// No OpenAI API key required. Returns a fixed valid JSON response.
/// </summary>
public class WritingExerciseTestFactory : ApiTestFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // Run after base SQLite setup — replaces real OpenAI provider with a fast deterministic fake.
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAiProvider));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddScoped<IAiProvider, FakeAiProvider>();

            var resolverDescriptors = services.Where(d => d.ServiceType == typeof(IAiProviderResolver)).ToList();
            foreach (var resolverDescriptor in resolverDescriptors)
                services.Remove(resolverDescriptor);
            services.AddScoped<IAiProviderResolver, FakeAiProviderResolver>();
        });
    }

    /// <summary>
    /// Seeds the writing exercise prompt template for tests.
    /// EnsureCreated does not run migrations, so seed data from migrations must be added manually.
    /// </summary>
    public async Task SeedPromptTemplateAsync()
    {
        await EnsureCreatedAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();

        if (!db.AiPrompts.Any(p => p.Key == "writing.exercise.v1"))
        {
            db.AiPrompts.Add(new LinguaCoach.Domain.Entities.AiPrompt(
                "writing.exercise.v1",
                "You are an English coach. Draft: {{userDraft}}. Return JSON: {\"overallScore\":0,\"correctedEmail\":\"\",\"feedbackInSourceLanguage\":\"\",\"grammarIssues\":[],\"vocabularyIssues\":[],\"toneIssues\":[],\"suggestedPhrases\":[],\"mistakesToTrack\":[]}",
                maxInputTokens: 800, maxOutputTokens: 600));
            await db.SaveChangesAsync();
        }

        // EnsureCreated skips migrations, so we seed CurriculumWordList manually.
        if (!db.CurriculumWordLists.Any())
        {
            var pair = db.LanguagePairs.First();
            var career = db.CareerProfiles.First();

            var words = new[]
            {
                ("approval", "Official agreement or permission", 1),
                ("submittal", "A formal document submitted for review", 2),
                ("revision", "A corrected or updated document version", 3),
                ("pending", "Awaiting action or decision", 4),
                ("outstanding", "Not yet resolved", 5),
                ("transmittal", "A cover document recording what is sent", 6),
                ("compliance", "Meeting required standards", 7),
                ("RFI", "Request for Information", 8),
                ("specification", "A detailed technical description", 9),
                ("drawing register", "A log tracking all project drawings", 10),
            };

            foreach (var (word, def, priority) in words)
                db.CurriculumWordLists.Add(new LinguaCoach.Domain.Entities.CurriculumWordList(
                    career.Id, pair.Id, word, def, string.Empty, priority));

            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Creates a student with fully completed onboarding so writing exercise is accessible.
    /// </summary>
    public async Task<(string Token, Guid UserId)> CreateOnboardedStudentAsync(
        string email = "writing_student@test.linguacoach.com")
    {
        await SeedPromptTemplateAsync();
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
            Role = LinguaCoach.Domain.Enums.UserRole.Student,
            EmailConfirmed = true, MustChangePassword = false
        };
        await userManager.CreateAsync(user, "Student@1234");

        var profile = new LinguaCoach.Domain.Entities.StudentProfile(user.Id);
        var pair = db.LanguagePairs
            .Include(lp => lp.SourceLanguage)
            .Include(lp => lp.TargetLanguage)
            .First();
        var track = db.LearningTracks.First();
        var career = db.CareerProfiles.First();
        profile.SetLanguagePair(pair);
        profile.SetLearningTrack(track);
        profile.SetCareerProfile(career);
        profile.SetSkillFocus(LinguaCoach.Domain.Enums.SkillFocus.Writing);
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync();

        return (tokenSvc.GenerateToken(user.Id, user.Email!, user.Role), user.Id);
    }
}

/// <summary>
/// Deterministic fake AI provider. Returns a valid structured JSON response.
/// </summary>
internal sealed class FakeAiProvider : IAiProvider
{
    public string ProviderName => "fake-provider";

    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        const string json = """
            {
              "overallScore": 68,
              "correctedEmail": "Dear John,\n\nI hope this email finds you well. I wanted to follow up on the submittal we sent last week.\n\nBest regards",
              "feedbackInSourceLanguage": "ایمیل شما خوب بود اما می‌توانید رسمی‌تر بنویسید.",
              "grammarIssues": ["Missing comma after 'John'"],
              "vocabularyIssues": [],
              "toneIssues": [],
              "suggestedPhrases": ["I would appreciate your response at your earliest convenience"],
              "mistakesToTrack": ["comma after salutation"]
            }
            """;

        return Task.FromResult(new AiResponse(json, InputTokens: 450, OutputTokens: 180, CostUsd: 0.004m, ModelName: "fake-model"));
    }
}

internal sealed class MalformedFakeAiProvider : IAiProvider
{
    public string ProviderName => "fake-provider";

    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
        => Task.FromResult(new AiResponse("not json", 10, 10, 0, "fake-model", ProviderName));
}

internal sealed class FakeAiProviderResolver : IAiProviderResolver
{
    private readonly IAiProvider _provider;

    public FakeAiProviderResolver(IAiProvider provider)
    {
        _provider = provider;
    }

    public AiProviderSelection ResolveWritingFeedbackProvider()
        => new(_provider, _provider.ProviderName, "fake-model");
}
