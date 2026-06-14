using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class PracticeGymExerciseTypeSelectionEndpointTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public PracticeGymExerciseTypeSelectionEndpointTests(ActivityTestFactory factory) => _factory = factory;

    [Theory]
    [InlineData("listening")]
    [InlineData("writing")]
    public async Task Select_ReturnsReadyPracticeGymExerciseType_ForSkill(string skill)
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"gym_select_{skill}_{Guid.NewGuid():N}@test.com");
        var response = await ClientWithToken(token).GetAsync($"/api/activity/exercise-types/select?skill={skill}&context=practiceGym");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("hasSelection").GetBoolean());
        var selected = body.GetProperty("selectedExerciseType");
        Assert.Equal(skill, selected.GetProperty("primarySkill").GetString());
        Assert.True(selected.GetProperty("isAvailableForGeneration").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(selected.GetProperty("key").GetString()));
    }

    [Fact]
    public async Task Select_WhenAllMatchingRowsDisabled_ReturnsSafeNoResult()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"gym_select_disabled_{Guid.NewGuid():N}@test.com");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
            var listening = await db.ExerciseTypeDefinitions.Where(e => e.PrimarySkill == "listening").ToListAsync();
            foreach (var type in listening) type.SetEnabled(false);
            await db.SaveChangesAsync();
        }

        var response = await ClientWithToken(token).GetAsync("/api/activity/exercise-types/select?skill=listening&context=practiceGym");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("hasSelection").GetBoolean());
        Assert.Contains("No ready Practice Gym exercise", body.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Select_DoesNotSelectPlannedPteRows()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"gym_select_pte_{Guid.NewGuid():N}@test.com");

        var response = await ClientWithToken(token).GetAsync("/api/activity/exercise-types/select?skill=listening&context=practiceGym");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var selectedKey = body.GetProperty("selectedExerciseType").GetProperty("key").GetString();
        Assert.NotEqual("summarize_spoken_text", selectedKey);
        Assert.NotEqual("write_from_dictation", selectedKey);
    }

    [Fact]
    public async Task Select_Unauthenticated_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/activity/exercise-types/select?skill=listening&context=practiceGym");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
