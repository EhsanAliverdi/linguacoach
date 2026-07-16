using System.Security.Cryptography;
using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Application.Storage;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase 4.7 (2026-07-17 reliable large uploads) — see <c>ImportUploadSessionContracts.cs</c> for
/// the design rationale (API-proxied bounded chunks instead of client→storage direct multipart,
/// because the installed Minio SDK exposes no public multipart primitives).
///
/// Every part and the assembled archive are written through the injected
/// <see cref="IFileStorageService"/> — this class never touches the local filesystem directly, so
/// it behaves identically whether the configured backend is Local, MinIO, or the test fake.
/// </summary>
internal sealed class ImportUploadSessionService : IImportUploadSessionService
{
    private const string StorageCategory = "import-packages";
    private const string PartStorageCategory = "import-upload-session-parts";

    private readonly LinguaCoachDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IZipPackageInspector _inspector;
    private readonly ImportPackageLimitsOptions _limits;

    public ImportUploadSessionService(
        LinguaCoachDbContext db,
        IFileStorageService storage,
        IZipPackageInspector inspector,
        IOptions<ImportPackageLimitsOptions> limits)
    {
        _db = db;
        _storage = storage;
        _inspector = inspector;
        _limits = limits.Value;
    }

    public async Task<CreateImportUploadSessionResult> CreateAsync(
        CreateImportUploadSessionCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.OriginalFileName))
            throw new ResourceImportValidationException("A file name is required.");
        if (command.DeclaredTotalSizeBytes <= 0)
            throw new ResourceImportValidationException("Declared file size must be greater than zero.");
        if (command.DeclaredTotalSizeBytes > _limits.MaxCompressedSizeBytes)
        {
            throw new ResourceImportValidationException(
                $"File size {command.DeclaredTotalSizeBytes:N0} bytes exceeds the configured limit of " +
                $"{_limits.MaxCompressedSizeBytes:N0} bytes. Split the package into smaller archives.");
        }

        var sourceExists = await _db.CefrResourceSources.AnyAsync(s => s.Id == command.CefrResourceSourceId, ct);
        if (!sourceExists)
            throw new ResourceImportValidationException("The selected resource source does not exist.");

        var partSize = _limits.ChunkedUploadPartSizeBytes;
        var totalParts = (int)Math.Ceiling(command.DeclaredTotalSizeBytes / (double)partSize);
        if (totalParts > _limits.MaxUploadPartCount)
        {
            throw new ResourceImportValidationException(
                $"Declared size requires {totalParts:N0} parts, exceeding the configured limit of " +
                $"{_limits.MaxUploadPartCount:N0} parts.");
        }

        var extension = Path.GetExtension(command.OriginalFileName);
        var finalKey = _storage.GenerateKey(command.CefrResourceSourceId.ToString(), StorageCategory, extension);

        var session = new ImportUploadSession(
            command.CefrResourceSourceId,
            command.OriginalFileName,
            command.DeclaredTotalSizeBytes,
            partSize,
            finalKey,
            DateTimeOffset.UtcNow,
            TimeSpan.FromHours(_limits.UploadSessionExpiryHours),
            command.CreatedByUserId,
            command.DeclaredChecksumSha256,
            command.Notes);

        _db.ImportUploadSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return new CreateImportUploadSessionResult(session.Id, partSize, session.TotalPartsExpected, session.ExpiresAtUtc);
    }

    public async Task<UploadImportSessionPartResult> UploadPartAsync(
        UploadImportSessionPartCommand command, CancellationToken ct = default)
    {
        var session = await LoadOwnedSessionAsync(command.SessionId, command.RequestingUserId, ct);

        if (session.Status is ImportUploadSessionStatus.Completed or ImportUploadSessionStatus.Aborted)
        {
            throw new ResourceImportValidationException(
                $"Upload session is {session.Status} and can no longer accept parts.");
        }
        if (session.IsExpired(DateTimeOffset.UtcNow))
            throw new ResourceImportValidationException("Upload session has expired. Create a new session and resume.");

        if (command.PartNumber < 1 || command.PartNumber > session.TotalPartsExpected)
        {
            throw new ResourceImportValidationException(
                $"Part number {command.PartNumber} is out of range (expected 1..{session.TotalPartsExpected}).");
        }

        // Every part must equal the configured part size except (only) the final part, which may
        // be smaller (the remainder). A part claiming to be larger than the configured part size
        // is rejected outright — that would defeat the whole point of bounding chunk size.
        var isLastPart = command.PartNumber == session.TotalPartsExpected;
        var maxAllowedForThisPart = isLastPart
            ? session.DeclaredTotalSizeBytes - (session.PartSizeBytes * (session.TotalPartsExpected - 1))
            : session.PartSizeBytes;
        if (command.DeclaredSizeBytes <= 0 || command.DeclaredSizeBytes > maxAllowedForThisPart)
        {
            throw new ResourceImportValidationException(
                $"Part {command.PartNumber} declared size {command.DeclaredSizeBytes:N0} bytes exceeds the " +
                $"maximum allowed {maxAllowedForThisPart:N0} bytes for this part.");
        }

        session.MarkInProgress();

        var partKey = $"{PartStorageCategory}/{session.Id:N}/part-{command.PartNumber:D6}";

        long actualSize;
        string checksumHex;
        await using (var hashingStream = new HashingPassthroughStream(command.Content, SHA256.Create()))
        {
            await _storage.SaveAsync(partKey, hashingStream, "application/octet-stream", ct, command.DeclaredSizeBytes);
            actualSize = hashingStream.BytesRead;
            checksumHex = Convert.ToHexString(hashingStream.GetHash()).ToLowerInvariant();
        }

        if (actualSize != command.DeclaredSizeBytes)
        {
            await _storage.DeleteAsync(partKey, ct);
            throw new ResourceImportValidationException(
                $"Part {command.PartNumber} received {actualSize:N0} bytes but declared {command.DeclaredSizeBytes:N0} bytes.");
        }

        if (command.DeclaredChecksumSha256 is not null &&
            !string.Equals(command.DeclaredChecksumSha256, checksumHex, StringComparison.OrdinalIgnoreCase))
        {
            await _storage.DeleteAsync(partKey, ct);
            throw new ResourceImportValidationException(
                $"Part {command.PartNumber} checksum mismatch: declared {command.DeclaredChecksumSha256}, computed {checksumHex}.");
        }

        var existingPart = await _db.ImportUploadSessionParts
            .FirstOrDefaultAsync(p => p.ImportUploadSessionId == session.Id && p.PartNumber == command.PartNumber, ct);

        var uploadedAt = DateTimeOffset.UtcNow;
        if (existingPart is null)
        {
            _db.ImportUploadSessionParts.Add(new ImportUploadSessionPart(
                session.Id, command.PartNumber, actualSize, partKey, uploadedAt, checksumHex));
        }
        else
        {
            existingPart.Replace(actualSize, partKey, uploadedAt, checksumHex);
        }

        await _db.SaveChangesAsync(ct);

        return new UploadImportSessionPartResult(command.PartNumber, actualSize, checksumHex, uploadedAt);
    }

    public async Task<ImportUploadSessionStatusDto> GetStatusAsync(
        Guid sessionId, Guid? requestingUserId, CancellationToken ct = default)
    {
        var session = await LoadOwnedSessionAsync(sessionId, requestingUserId, ct);
        var parts = await _db.ImportUploadSessionParts
            .Where(p => p.ImportUploadSessionId == sessionId)
            .OrderBy(p => p.PartNumber)
            .Select(p => new ImportUploadSessionPartSummary(p.PartNumber, p.SizeBytes, p.Sha256Checksum, p.UploadedAtUtc))
            .ToListAsync(ct);

        return new ImportUploadSessionStatusDto(
            session.Id, session.Status, session.OriginalFileName, session.DeclaredTotalSizeBytes,
            session.PartSizeBytes, session.TotalPartsExpected, parts, session.ImportPackageId, session.ExpiresAtUtc);
    }

    public async Task<ImportPackageManifestSummaryDto> CompleteAsync(
        CompleteImportUploadSessionCommand command, CancellationToken ct = default)
    {
        var session = await LoadOwnedSessionAsync(command.SessionId, command.RequestingUserId, ct);

        // Idempotent: a session already completed just returns the same package's summary again —
        // no re-assembly, no re-inspection, no second ImportPackage row.
        if (session.Status == ImportUploadSessionStatus.Completed)
        {
            if (session.ImportPackageId is null)
                throw new ResourceImportValidationException("Session is marked completed but has no linked package.");
            return await GetExistingPackageSummaryAsync(session.ImportPackageId.Value, ct);
        }

        if (session.Status == ImportUploadSessionStatus.Aborted)
            throw new ResourceImportValidationException("Upload session was aborted and cannot be completed.");
        if (session.IsExpired(DateTimeOffset.UtcNow))
            throw new ResourceImportValidationException("Upload session has expired. Create a new session and resume.");

        var parts = await _db.ImportUploadSessionParts
            .Where(p => p.ImportUploadSessionId == session.Id)
            .OrderBy(p => p.PartNumber)
            .ToListAsync(ct);

        if (parts.Count != session.TotalPartsExpected)
        {
            throw new ResourceImportValidationException(
                $"Upload incomplete: {parts.Count} of {session.TotalPartsExpected} parts received.");
        }
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].PartNumber != i + 1)
            {
                throw new ResourceImportValidationException(
                    $"Parts are not contiguous — missing part {i + 1}.");
            }
        }

        var totalReceivedBytes = parts.Sum(p => p.SizeBytes);
        if (totalReceivedBytes != session.DeclaredTotalSizeBytes)
        {
            throw new ResourceImportValidationException(
                $"Assembled size {totalReceivedBytes:N0} bytes does not match declared size " +
                $"{session.DeclaredTotalSizeBytes:N0} bytes.");
        }

        // Assemble: stream each part's storage-backed content, in order, into the final object.
        // Never buffers the whole archive in memory — SequentialPartStream reads one part at a
        // time from IFileStorageService.ReadAsync and the underlying SaveAsync call streams it
        // straight through to the destination (Local: FileStream; MinIO: PutObjectArgs over the
        // stream directly, no longer copied into a full in-memory MemoryStream first — see
        // MinioFileStorageService.SaveAsync).
        string wholeFileChecksum;
        await using (var assembledStream = new SequentialPartStream(parts.Select(p => p.StorageKey).ToArray(), _storage))
        await using (var hashingStream = new HashingPassthroughStream(assembledStream, SHA256.Create(), leaveOpenInner: true))
        {
            await _storage.SaveAsync(session.FinalStorageKey, hashingStream, "application/zip", ct, totalReceivedBytes);
            wholeFileChecksum = Convert.ToHexString(hashingStream.GetHash()).ToLowerInvariant();
        }

        if (session.DeclaredChecksumSha256 is not null &&
            !string.Equals(session.DeclaredChecksumSha256, wholeFileChecksum, StringComparison.OrdinalIgnoreCase))
        {
            await _storage.DeleteAsync(session.FinalStorageKey, ct);
            throw new ResourceImportValidationException(
                $"Assembled archive checksum mismatch: declared {session.DeclaredChecksumSha256}, computed {wholeFileChecksum}.");
        }

        var package = new ImportPackage(
            session.CefrResourceSourceId,
            session.OriginalFileName,
            DateTimeOffset.UtcNow,
            session.CreatedByUserId,
            archiveStorageKey: session.FinalStorageKey,
            archiveChecksum: wholeFileChecksum,
            compressedSizeBytes: totalReceivedBytes,
            notes: session.Notes);
        package.MoveToStatus(ImportPackageStatus.Uploaded);

        _db.ImportPackages.Add(package);
        session.Complete(package.Id, DateTimeOffset.UtcNow);

        // Phase 4.8 — two concurrent CompleteAsync calls for the same session can both pass every
        // check above (both observe Status == InProgress before either commits) and both reach
        // this point. ConcurrencyStamp is a real EF concurrency token (see
        // ImportUploadSessionConfiguration), so only the first writer's SaveChangesAsync succeeds;
        // the second gets DbUpdateConcurrencyException and falls back to the same idempotent
        // already-completed path a normal duplicate completion call takes — never a second
        // ImportPackage row for one session.
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.Entry(package).State = EntityState.Detached;
            await _db.Entry(session).ReloadAsync(ct);
            if (session.Status == ImportUploadSessionStatus.Completed && session.ImportPackageId is not null)
                return await GetExistingPackageSummaryAsync(session.ImportPackageId.Value, ct);
            throw new ResourceImportValidationException(
                "Upload session completion conflicted with a concurrent request. Retry the request.");
        }

        // Only now — after every part is verified present, contiguous, size-matched, and
        // checksum-verified — does inspection/plan-eligible processing begin.
        var summary = await ImportPackageInspectionRunner.InspectAndPersistAsync(package, _db, _storage, _inspector, ct);

        // Best-effort cleanup of the now-redundant per-part objects; a failure here must not fail
        // the (already-successful) completion.
        foreach (var part in parts)
        {
            try { await _storage.DeleteAsync(part.StorageKey, ct); }
            catch { /* cleanup is best-effort — orphaned part objects do not affect correctness */ }
        }

        return summary;
    }

    public async Task AbortAsync(AbortImportUploadSessionCommand command, CancellationToken ct = default)
    {
        var session = await LoadOwnedSessionAsync(command.SessionId, command.RequestingUserId, ct);

        if (session.Status == ImportUploadSessionStatus.Completed)
            throw new ResourceImportValidationException("A completed upload session cannot be aborted.");
        if (session.Status == ImportUploadSessionStatus.Aborted)
            return; // already aborted — idempotent no-op

        var parts = await _db.ImportUploadSessionParts
            .Where(p => p.ImportUploadSessionId == session.Id)
            .ToListAsync(ct);

        session.Abort(DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(ct);

        foreach (var part in parts)
        {
            try { await _storage.DeleteAsync(part.StorageKey, ct); }
            catch { /* best-effort cleanup */ }
        }
    }

    private async Task<ImportUploadSession> LoadOwnedSessionAsync(Guid sessionId, Guid? requestingUserId, CancellationToken ct)
    {
        var session = await _db.ImportUploadSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new ResourceImportValidationException("Upload session not found.");
        if (!session.IsOwnedBy(requestingUserId))
            throw new ImportUploadSessionForbiddenException();
        return session;
    }

    private async Task<ImportPackageManifestSummaryDto> GetExistingPackageSummaryAsync(Guid packageId, CancellationToken ct)
    {
        var package = await _db.ImportPackages.FirstOrDefaultAsync(p => p.Id == packageId, ct)
            ?? throw new ResourceImportValidationException("Linked import package not found.");

        if (string.IsNullOrEmpty(package.ManifestJson))
        {
            return new ImportPackageManifestSummaryDto(
                package.Id, package.Status, IsAccepted: package.Status != ImportPackageStatus.Failed,
                package.ErrorSummary, package.CompressedSizeBytes ?? 0, 0, 0,
                Array.Empty<ImportPackageFolderGroup>(), Array.Empty<string>(), 0, 0, 0);
        }

        var manifest = System.Text.Json.JsonSerializer.Deserialize<ImportPackageManifest>(package.ManifestJson)!;
        return ImportPackageManifestSummaryMapper.ToSummary(package, manifest);
    }
}
