using System.IO.Compression;
using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Infrastructure.ResourceImport;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase 4 (2026-07-15), Part A — ZIP ingestion security guards: path traversal, entry count,
/// per-file size, compression ratio, nested archives, and checksum computation.
/// </summary>
public sealed class ZipPackageInspectorTests
{
    private static ZipPackageInspector CreateInspector(ImportPackageLimitsOptions? limits = null) =>
        new(Options.Create(limits ?? new ImportPackageLimitsOptions()));

    private static MemoryStream BuildZip(params (string Name, byte[] Content)[] entries)
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }
        }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task InspectAsync_accepts_a_well_formed_archive()
    {
        using var zip = BuildZip(
            ("data/words.csv", System.Text.Encoding.UTF8.GetBytes("word,definition\nhello,greeting\n")),
            ("data/notes.txt", System.Text.Encoding.UTF8.GetBytes("some notes")));

        var manifest = await CreateInspector().InspectAsync(zip);

        manifest.IsAccepted.Should().BeTrue();
        manifest.EntryCount.Should().Be(2);
        manifest.Entries.Should().OnlyContain(e => !e.IsSuspicious);
        manifest.Entries.Should().OnlyContain(e => e.Checksum.Length == 64); // SHA-256 hex
    }

    [Fact]
    public async Task InspectAsync_flags_path_traversal_entries_as_suspicious()
    {
        using var zip = BuildZip(("../../etc/passwd", System.Text.Encoding.UTF8.GetBytes("x")));

        var manifest = await CreateInspector().InspectAsync(zip);

        manifest.IsAccepted.Should().BeTrue();
        manifest.SuspiciousEntries.Should().ContainSingle();
        manifest.SuspiciousEntries[0].SuspiciousReason.Should().Contain("traversal");
    }

    [Fact]
    public async Task InspectAsync_rejects_archive_exceeding_entry_count_limit()
    {
        var entries = Enumerable.Range(0, 5).Select(i => ($"file{i}.txt", System.Text.Encoding.UTF8.GetBytes("x"))).ToArray();
        using var zip = BuildZip(entries);

        var limits = new ImportPackageLimitsOptions { MaxEntryCount = 3 };
        var manifest = await CreateInspector(limits).InspectAsync(zip);

        manifest.IsAccepted.Should().BeFalse();
        manifest.RejectionReason.Should().Contain("entries");
    }

    [Fact]
    public async Task InspectAsync_flags_entries_exceeding_per_file_size_limit()
    {
        using var zip = BuildZip(("big.txt", new byte[1000]));

        var limits = new ImportPackageLimitsOptions { MaxIndividualFileSizeBytes = 500 };
        var manifest = await CreateInspector(limits).InspectAsync(zip);

        manifest.IsAccepted.Should().BeTrue();
        manifest.SuspiciousEntries.Should().ContainSingle();
        manifest.SuspiciousEntries[0].SuspiciousReason.Should().Contain("exceeding the per-file limit");
    }

    [Fact]
    public async Task InspectAsync_rejects_archive_exceeding_expanded_size_limit()
    {
        using var zip = BuildZip(("file.txt", new byte[10_000]));

        var limits = new ImportPackageLimitsOptions { MaxExpandedSizeBytes = 5_000 };
        var manifest = await CreateInspector(limits).InspectAsync(zip);

        manifest.IsAccepted.Should().BeFalse();
        manifest.RejectionReason.Should().Contain("expanded size");
    }

    [Fact]
    public async Task InspectAsync_flags_nested_archive_entries_when_nesting_is_disabled()
    {
        using var zip = BuildZip(("nested/inner.zip", new byte[100]));

        var manifest = await CreateInspector().InspectAsync(zip);

        manifest.SuspiciousEntries.Should().ContainSingle();
        manifest.SuspiciousEntries[0].SuspiciousReason.Should().Contain("Nested archives");
    }

    [Fact]
    public async Task InspectAsync_detects_duplicate_checksum_entries()
    {
        var content = System.Text.Encoding.UTF8.GetBytes("identical content");
        using var zip = BuildZip(("a.txt", content), ("b.txt", content));

        var manifest = await CreateInspector().InspectAsync(zip);

        manifest.DuplicateChecksumEntries.Should().HaveCount(2);
    }

    [Fact]
    public async Task InspectAsync_throws_for_a_stream_that_is_not_seekable()
    {
        await using var nonSeekable = new NonSeekableStream(new MemoryStream(new byte[10]));

        var act = async () => await CreateInspector().InspectAsync(nonSeekable);

        await act.Should().ThrowAsync<ImportPackageInspectionException>();
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly Stream _inner;
        public NonSeekableStream(Stream inner) => _inner = inner;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
