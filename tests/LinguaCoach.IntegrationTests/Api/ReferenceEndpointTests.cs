using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

public sealed class ReferenceEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public ReferenceEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetLanguagePairs_ReturnsSeededPair()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"ref_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var response = await client.GetAsync("/api/reference/language-pairs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pairs = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(pairs);
        Assert.True(pairs.Length >= 1);
        Assert.Equal("fa", pairs[0].GetProperty("sourceCode").GetString());
        Assert.Equal("en", pairs[0].GetProperty("targetCode").GetString());
    }

    [Fact]
    public async Task GetTracks_ByFaEnPairId_ReturnsWorkplaceEnglish()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"track_{Guid.NewGuid():N}@test.com");
        var langPairId = GetFaEnPairId();
        var client = ClientWithToken(token);

        var response = await client.GetAsync($"/api/reference/tracks?languagePairId={langPairId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tracks = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(tracks);
        Assert.Contains(tracks, t => t.GetProperty("name").GetString() == "Workplace English");
    }

    [Fact]
    public async Task GetCareerProfiles_ByFaEnPairId_ReturnsDocumentController()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"career_{Guid.NewGuid():N}@test.com");
        var langPairId = GetFaEnPairId();
        var client = ClientWithToken(token);

        var response = await client.GetAsync($"/api/reference/career-profiles?languagePairId={langPairId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profiles = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(profiles);
        Assert.Contains(profiles, p => p.GetProperty("name").GetString() == "Document Controller");
    }

    [Fact]
    public async Task GetTracks_UnknownLanguagePairId_ReturnsEmptyArray()
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"empty_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var response = await client.GetAsync($"/api/reference/tracks?languagePairId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tracks = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(tracks);
        Assert.Empty(tracks);
    }

    [Fact]
    public async Task GetLanguagePairs_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/reference/language-pairs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Guid GetFaEnPairId()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        return db.LanguagePairs.First().Id;
    }
}
