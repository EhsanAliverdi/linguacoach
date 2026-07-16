using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Lessons;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.UnitTests.Lessons;

/// <summary>
/// Phase 4.6 — media discovery on LessonResourceLookup.FindAsync's snapshot. Proves the lookup
/// surfaces a published Listening/Speaking resource's media fields (AudioStorageKey/
/// AudioContentType/AudioDurationSeconds/MediaType/ImageUrl) for a future consumer, while
/// confirming (per the Phase 4.5 audit finding) that Lesson/Exercise generation itself is
/// deliberately left untouched — this test only proves the data is available on the snapshot, not
/// that anything downstream reads it yet.
/// </summary>
public sealed class LessonResourceLookupTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly Guid _sourceId;

    public LessonResourceLookupTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var source = new CefrResourceSource("Test Source", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        _db.CefrResourceSources.Add(source);
        _sourceId = source.Id;
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task FindAsync_surfaces_audio_storage_key_content_type_and_duration_for_a_published_listening_resource()
    {
        var entry = new ResourceBankItem(
            PublishedResourceType.Listening, _sourceId, "B1",
            ResourceBankItemContent.Serialize(new ListeningPassageContent(
                "Morning News", "A transcript.", "resource-bank-audio/news.mp3", "audio/mpeg", null, 87.5m)));
        _db.ResourceBankItems.Add(entry);
        await _db.SaveChangesAsync();

        var snapshot = await LessonResourceLookup.FindAsync(_db, PublishedResourceType.Listening, entry.Id, CancellationToken.None);

        snapshot.Should().NotBeNull();
        snapshot!.MediaType.Should().Be("Audio");
        snapshot.AudioStorageKey.Should().Be("resource-bank-audio/news.mp3");
        snapshot.AudioContentType.Should().Be("audio/mpeg");
        snapshot.AudioDurationSeconds.Should().Be(87.5m);
    }

    [Fact]
    public async Task FindAsync_surfaces_null_media_fields_when_the_listening_resources_duration_is_unknown()
    {
        var entry = new ResourceBankItem(
            PublishedResourceType.Listening, _sourceId, "B1",
            ResourceBankItemContent.Serialize(new ListeningPassageContent(
                "Morning News", "A transcript.", "resource-bank-audio/news.mp3", "audio/mpeg", null)));
        _db.ResourceBankItems.Add(entry);
        await _db.SaveChangesAsync();

        var snapshot = await LessonResourceLookup.FindAsync(_db, PublishedResourceType.Listening, entry.Id, CancellationToken.None);

        snapshot!.AudioDurationSeconds.Should().BeNull();
        snapshot.AudioStorageKey.Should().Be("resource-bank-audio/news.mp3");
    }

    [Fact]
    public async Task FindAsync_surfaces_image_url_for_a_published_speaking_resource()
    {
        var entry = new ResourceBankItem(
            PublishedResourceType.Speaking, _sourceId, "B1",
            ResourceBankItemContent.Serialize(new SpeakingPromptContent("Deadline talk", "Negotiate a deadline.", null, "https://example.test/image.jpg")));
        _db.ResourceBankItems.Add(entry);
        await _db.SaveChangesAsync();

        var snapshot = await LessonResourceLookup.FindAsync(_db, PublishedResourceType.Speaking, entry.Id, CancellationToken.None);

        snapshot!.MediaType.Should().Be("Image");
        snapshot.ImageUrl.Should().Be("https://example.test/image.jpg");
    }

    [Fact]
    public async Task FindAsync_leaves_media_fields_null_for_resource_types_with_no_media()
    {
        var entry = new ResourceBankItem(
            PublishedResourceType.Vocabulary, _sourceId, "A1",
            ResourceBankItemContent.Serialize(new VocabularyContent("hello", null, null)));
        _db.ResourceBankItems.Add(entry);
        await _db.SaveChangesAsync();

        var snapshot = await LessonResourceLookup.FindAsync(_db, PublishedResourceType.Vocabulary, entry.Id, CancellationToken.None);

        snapshot!.MediaType.Should().BeNull();
        snapshot.AudioStorageKey.Should().BeNull();
        snapshot.ImageUrl.Should().BeNull();
    }
}
