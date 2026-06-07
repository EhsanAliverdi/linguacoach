using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LinguaCoach.IntegrationTests.Api;

/// <summary>
/// T-Sprint9: Vocabulary extraction and review.
/// Tests: auth guard, student isolation, status filters, status update, invalid status rejection.
/// </summary>
public sealed class VocabularyEndpointTests : IClassFixture<ActivityTestFactory>
{
    private readonly ActivityTestFactory _factory;

    public VocabularyEndpointTests(ActivityTestFactory factory) => _factory = factory;

    // ── Auth guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetVocabulary_WithNoToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/vocabulary");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task PatchVocabularyStatus_WithNoToken_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PatchAsJsonAsync($"/api/vocabulary/{Guid.NewGuid()}/status", new { status = "Mastered" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Empty state ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetVocabulary_WithNoEntries_ReturnsEmptyArray()
    {
        var (token, _) = await _factory.CreateOnboardedStudentAsync($"vocab_empty_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        var resp = await client.GetAsync("/api/vocabulary");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(0, body.GetArrayLength());
    }

    // ── Returns own entries ───────────────────────────────────────────────────

    [Fact]
    public async Task GetVocabulary_ReturnsStudentOwnEntries()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vocab_own_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);

        AddVocabItem(db, profile.Id, "could you please", "polite_request");
        AddVocabItem(db, profile.Id, "at your earliest convenience", "workplace_phrase");
        await db.SaveChangesAsync();

        var resp = await client.GetAsync("/api/vocabulary");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var items = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, items.GetArrayLength());

        // Verify expected fields
        var first = items[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("term", out _));
        Assert.True(first.TryGetProperty("status", out var status));
        Assert.Equal("New", status.GetString());
        Assert.True(first.TryGetProperty("category", out _));
    }

    // ── Student isolation ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetVocabulary_DoesNotReturnAnotherStudentsEntries()
    {
        var (tokenA, userIdA) = await _factory.CreateOnboardedStudentAsync($"vocab_iso_a_{Guid.NewGuid():N}@test.com");
        var (tokenB, _) = await _factory.CreateOnboardedStudentAsync($"vocab_iso_b_{Guid.NewGuid():N}@test.com");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profileA = db.StudentProfiles.First(p => p.UserId == userIdA);

        AddVocabItem(db, profileA.Id, "please find attached", "workplace_phrase");
        await db.SaveChangesAsync();

        // Student B should see no entries
        var clientB = ClientWithToken(tokenB);
        var respB = await clientB.GetAsync("/api/vocabulary");
        Assert.Equal(HttpStatusCode.OK, respB.StatusCode);

        var items = await respB.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, items.GetArrayLength());
    }

    // ── Status filter ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetVocabulary_StatusFilter_ReturnsOnlyMatchingEntries()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vocab_filter_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);

        var newItem = AddVocabItem(db, profile.Id, "follow up", "workplace_phrase");
        var practisingItem = AddVocabItem(db, profile.Id, "as per our discussion", "connector");
        practisingItem.UpdateStatus(VocabularyItemStatus.Practising);
        await db.SaveChangesAsync();

        var resp = await client.GetAsync("/api/vocabulary?status=Practising");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var items = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("as per our discussion", items[0].GetProperty("term").GetString());
    }

    // ── PATCH status update ───────────────────────────────────────────────────

    [Fact]
    public async Task PatchStatus_UpdatesOwnEntry()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vocab_patch_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);

        var item = AddVocabItem(db, profile.Id, "kind regards", "useful_expression");
        await db.SaveChangesAsync();

        var patchResp = await client.PatchAsJsonAsync(
            $"/api/vocabulary/{item.Id}/status",
            new { status = "Mastered" });

        Assert.Equal(HttpStatusCode.NoContent, patchResp.StatusCode);

        // Verify in DB
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var updated = db2.StudentVocabularyItems.First(v => v.Id == item.Id);
        Assert.Equal(VocabularyItemStatus.Mastered, updated.Status);
    }

    [Fact]
    public async Task PatchStatus_InvalidStatus_Returns400()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vocab_bad_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);

        var item = AddVocabItem(db, profile.Id, "please be advised", "workplace_phrase");
        await db.SaveChangesAsync();

        var resp = await client.PatchAsJsonAsync(
            $"/api/vocabulary/{item.Id}/status",
            new { status = "NotAStatus" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_AnotherStudentsEntry_Returns403()
    {
        var (tokenA, userIdA) = await _factory.CreateOnboardedStudentAsync($"vocab_403a_{Guid.NewGuid():N}@test.com");
        var (tokenB, _) = await _factory.CreateOnboardedStudentAsync($"vocab_403b_{Guid.NewGuid():N}@test.com");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profileA = db.StudentProfiles.First(p => p.UserId == userIdA);

        var itemA = AddVocabItem(db, profileA.Id, "please confirm receipt", "workplace_phrase");
        await db.SaveChangesAsync();

        // Student B tries to update A's item
        var clientB = ClientWithToken(tokenB);
        var resp = await clientB.PatchAsJsonAsync(
            $"/api/vocabulary/{itemA.Id}/status",
            new { status = "Mastered" });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── No raw JSON in response ───────────────────────────────────────────────

    [Fact]
    public async Task GetVocabulary_ResponseHasNoRawJsonFields()
    {
        var (token, userId) = await _factory.CreateOnboardedStudentAsync($"vocab_nojson_{Guid.NewGuid():N}@test.com");
        var client = ClientWithToken(token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LinguaCoachDbContext>();
        var profile = db.StudentProfiles.First(p => p.UserId == userId);

        AddVocabItem(db, profile.Id, "could you please", "polite_request");
        await db.SaveChangesAsync();

        var resp = await client.GetAsync("/api/vocabulary");
        var raw = await resp.Content.ReadAsStringAsync();

        // Response should not contain raw JSON-style property names in string values
        Assert.DoesNotContain("StudentProfileId", raw);
        Assert.DoesNotContain("SourceActivityAttemptId", raw);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static StudentVocabularyItem AddVocabItem(LinguaCoachDbContext db, Guid profileId, string term, string category)
    {
        var item = new StudentVocabularyItem(
            studentProfileId: profileId,
            term: term,
            suggestedPhrase: $"You can use '{term}' in a workplace email.",
            meaningOrExplanation: $"A useful {category.Replace('_', ' ')} expression.",
            exampleSentence: $"Could you {term} by end of day?",
            category: category,
            source: VocabularyItemSource.AiExtractedFromWritingAttempt);

        db.StudentVocabularyItems.Add(item);
        return item;
    }

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
