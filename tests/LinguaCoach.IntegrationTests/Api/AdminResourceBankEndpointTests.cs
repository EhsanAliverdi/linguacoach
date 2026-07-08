using System.Net;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>Phase E5 — read-only browse/search over the published Cefr* bank tables. All 6
/// endpoints (3 list + 3 detail) are admin-only, matching AdminResourceCandidateController's
/// existing auth convention.</summary>
public sealed class AdminResourceBankEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public AdminResourceBankEndpointTests(ApiTestFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/admin/resource-banks/vocabulary")]
    [InlineData("/api/admin/resource-banks/grammar")]
    [InlineData("/api/admin/resource-banks/reading-references")]
    [InlineData("/api/admin/resource-banks/reading-passages")]
    public async Task List_Unauthenticated_Returns401(string route)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/resource-banks/vocabulary")]
    [InlineData("/api/admin/resource-banks/grammar")]
    [InlineData("/api/admin/resource-banks/reading-references")]
    [InlineData("/api/admin/resource-banks/reading-passages")]
    public async Task List_NonAdmin_Returns403(string route)
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/resource-banks/vocabulary")]
    [InlineData("/api/admin/resource-banks/grammar")]
    [InlineData("/api/admin/resource-banks/reading-references")]
    [InlineData("/api/admin/resource-banks/reading-passages")]
    public async Task Detail_Unauthenticated_Returns401(string route)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"{route}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/resource-banks/vocabulary")]
    [InlineData("/api/admin/resource-banks/grammar")]
    [InlineData("/api/admin/resource-banks/reading-references")]
    [InlineData("/api/admin/resource-banks/reading-passages")]
    public async Task Detail_NonAdmin_Returns403(string route)
    {
        var (token, _) = await _factory.CreateStudentAndGetTokenAsync($"student-{Guid.NewGuid():N}@test.linguacoach.com");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"{route}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/resource-banks/vocabulary")]
    [InlineData("/api/admin/resource-banks/grammar")]
    [InlineData("/api/admin/resource-banks/reading-references")]
    [InlineData("/api/admin/resource-banks/reading-passages")]
    public async Task Detail_NonexistentId_Returns404(string route)
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"{route}/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/resource-banks/vocabulary")]
    [InlineData("/api/admin/resource-banks/grammar")]
    [InlineData("/api/admin/resource-banks/reading-references")]
    [InlineData("/api/admin/resource-banks/reading-passages")]
    public async Task List_Admin_Returns200_With_Empty_Result_When_Nothing_Published(string route)
    {
        var token = await _factory.CreateAdminAndGetTokenAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
