using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Infrastructure.Storage;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase 4.6 — authenticated audio access for a published Listening Resource Bank item. Mirrors
/// ResourceCandidateAudioServiceTests' convention (FakeFileStorageService, SQLite in-memory).
/// Ownership/ scope is enforced by construction (the storage key always comes from the row's own
/// ContentJson, keyed only by the row's own Id) — these tests prove the resulting behavior: no
/// media for a non-Listening or non-existent row, and correct bytes/content-type for a real one.
/// </summary>
public sealed class ResourceBankMediaServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly FakeFileStorageService _storage = new();
    private readonly ResourceBankMediaService _sut;
    private readonly Guid _sourceId;

    public ResourceBankMediaServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var source = new CefrResourceSource("Media Test Source", "Internal/Original", allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        _db.CefrResourceSources.Add(source);
        _sourceId = source.Id;
        _db.SaveChanges();

        _sut = new ResourceBankMediaService(_db, _storage);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private async Task<ResourceBankItem> SeedListeningWithRealAudioAsync(string key, byte[] bytes, string contentType)
    {
        await _storage.SaveAsync(key, new MemoryStream(bytes), contentType);
        var entry = new ResourceBankItem(
            PublishedResourceType.Listening, _sourceId, "B1",
            ResourceBankItemContent.Serialize(new ListeningPassageContent("News", "A transcript.", key, contentType, null)),
            null, null, null, null);
        _db.ResourceBankItems.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    private async Task<ResourceBankItem> SeedVocabularyAsync()
    {
        var entry = new ResourceBankItem(
            PublishedResourceType.Vocabulary, _sourceId, "A1",
            ResourceBankItemContent.Serialize(new VocabularyContent("hello", null, null)),
            null, null, null, null);
        _db.ResourceBankItems.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    [Fact]
    public async Task GetAudioUrl_returns_null_for_a_non_existent_resource()
    {
        var result = await _sut.GetAudioUrlAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAudioUrl_returns_null_for_a_non_listening_resource()
    {
        var entry = await SeedVocabularyAsync();
        var result = await _sut.GetAudioUrlAsync(entry.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAudioUrl_returns_the_local_streaming_marker_for_a_real_listening_resource()
    {
        var entry = await SeedListeningWithRealAudioAsync("resource-bank-audio/news.mp3", new byte[] { 1, 2, 3 }, "audio/mpeg");

        var result = await _sut.GetAudioUrlAsync(entry.Id);

        result.Should().NotBeNull();
        result!.Url.Should().Be($"/api/admin/resource-bank/{entry.Id}/audio");
    }

    [Fact]
    public async Task GetAudioStream_returns_the_correct_bytes_and_content_type()
    {
        var bytes = new byte[] { 9, 8, 7, 6 };
        var entry = await SeedListeningWithRealAudioAsync("resource-bank-audio/news.mp3", bytes, "audio/mpeg");

        var result = await _sut.GetAudioStreamAsync(entry.Id);

        result.Should().NotBeNull();
        result!.Bytes.Should().Equal(bytes);
        result.ContentType.Should().Be("audio/mpeg");
    }

    [Fact]
    public async Task GetAudioStream_returns_null_for_a_non_listening_resource()
    {
        var entry = await SeedVocabularyAsync();
        var result = await _sut.GetAudioStreamAsync(entry.Id);
        result.Should().BeNull();
    }

    /// <summary>Ownership/scope: a media request for one resource's id can never resolve another
    /// resource's storage key — the lookup is always by (this row's own Id, Type==Listening), never
    /// by a client-supplied key.</summary>
    [Fact]
    public async Task GetAudioStream_for_one_listening_resource_never_returns_another_resources_audio()
    {
        var first = await SeedListeningWithRealAudioAsync("resource-bank-audio/first.mp3", new byte[] { 1 }, "audio/mpeg");
        var second = await SeedListeningWithRealAudioAsync("resource-bank-audio/second.mp3", new byte[] { 2 }, "audio/mpeg");

        var firstResult = await _sut.GetAudioStreamAsync(first.Id);
        var secondResult = await _sut.GetAudioStreamAsync(second.Id);

        firstResult!.Bytes.Should().Equal(new byte[] { 1 });
        secondResult!.Bytes.Should().Equal(new byte[] { 2 });
    }
}
