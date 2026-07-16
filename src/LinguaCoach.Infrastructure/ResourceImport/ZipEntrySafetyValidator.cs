using System.IO.Compression;
using System.Security.Cryptography;

namespace LinguaCoach.Infrastructure.ResourceImport;

// ── Phase 4.8 (2026-07-17 security/concurrency/idempotency) — the single hardened per-entry
// safety check, shared by inspection (ZipPackageInspector, sample-time) and extraction
// (ImportPackageProcessingService, execution-time). Phase 4 originally ran these checks only at
// inspection time and had extraction trust the stored manifest unconditionally; if the archive
// object on storage were ever mutated between inspection and extraction (or the manifest were
// stale/tampered), the traversal/zip-bomb guards would never be re-checked against the live
// bytes. Extraction now re-runs this exact validator against the real ZipArchiveEntry instead of
// trusting the manifest's IsSuspicious flag alone. ──

internal static class ZipEntrySafetyValidator
{
    private static readonly HashSet<string> NestedArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2" };

    public sealed record EntryValidationResult(bool IsSuspicious, string? Reason, string RelativePath, string Checksum);

    /// <summary>Validates one live ZIP entry against the same limits/rules the inspector applies,
    /// plus computes its bounded checksum. Never decompresses more than the entry's own declared
    /// length — a lying central directory aborts as a zip-bomb, exactly as at inspection time.</summary>
    public static EntryValidationResult Validate(ZipArchiveEntry entry, ImportPackageLimitsOptions limits)
    {
        var relativePath = NormalizeRelativePath(entry.FullName);

        if (IsPathTraversal(entry.FullName, relativePath))
            return new EntryValidationResult(true, "Entry path escapes the archive root (path traversal).", relativePath, string.Empty);

        if (entry.Length > limits.MaxIndividualFileSizeBytes)
            return new EntryValidationResult(true,
                $"Entry declares {entry.Length:N0} bytes, exceeding the per-file limit of {limits.MaxIndividualFileSizeBytes:N0}.",
                relativePath, string.Empty);

        if (entry.CompressedLength > 0 && entry.Length / (double)entry.CompressedLength > limits.MaxCompressionRatioPerEntry)
            return new EntryValidationResult(true,
                $"Entry compression ratio exceeds the configured limit of {limits.MaxCompressionRatioPerEntry}:1.",
                relativePath, string.Empty);

        if (limits.MaxNestedArchiveDepth <= 0 && NestedArchiveExtensions.Contains(Path.GetExtension(entry.Name)))
            return new EntryValidationResult(true, "Nested archives are not supported in this processing mode.", relativePath, string.Empty);

        string checksum;
        try
        {
            checksum = ComputeBoundedChecksum(entry);
        }
        catch (InvalidDataException)
        {
            return new EntryValidationResult(true, "Entry could not be read — likely encrypted or corrupt.", relativePath, string.Empty);
        }
        catch (ZipBombGuardException)
        {
            return new EntryValidationResult(true, "Entry's decompressed size exceeded its declared length (possible zip bomb).", relativePath, string.Empty);
        }

        return new EntryValidationResult(false, null, relativePath, checksum);
    }

    public static bool IsPathTraversal(string fullName, string normalizedRelativePath)
    {
        if (Path.IsPathRooted(fullName) || fullName.Contains(':'))
            return true;

        var segments = fullName.Split('/', '\\');
        return segments.Any(s => s == "..") || normalizedRelativePath.StartsWith("..", StringComparison.Ordinal);
    }

    public static string NormalizeRelativePath(string fullName)
    {
        var normalized = fullName.Replace('\\', '/').TrimStart('/');
        var combined = new List<string>();
        foreach (var segment in normalized.Split('/'))
        {
            if (segment is "" or ".")
                continue;
            if (segment == "..")
            {
                if (combined.Count > 0) combined.RemoveAt(combined.Count - 1);
                else combined.Add("..");
                continue;
            }
            combined.Add(segment);
        }
        return string.Join('/', combined);
    }

    private static string ComputeBoundedChecksum(ZipArchiveEntry entry)
    {
        using var entryStream = entry.Open();
        using var sha256 = SHA256.Create();
        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;
        while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > entry.Length)
                throw new ZipBombGuardException();
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
        }
        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
    }

    private sealed class ZipBombGuardException : Exception;
}
