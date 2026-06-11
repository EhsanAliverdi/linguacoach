using System.Text;
using FluentAssertions;
using LinguaCoach.Infrastructure.Storage;

namespace LinguaCoach.UnitTests.Storage;

public sealed class FakeFileStorageServiceTests
{
    private readonly FakeFileStorageService _sut = new();

    [Fact]
    public async Task SaveReadDeleteMove_RoundTrip()
    {
        var bytes = Encoding.UTF8.GetBytes("audio-bytes");
        await using (var ms = new MemoryStream(bytes))
            await _sut.SaveAsync("tts-audio/a.wav", ms, "audio/wav");

        (await _sut.ExistsAsync("tts-audio/a.wav")).Should().BeTrue();

        await using var read = await _sut.ReadAsync("tts-audio/a.wav");
        using var outMs = new MemoryStream();
        await read.CopyToAsync(outMs);
        outMs.ToArray().Should().Equal(bytes);

        await _sut.MoveAsync("tts-audio/a.wav", "tts-audio/b.wav");
        (await _sut.ExistsAsync("tts-audio/a.wav")).Should().BeFalse();
        (await _sut.ExistsAsync("tts-audio/b.wav")).Should().BeTrue();

        await _sut.DeleteAsync("tts-audio/b.wav");
        (await _sut.ExistsAsync("tts-audio/b.wav")).Should().BeFalse();
    }

    [Fact]
    public async Task Keys_And_GetBytes_Helpers_Work()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        await using (var ms = new MemoryStream(bytes))
            await _sut.SaveAsync("k1", ms, "audio/wav");

        _sut.Keys.Should().Contain("k1");
        _sut.GetBytes("k1").Should().Equal(bytes);
        _sut.GetBytes("missing").Should().BeNull();
    }

    [Fact]
    public async Task Read_MissingKey_Throws()
    {
        var act = async () => await _sut.ReadAsync("nope");
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task Move_MissingSource_Throws()
    {
        var act = async () => await _sut.MoveAsync("nope", "dest");
        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}
