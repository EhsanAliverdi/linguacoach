using System.Text;
using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Infrastructure.Storage;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase J5c — real audio-file upload/storage for ListeningPassage resource candidates. Uses
/// FakeFileStorageService (in-memory, no MinIO dependency) matching this codebase's existing
/// convention for testing IFileStorageService consumers.
/// </summary>
public sealed class ResourceCandidateAudioServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly FakeFileStorageService _storage = new();
    private readonly ResourceCandidateAudioService _sut;
    private readonly ActivityContentFingerprintService _fingerprint = new();

    public ResourceCandidateAudioServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ResourceCandidateAudioService(_db, _storage, NullLogger<ResourceCandidateAudioService>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private ResourceCandidate SeedCandidate(ResourceCandidateType type = ResourceCandidateType.ListeningPassage)
    {
        var source = new CefrResourceSource($"Test Source {Guid.NewGuid():N}", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport();
        _db.CefrResourceSources.Add(source);
        _db.SaveChanges();

        var run = new ResourceImportRun(source.Id, ResourceImportMode.Csv, "test.csv", $"filehash-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        _db.ResourceImportRuns.Add(run);
        _db.SaveChanges();

        var raw = new ResourceRawRecord(run.Id, $"rawhash-{Guid.NewGuid():N}", "en", "row", rawJson: """{"title":"Morning News"}""");
        raw.MarkParsed();
        _db.ResourceRawRecords.Add(raw);
        _db.SaveChanges();

        var fingerprint = _fingerprint.ComputeFingerprint(new ActivityContentFingerprintRequest(
            """{"title":"Morning News"}""", ActivityContentShape.Unknown, null, "Morning News"));
        var candidate = new ResourceCandidate(
            raw.Id, type, "Morning News", """{"title":"Morning News"}""", "en",
            "morning news", fingerprint, ResourceCandidateValidationStatus.NeedsReview);
        _db.ResourceCandidates.Add(candidate);
        _db.SaveChanges();
        return candidate;
    }

    private static Stream ToStream(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Upload_attaches_audio_and_stores_bytes()
    {
        var candidate = SeedCandidate();

        var result = await _sut.UploadAsync(candidate.Id, ToStream("fake mp3 bytes"), "audio/mpeg");

        result.CandidateId.Should().Be(candidate.Id);
        result.AudioContentType.Should().Be("audio/mpeg");

        var reloaded = await _db.ResourceCandidates.AsNoTracking().FirstAsync(c => c.Id == candidate.Id);
        reloaded.AudioStorageKey.Should().NotBeNullOrWhiteSpace();
        reloaded.AudioContentType.Should().Be("audio/mpeg");
        _storage.Keys.Should().ContainSingle();
    }

    [Fact]
    public async Task Upload_rejects_a_non_listening_candidate()
    {
        var candidate = SeedCandidate(ResourceCandidateType.VocabularyEntry);

        var act = async () => await _sut.UploadAsync(candidate.Id, ToStream("bytes"), "audio/mpeg");

        await act.Should().ThrowAsync<ResourceImportValidationException>()
            .WithMessage("*ListeningPassage*");
    }

    [Fact]
    public async Task Upload_rejects_a_nonexistent_candidate()
    {
        var act = async () => await _sut.UploadAsync(Guid.NewGuid(), ToStream("bytes"), "audio/mpeg");
        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }

    [Fact]
    public async Task Upload_rejects_an_already_published_candidate()
    {
        var candidate = SeedCandidate();
        candidate.ApplyAnalysis(
            """{"cefrLevel":"A1"}""", "A1", 0.95, "listening", null, 1,
            "[]", "[]", null, null, null, null, null, 0.9, "morning news");
        candidate.ApplyValidation(ResourceCandidateValidationStatus.Passed, """{"errors":[],"warnings":[]}""");
        candidate.Approve();
        candidate.MarkPublished("CefrListeningPassage", Guid.NewGuid(), DateTimeOffset.UtcNow, null);
        _db.SaveChanges();

        var act = async () => await _sut.UploadAsync(candidate.Id, ToStream("bytes"), "audio/mpeg");

        await act.Should().ThrowAsync<ResourceImportValidationException>()
            .WithMessage("*already been published*");
    }

    [Fact]
    public async Task GetAudioUrl_returns_null_when_no_audio_attached()
    {
        var candidate = SeedCandidate();
        var result = await _sut.GetAudioUrlAsync(candidate.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAudioUrl_falls_back_to_the_streaming_endpoint_for_fake_storage()
    {
        var candidate = SeedCandidate();
        await _sut.UploadAsync(candidate.Id, ToStream("bytes"), "audio/mpeg");

        var result = await _sut.GetAudioUrlAsync(candidate.Id);

        result.Should().NotBeNull();
        result!.Url.Should().Be($"/api/admin/resource-candidates/{candidate.Id}/audio");
    }

    [Fact]
    public async Task GetAudioStream_returns_the_uploaded_bytes_and_content_type()
    {
        var candidate = SeedCandidate();
        await _sut.UploadAsync(candidate.Id, ToStream("fake mp3 bytes"), "audio/mpeg");

        var result = await _sut.GetAudioStreamAsync(candidate.Id);

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("audio/mpeg");
        Encoding.UTF8.GetString(result.Bytes).Should().Be("fake mp3 bytes");
    }

    [Fact]
    public async Task GetAudioStream_returns_null_when_no_audio_attached()
    {
        var candidate = SeedCandidate();
        var result = await _sut.GetAudioStreamAsync(candidate.Id);
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("audio/mpeg", true)]
    [InlineData("audio/wav", true)]
    [InlineData("audio/webm", true)]
    [InlineData("video/mp4", false)]
    [InlineData("application/pdf", false)]
    public void IsAllowedMimeType_matches_the_speaking_audio_allowlist(string mimeType, bool expected)
    {
        _sut.IsAllowedMimeType(mimeType).Should().Be(expected);
    }
}
