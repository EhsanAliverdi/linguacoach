using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Application.LearningPath;
using LinguaCoach.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// T29: Learning path endpoint, path generation fallback, module-aware activity context, dashboard path data.
/// </summary>
public sealed class LearningPathEndpointTests : IClassFixture<LearningPathTestFactory>
{
    private readonly LearningPathTestFactory _factory;

    public LearningPathEndpointTests(LearningPathTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetLearningPath_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/learning-path");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLearningPath_WhenNoPathExists_Returns404()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"no_path_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        // This factory's path generator always fails → no path generated at onboarding.
        // Dashboard fallback doesn't create path via this endpoint route.
        var response = await client.GetAsync("/api/learning-path");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetLearningPath_WhenPathExists_Returns200WithModules()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"path_exists_{Guid.NewGuid():N}@test.com");

        // Seed a path directly so this test is independent of path generation success.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = db.StudentProfiles.First(p => p.UserId == userId);
            var path = new LinguaCoach.Domain.Entities.LearningPath(profile.Id, "Test Path — B1", "Test summary");
            db.LearningPaths.Add(path);
            await db.SaveChangesAsync();
            db.LearningModules.AddRange(
                new LinguaCoach.Domain.Entities.LearningModule(path.Id, "Module One", "Desc one", 1),
                new LinguaCoach.Domain.Entities.LearningModule(path.Id, "Module Two", "Desc two", 2));
            await db.SaveChangesAsync();
        }

        var client = ClientWithToken(token);
        var response = await client.GetAsync("/api/learning-path");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Test Path — B1", body.GetProperty("title").GetString());
        Assert.Equal(2, body.GetProperty("totalModules").GetInt32());
        Assert.Equal(2, body.GetProperty("modules").GetArrayLength());

        var firstModule = body.GetProperty("modules").EnumerateArray().First();
        Assert.Equal("Module One", firstModule.GetProperty("title").GetString());
        Assert.True(firstModule.GetProperty("isCurrent").GetBoolean());
    }

    [Fact]
    public async Task GetDashboard_WhenPathExists_IncludesLearningPathSummary()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"dash_path_{Guid.NewGuid():N}@test.com");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var profile = db.StudentProfiles.First(p => p.UserId == userId);
            var path = new LinguaCoach.Domain.Entities.LearningPath(profile.Id, "Dashboard Path — B1", "Summary");
            db.LearningPaths.Add(path);
            await db.SaveChangesAsync();
            db.LearningModules.Add(
                new LinguaCoach.Domain.Entities.LearningModule(path.Id, "Email writing", "Practice emails", 1));
            await db.SaveChangesAsync();
        }

        var client = ClientWithToken(token);
        var response = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("learningPath").ValueKind);

        var lp = body.GetProperty("learningPath");
        Assert.Equal("Dashboard Path — B1", lp.GetProperty("title").GetString());
        Assert.Equal(1, lp.GetProperty("totalModules").GetInt32());
        Assert.Equal(0, lp.GetProperty("modulesCompleted").GetInt32());

        var cm = lp.GetProperty("currentModule");
        Assert.Equal("Email writing", cm.GetProperty("title").GetString());
        Assert.Equal(1, cm.GetProperty("order").GetInt32());
    }

    [Fact]
    public async Task GetDashboard_WhenNoPath_LearningPathIsNull()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"dash_no_path_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("learningPath").ValueKind);
    }

    // Phase I2A (legacy fallback deletion): GET /api/activity/next (and the lazy path-generation
    // + SystemFallback behaviour it exercised when AI path generation failed) was removed. See
    // docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

/// <summary>
/// Test factory where ILearningPathGenerator always fails so we can test the fallback paths.
/// </summary>
public sealed class LearningPathTestFactory : ActivityTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(d => d.ServiceType == typeof(ILearningPathGenerator)).ToList();
            foreach (var d in descriptors) services.Remove(d);
            services.AddScoped<ILearningPathGenerator, AlwaysFailingPathGenerator>();
        });
    }
}

internal sealed class AlwaysFailingPathGenerator : ILearningPathGenerator
{
    public Task<LearningPathDto> GenerateAsync(GenerateLearningPathCommand command, CancellationToken ct = default)
        => throw new InvalidOperationException("Simulated path generation failure (test).");
}
