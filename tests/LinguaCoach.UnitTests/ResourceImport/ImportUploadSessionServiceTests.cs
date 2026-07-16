using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.ResourceImport;
using LinguaCoach.Infrastructure.Storage;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinguaCoach.UnitTests.ResourceImport;

/// <summary>
/// Phase 4.7 (2026-07-17 reliable large uploads) — coverage for the resumable, chunked-upload
/// session lifecycle (<see cref="ImportUploadSessionService"/>): create → upload parts (any
/// retryable) → resume with the same session id → complete (idempotent, size/checksum verified,
/// inspection gated on full verification) → abort. Uses a tiny <c>ChunkedUploadPartSizeBytes</c>
/// so a small test ZIP still splits across multiple parts, exercising the same code path a real
/// multi-hundred-megabyte upload would.
/// </summary>
public sealed class ImportUploadSessionServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly FakeFileStorageService _storage = new();
    private readonly ImportPackageLimitsOptions _limits = new() { ChunkedUploadPartSizeBytes = 20, MaxUploadPartCount = 128 };
    private readonly ImportUploadSessionService _sessions;

    public ImportUploadSessionServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var inspector = new ZipPackageInspector(Options.Create(_limits));
        _sessions = new ImportUploadSessionService(_db, _storage, inspector, Options.Create(_limits));
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private async Task<Guid> SeedSourceAsync()
    {
        var source = new CefrResourceSource($"Test Source {Guid.NewGuid():N}", "CC-BY-4.0", allowsStudentDisplay: true, allowsCommercialUse: true);
        source.ApproveForImport("test");
        _db.CefrResourceSources.Add(source);
        await _db.SaveChangesAsync();
        return source.Id;
    }

    private static byte[] BuildSmallCsvZip()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("words.csv");
            using var entryStream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes("word,definition\nhello,greeting\nworld,planet\n");
            entryStream.Write(bytes, 0, bytes.Length);
        }
        return ms.ToArray();
    }

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>Splits <paramref name="bytes"/> into <paramref name="partSize"/>-sized chunks
    /// (last chunk may be smaller), mirroring what the Angular client does with
    /// <c>Blob.slice</c>.</summary>
    private static List<byte[]> SplitIntoParts(byte[] bytes, long partSize)
    {
        var parts = new List<byte[]>();
        for (var offset = 0; offset < bytes.Length; offset += (int)partSize)
        {
            var len = (int)Math.Min(partSize, bytes.Length - offset);
            var chunk = new byte[len];
            Array.Copy(bytes, offset, chunk, 0, len);
            parts.Add(chunk);
        }
        return parts;
    }

    private async Task UploadAllPartsAsync(Guid sessionId, List<byte[]> parts, Guid? userId = null)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            await _sessions.UploadPartAsync(new UploadImportSessionPartCommand(
                sessionId, i + 1, new MemoryStream(parts[i]), parts[i].Length, userId));
        }
    }

    [Fact]
    public async Task Create_upload_all_parts_and_complete_succeeds()
    {
        var sourceId = await SeedSourceAsync();
        var zipBytes = BuildSmallCsvZip();

        var created = await _sessions.CreateAsync(new CreateImportUploadSessionCommand(
            sourceId, "words.zip", zipBytes.Length, CreatedByUserId: null));

        created.TotalPartsExpected.Should().BeGreaterThan(1, "the test part size is tiny so a small zip should still span multiple parts");

        var parts = SplitIntoParts(zipBytes, created.PartSizeBytes);
        await UploadAllPartsAsync(created.SessionId, parts);

        var summary = await _sessions.CompleteAsync(new CompleteImportUploadSessionCommand(created.SessionId, null));

        summary.IsAccepted.Should().BeTrue();
        var session = await _db.ImportUploadSessions.FirstAsync(s => s.Id == created.SessionId);
        session.Status.Should().Be(ImportUploadSessionStatus.Completed);
        session.ImportPackageId.Should().NotBeNull();
    }

    [Fact]
    public async Task A_failed_part_can_be_retried_without_restarting_the_session()
    {
        var sourceId = await SeedSourceAsync();
        var zipBytes = BuildSmallCsvZip();

        var created = await _sessions.CreateAsync(new CreateImportUploadSessionCommand(sourceId, "words.zip", zipBytes.Length, null));
        var parts = SplitIntoParts(zipBytes, created.PartSizeBytes);

        // Upload part 1 "wrong" first (simulating corrupted bytes reaching the server), then retry
        // with the correct bytes for the same part number — this must simply replace it.
        var wrongFirstPart = new byte[parts[0].Length];
        Array.Fill(wrongFirstPart, (byte)0xFF);
        await _sessions.UploadPartAsync(new UploadImportSessionPartCommand(created.SessionId, 1, new MemoryStream(wrongFirstPart), wrongFirstPart.Length, null));
        await _sessions.UploadPartAsync(new UploadImportSessionPartCommand(created.SessionId, 1, new MemoryStream(parts[0]), parts[0].Length, null));

        for (var i = 1; i < parts.Count; i++)
            await _sessions.UploadPartAsync(new UploadImportSessionPartCommand(created.SessionId, i + 1, new MemoryStream(parts[i]), parts[i].Length, null));

        var summary = await _sessions.CompleteAsync(new CompleteImportUploadSessionCommand(created.SessionId, null));
        summary.IsAccepted.Should().BeTrue();
    }

    [Fact]
    public async Task An_interrupted_upload_can_resume_with_the_same_session_and_complete()
    {
        var sourceId = await SeedSourceAsync();
        var zipBytes = BuildSmallCsvZip();

        var created = await _sessions.CreateAsync(new CreateImportUploadSessionCommand(sourceId, "words.zip", zipBytes.Length, null));
        var parts = SplitIntoParts(zipBytes, created.PartSizeBytes);

        // Only the first part arrives before the "interruption".
        await _sessions.UploadPartAsync(new UploadImportSessionPartCommand(created.SessionId, 1, new MemoryStream(parts[0]), parts[0].Length, null));

        // A fresh service instance (simulating a new request / process) resumes using the same id.
        var inspector = new ZipPackageInspector(Options.Create(_limits));
        var resumedSessions = new ImportUploadSessionService(_db, _storage, inspector, Options.Create(_limits));

        var status = await resumedSessions.GetStatusAsync(created.SessionId, null);
        status.UploadedParts.Should().ContainSingle(p => p.PartNumber == 1);

        for (var i = 1; i < parts.Count; i++)
            await resumedSessions.UploadPartAsync(new UploadImportSessionPartCommand(created.SessionId, i + 1, new MemoryStream(parts[i]), parts[i].Length, null));

        var summary = await resumedSessions.CompleteAsync(new CompleteImportUploadSessionCommand(created.SessionId, null));
        summary.IsAccepted.Should().BeTrue();
    }

    [Fact]
    public async Task Aborted_session_cannot_subsequently_complete()
    {
        var sourceId = await SeedSourceAsync();
        var zipBytes = BuildSmallCsvZip();

        var created = await _sessions.CreateAsync(new CreateImportUploadSessionCommand(sourceId, "words.zip", zipBytes.Length, null));
        var parts = SplitIntoParts(zipBytes, created.PartSizeBytes);
        await UploadAllPartsAsync(created.SessionId, parts);

        await _sessions.AbortAsync(new AbortImportUploadSessionCommand(created.SessionId, null));

        var act = async () => await _sessions.CompleteAsync(new CompleteImportUploadSessionCommand(created.SessionId, null));
        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }

    [Fact]
    public async Task Completion_is_idempotent_and_does_not_create_a_duplicate_package()
    {
        var sourceId = await SeedSourceAsync();
        var zipBytes = BuildSmallCsvZip();

        var created = await _sessions.CreateAsync(new CreateImportUploadSessionCommand(sourceId, "words.zip", zipBytes.Length, null));
        var parts = SplitIntoParts(zipBytes, created.PartSizeBytes);
        await UploadAllPartsAsync(created.SessionId, parts);

        var first = await _sessions.CompleteAsync(new CompleteImportUploadSessionCommand(created.SessionId, null));
        var second = await _sessions.CompleteAsync(new CompleteImportUploadSessionCommand(created.SessionId, null));

        first.ImportPackageId.Should().Be(second.ImportPackageId);
        var packageCount = await _db.ImportPackages.CountAsync(p => p.CefrResourceSourceId == sourceId);
        packageCount.Should().Be(1);
    }

    [Fact]
    public async Task A_different_users_session_cannot_be_accessed()
    {
        var sourceId = await SeedSourceAsync();
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var zipBytes = BuildSmallCsvZip();

        var created = await _sessions.CreateAsync(new CreateImportUploadSessionCommand(sourceId, "words.zip", zipBytes.Length, ownerUserId));

        var statusAct = async () => await _sessions.GetStatusAsync(created.SessionId, otherUserId);
        await statusAct.Should().ThrowAsync<ImportUploadSessionForbiddenException>();

        var uploadAct = async () => await _sessions.UploadPartAsync(new UploadImportSessionPartCommand(
            created.SessionId, 1, new MemoryStream(new byte[] { 1 }), 1, otherUserId));
        await uploadAct.Should().ThrowAsync<ImportUploadSessionForbiddenException>();

        var completeAct = async () => await _sessions.CompleteAsync(new CompleteImportUploadSessionCommand(created.SessionId, otherUserId));
        await completeAct.Should().ThrowAsync<ImportUploadSessionForbiddenException>();

        var abortAct = async () => await _sessions.AbortAsync(new AbortImportUploadSessionCommand(created.SessionId, otherUserId));
        await abortAct.Should().ThrowAsync<ImportUploadSessionForbiddenException>();
    }

    [Fact]
    public async Task Too_many_parts_is_rejected_at_session_creation()
    {
        var sourceId = await SeedSourceAsync();
        var declaredSize = _limits.ChunkedUploadPartSizeBytes * (_limits.MaxUploadPartCount + 5);

        var act = async () => await _sessions.CreateAsync(new CreateImportUploadSessionCommand(sourceId, "huge.zip", declaredSize, null));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }

    [Fact]
    public async Task An_oversized_part_is_rejected()
    {
        var sourceId = await SeedSourceAsync();
        var created = await _sessions.CreateAsync(new CreateImportUploadSessionCommand(sourceId, "words.zip", 100, null));

        var tooBig = new byte[created.PartSizeBytes + 1];
        var act = async () => await _sessions.UploadPartAsync(new UploadImportSessionPartCommand(
            created.SessionId, 1, new MemoryStream(tooBig), tooBig.Length, null));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }

    [Fact]
    public async Task An_out_of_range_part_number_is_rejected()
    {
        var sourceId = await SeedSourceAsync();
        var created = await _sessions.CreateAsync(new CreateImportUploadSessionCommand(sourceId, "words.zip", 10, null));

        var act = async () => await _sessions.UploadPartAsync(new UploadImportSessionPartCommand(
            created.SessionId, created.TotalPartsExpected + 5, new MemoryStream(new byte[] { 1 }), 1, null));

        await act.Should().ThrowAsync<ResourceImportValidationException>();
    }

    [Fact]
    public async Task Final_size_mismatch_between_declared_and_received_is_rejected()
    {
        var sourceId = await SeedSourceAsync();
        var zipBytes = BuildSmallCsvZip();

        var created = await _sessions.CreateAsync(new CreateImportUploadSessionCommand(sourceId, "words.zip", zipBytes.Length, null));
        var parts = SplitIntoParts(zipBytes, created.PartSizeBytes);
        parts.Count.Should().BeGreaterThan(1);
        parts[0].Length.Should().BeGreaterThan(1, "the first part is always a full-size chunk, unlike a possibly 1-byte remainder last part");

        // Send one byte fewer than the real first part — the declared size for that call matches
        // what's actually sent, so per-part validation passes, but the assembled total ends up
        // short of the session's declared total size.
        var shortFirstPart = parts[0][..(parts[0].Length - 1)];
        await _sessions.UploadPartAsync(new UploadImportSessionPartCommand(
            created.SessionId, 1, new MemoryStream(shortFirstPart), shortFirstPart.Length, null));

        for (var i = 1; i < parts.Count; i++)
            await _sessions.UploadPartAsync(new UploadImportSessionPartCommand(created.SessionId, i + 1, new MemoryStream(parts[i]), parts[i].Length, null));

        var act = async () => await _sessions.CompleteAsync(new CompleteImportUploadSessionCommand(created.SessionId, null));
        await act.Should().ThrowAsync<ResourceImportValidationException>().WithMessage("*does not match declared size*");
    }

    [Fact]
    public async Task Whole_file_checksum_mismatch_is_rejected()
    {
        var sourceId = await SeedSourceAsync();
        var zipBytes = BuildSmallCsvZip();
        var wrongChecksum = Sha256Hex(Encoding.UTF8.GetBytes("not the real content"));

        var created = await _sessions.CreateAsync(new CreateImportUploadSessionCommand(
            sourceId, "words.zip", zipBytes.Length, null, DeclaredChecksumSha256: wrongChecksum));
        var parts = SplitIntoParts(zipBytes, created.PartSizeBytes);
        await UploadAllPartsAsync(created.SessionId, parts);

        var act = async () => await _sessions.CompleteAsync(new CompleteImportUploadSessionCommand(created.SessionId, null));
        await act.Should().ThrowAsync<ResourceImportValidationException>().WithMessage("*checksum mismatch*");
    }

    [Fact]
    public async Task Per_part_checksum_mismatch_is_rejected()
    {
        var sourceId = await SeedSourceAsync();
        var created = await _sessions.CreateAsync(new CreateImportUploadSessionCommand(sourceId, "words.zip", 10, null));
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var wrongChecksum = Sha256Hex(new byte[] { 9, 9, 9 });

        var act = async () => await _sessions.UploadPartAsync(new UploadImportSessionPartCommand(
            created.SessionId, 1, new MemoryStream(payload), payload.Length, null, DeclaredChecksumSha256: wrongChecksum));

        await act.Should().ThrowAsync<ResourceImportValidationException>().WithMessage("*checksum mismatch*");
    }

    [Fact]
    public async Task Package_inspection_does_not_start_before_completion()
    {
        var sourceId = await SeedSourceAsync();
        var zipBytes = BuildSmallCsvZip();

        var created = await _sessions.CreateAsync(new CreateImportUploadSessionCommand(sourceId, "words.zip", zipBytes.Length, null));
        var parts = SplitIntoParts(zipBytes, created.PartSizeBytes);

        // Upload only some parts — no ImportPackage row should exist yet.
        await _sessions.UploadPartAsync(new UploadImportSessionPartCommand(created.SessionId, 1, new MemoryStream(parts[0]), parts[0].Length, null));
        (await _db.ImportPackages.CountAsync(p => p.CefrResourceSourceId == sourceId)).Should().Be(0);

        for (var i = 1; i < parts.Count; i++)
            await _sessions.UploadPartAsync(new UploadImportSessionPartCommand(created.SessionId, i + 1, new MemoryStream(parts[i]), parts[i].Length, null));

        // All parts uploaded but Complete was never called — still no ImportPackage row.
        (await _db.ImportPackages.CountAsync(p => p.CefrResourceSourceId == sourceId)).Should().Be(0);

        await _sessions.CompleteAsync(new CompleteImportUploadSessionCommand(created.SessionId, null));
        (await _db.ImportPackages.CountAsync(p => p.CefrResourceSourceId == sourceId)).Should().Be(1);
    }

    [Fact]
    public async Task Completion_streams_the_assembled_archive_with_a_known_size_hint_instead_of_buffering()
    {
        // Phase 4.7 memory-safety proof: FakeFileStorageService records whether SaveAsync received
        // a size hint. The real bug this phase fixes was MinioFileStorageService fully buffering
        // an unknown-length stream into a MemoryStream; supplying a known size lets it skip that
        // buffering entirely (see MinioFileStorageService.SaveAsync). This proves the session
        // service always supplies that hint for the assembled archive, not just small saves.
        var sourceId = await SeedSourceAsync();
        var zipBytes = BuildSmallCsvZip();

        var created = await _sessions.CreateAsync(new CreateImportUploadSessionCommand(sourceId, "words.zip", zipBytes.Length, null));
        var parts = SplitIntoParts(zipBytes, created.PartSizeBytes);
        await UploadAllPartsAsync(created.SessionId, parts);

        await _sessions.CompleteAsync(new CompleteImportUploadSessionCommand(created.SessionId, null));

        _storage.LastSaveUsedKnownSize.Should().BeTrue();
    }
}
