namespace LinguaCoach.Infrastructure.ResourceImport;

/// <summary>
/// Phase 4 (2026-07-15 large-scale AI import packages, Part N — cost, limits, and safety) — bound
/// from configuration section "ImportPackageLimits". Every limit here exists so the system never
/// "accepts work the infrastructure cannot safely complete" — a package exceeding any of these is
/// rejected with an actionable message (split the package, provide a smaller sample, etc.), never
/// silently truncated or partially processed.
/// </summary>
public sealed class ImportPackageLimitsOptions
{
    public const string SectionName = "ImportPackageLimits";

    /// <summary>Hard ceiling on the uploaded archive's own (compressed) size. Above this, the
    /// upload itself is rejected before any inspection — matches "do not accept work the
    /// infrastructure cannot safely complete."</summary>
    public long MaxCompressedSizeBytes { get; set; } = 2_000_000_000; // 2 GB

    /// <summary>Ceiling on the sum of every entry's declared (uncompressed) size, read from the
    /// ZIP central directory without decompressing — the primary zip-bomb defense.</summary>
    public long MaxExpandedSizeBytes { get; set; } = 8_000_000_000; // 8 GB

    public int MaxEntryCount { get; set; } = 200_000;

    /// <summary>Per-entry uncompressed/compressed ratio ceiling — a legitimate audio/image/text
    /// file rarely exceeds ~50:1; a crafted zip-bomb entry commonly claims 1000:1+.</summary>
    public int MaxCompressionRatioPerEntry { get; set; } = 100;

    /// <summary>0 = reject any nested archive entry outright (the safe default — this phase does
    /// not implement recursive extraction). A future phase could raise this and add recursive
    /// inspection bounded by the same limits at each level.</summary>
    public int MaxNestedArchiveDepth { get; set; } = 0;

    public long MaxIndividualFileSizeBytes { get; set; } = 500_000_000; // 500 MB (e.g. one long audio/video file)

    /// <summary>Above this expanded-size threshold, <c>IImportProcessingModeDecisionService</c>
    /// will never choose <c>Direct</c>/<c>FullAiAssisted</c> — sample-driven analysis is
    /// mandatory. See Part C.</summary>
    public long FullAiAnalysisMaxExpandedSizeBytes { get; set; } = 50_000_000; // 50 MB
    public int FullAiAnalysisMaxFileCount { get; set; } = 200;
    public int FullAiAnalysisMaxEstimatedRecordCount { get; set; } = 500;

    public int SampleMaxFilesPerGroup { get; set; } = 20;
    public int SampleMaxTotalFiles { get; set; } = 100;

    public int MaxAiInputCharacters { get; set; } = 60_000;
    public int MaxAiOutputCharacters { get; set; } = 20_000;
    public int CandidateBatchSize { get; set; } = 25;

    public int MaxTranscriptionDurationSecondsPerFile { get; set; } = 3600; // 1 hour
    public int MaxTtsTextLength { get; set; } = 5000;

    public int MaxConcurrentPackageJobs { get; set; } = 2;
    public int MaxJobRetries { get; set; } = 3;

    /// <summary>Phase 4.7 (2026-07-17 reliable large uploads) — size of each chunk the client
    /// uploads to the API for a resumable session-based upload. 32 MB keeps every part comfortably
    /// bounded in memory (streamed straight to storage, never buffered whole) and small enough to
    /// retry quickly on a flaky connection without resending a large amount of data.</summary>
    public long ChunkedUploadPartSizeBytes { get; set; } = 32 * 1024 * 1024; // 32 MB

    /// <summary>Ceiling on the number of parts a single session may declare — derived from
    /// <see cref="MaxCompressedSizeBytes"/> / <see cref="ChunkedUploadPartSizeBytes"/> with
    /// headroom; guards against a caller declaring an absurd part count to exhaust session-part
    /// bookkeeping rows.</summary>
    public int MaxUploadPartCount { get; set; } = 128;

    /// <summary>How long an upload session may sit idle (no completion) before it is considered
    /// expired and must be recreated. Parts already uploaded are not implicitly deleted by
    /// expiry alone — an explicit abort (or a fresh completion attempt, which will reject due to
    /// expiry) is what triggers cleanup.</summary>
    public int UploadSessionExpiryHours { get; set; } = 24;
}
