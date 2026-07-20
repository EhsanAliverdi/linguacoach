using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A single execution of the Phase E1 English resource import pipeline against one uploaded
/// file, scoped to one <see cref="CefrResourceSource"/>. Owns zero or more
/// <see cref="ResourceRawRecord"/> rows. Never writes to any published Cefr* bank table —
/// publishing is Phase E4, out of scope here.
/// </summary>
public sealed class ResourceImportRun : BaseEntity
{
    public const string CurrentParserVersion = "v1";

    public Guid CefrResourceSourceId { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public ResourceImportRunStatus Status { get; private set; }
    public Guid? ImportedByUserId { get; private set; }
    public ResourceImportMode ImportMode { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string FileHash { get; private set; } = string.Empty;
    public string? SourceVersion { get; private set; }
    public string ParserVersion { get; private set; } = CurrentParserVersion;

    /// <summary>Always null in Phase E1 — no AI analysis happens yet. Reserved for Phase E2.</summary>
    public string? AiModelUsed { get; private set; }

    public int TotalRecordCount { get; private set; }
    public int SucceededCount { get; private set; }
    public int RejectedCount { get; private set; }
    public int WarningCount { get; private set; }
    public string? ErrorSummary { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>Phase 4 (2026-07-15 large-scale AI import packages) — set when this run was
    /// created as part of an <see cref="ImportPackage"/>'s deterministic full-package processing
    /// (one run per detected schema/file group within the package). Null for a simple single-file
    /// CSV/JSON/JSONL/paste import — the pre-Phase-4 path, unchanged.</summary>
    public Guid? ImportPackageId { get; private set; }

    private ResourceImportRun() { }

    public ResourceImportRun(
        Guid cefrResourceSourceId,
        ResourceImportMode importMode,
        string fileName,
        string fileHash,
        DateTimeOffset startedAtUtc,
        Guid? importedByUserId = null,
        string? sourceVersion = null,
        string? notes = null,
        Guid? importPackageId = null)
    {
        if (cefrResourceSourceId == Guid.Empty)
            throw new ArgumentException("CefrResourceSourceId must not be empty.", nameof(cefrResourceSourceId));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("FileName is required.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(fileHash))
            throw new ArgumentException("FileHash is required.", nameof(fileHash));

        CefrResourceSourceId = cefrResourceSourceId;
        ImportMode = importMode;
        FileName = fileName.Trim();
        FileHash = fileHash.Trim();
        StartedAtUtc = startedAtUtc;
        ImportedByUserId = importedByUserId;
        SourceVersion = sourceVersion?.Trim();
        Notes = notes?.Trim();
        ParserVersion = CurrentParserVersion;
        Status = ResourceImportRunStatus.Running;
        ImportPackageId = importPackageId;
    }

    /// <summary>Marks the run failed before any row-level processing happened (e.g. source not
    /// approved, non-English source, or the file itself was unreadable/malformed).</summary>
    public void MarkFailed(string errorSummary, DateTimeOffset completedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(errorSummary))
            throw new ArgumentException("ErrorSummary is required to mark a run failed.", nameof(errorSummary));

        Status = ResourceImportRunStatus.Failed;
        ErrorSummary = errorSummary.Trim();
        CompletedAtUtc = completedAtUtc;
    }

    /// <summary>Platform Reliability Sprint 8 — one-time backfill for runs created by an internal
    /// seed-pack seeder before it started creating a self-approved <see cref="ImportPackage"/>/
    /// <see cref="ImportProfile"/> for its own content (Phase 4.2's mandatory publish-provenance
    /// gate postdates those runs, and this codebase's seeders are idempotent-by-source-name, so
    /// they never re-run to pick up the gate once a source already exists). Only valid when this
    /// run genuinely has no package yet — never call this to reassign an already-linked run.</summary>
    public void AssignRetroactiveImportPackage(Guid importPackageId)
    {
        if (ImportPackageId is not null)
            throw new InvalidOperationException(
                $"ResourceImportRun {Id} already has ImportPackageId {ImportPackageId} — cannot reassign.");
        if (importPackageId == Guid.Empty)
            throw new ArgumentException("importPackageId must not be empty.", nameof(importPackageId));

        ImportPackageId = importPackageId;
    }

    /// <summary>Finalizes counts/status after row-level processing completes.</summary>
    public void Complete(
        int totalRecordCount,
        int succeededCount,
        int rejectedCount,
        int warningCount,
        DateTimeOffset completedAtUtc,
        string? errorSummary = null)
    {
        TotalRecordCount = totalRecordCount;
        SucceededCount = succeededCount;
        RejectedCount = rejectedCount;
        WarningCount = warningCount;
        CompletedAtUtc = completedAtUtc;
        ErrorSummary = errorSummary?.Trim();

        Status = totalRecordCount == 0 || succeededCount == 0
            ? ResourceImportRunStatus.Failed
            : rejectedCount == 0
                ? ResourceImportRunStatus.Completed
                : ResourceImportRunStatus.CompletedWithWarnings;
    }
}
