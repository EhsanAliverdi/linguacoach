using System.Text;
using FluentAssertions;
using LinguaCoach.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Storage;

public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _basePath;
    private readonly LocalFileStorageService _sut;

    public LocalFileStorageServiceTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "speakpath-tests", Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:LocalBasePath"] = _basePath })
            .Build();
        _sut = new LocalFileStorageService(config, NullLogger<LocalFileStorageService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath)) Directory.Delete(_basePath, recursive: true);
    }

    [Fact]
    public async Task SaveThenRead_RoundTrips()
    {
        var key = _sut.GenerateKey("student-1", "tts-audio", ".wav");
        var bytes = Encoding.UTF8.GetBytes("hello audio");

        await using (var ms = new MemoryStream(bytes))
            await _sut.SaveAsync(key, ms, "audio/wav");

        (await _sut.ExistsAsync(key)).Should().BeTrue();
        await using var read = await _sut.ReadAsync(key);
        using var outMs = new MemoryStream();
        await read.CopyToAsync(outMs);
        outMs.ToArray().Should().Equal(bytes);
    }

    [Fact]
    public async Task Delete_RemovesKey()
    {
        var key = _sut.GenerateKey("s", "tts-audio", ".wav");
        await using (var ms = new MemoryStream(new byte[] { 1, 2, 3 }))
            await _sut.SaveAsync(key, ms, "audio/wav");

        await _sut.DeleteAsync(key);

        (await _sut.ExistsAsync(key)).Should().BeFalse();
    }

    [Fact]
    public async Task Move_RelocatesContent()
    {
        var from = _sut.GenerateKey("s", "speaking-recordings/tmp", ".webm");
        var to = "speaking-recordings/final.webm";
        await using (var ms = new MemoryStream(new byte[] { 9, 8, 7 }))
            await _sut.SaveAsync(from, ms, "audio/webm");

        await _sut.MoveAsync(from, to);

        (await _sut.ExistsAsync(from)).Should().BeFalse();
        (await _sut.ExistsAsync(to)).Should().BeTrue();
    }

    [Fact]
    public async Task Read_MissingKey_Throws()
    {
        var act = async () => await _sut.ReadAsync("does/not/exist.wav");
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task Move_MissingSource_Throws()
    {
        var act = async () => await _sut.MoveAsync("missing.wav", "dest.wav");
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task GenerateSignedUrl_ReturnsLocalScheme_WithExpiry()
    {
        var result = await _sut.GenerateSignedUrlAsync("tts-audio/x.wav", TimeSpan.FromMinutes(5));
        result.Url.Should().StartWith("local://");
        result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }
}
