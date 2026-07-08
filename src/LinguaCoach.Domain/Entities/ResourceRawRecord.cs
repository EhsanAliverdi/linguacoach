using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// One as-imported row from a <see cref="ResourceImportRun"/>'s uploaded file, before any
/// candidate staging. Always carries a JSON representation (<see cref="RawJson"/>) regardless of
/// the original file format, so downstream code has one consistent shape to work with.
/// </summary>
public sealed class ResourceRawRecord : BaseEntity
{
    public Guid ResourceImportRunId { get; private set; }
    public string? ExternalRecordId { get; private set; }
    public string? RawJson { get; private set; }
    public string? RawText { get; private set; }
    public string RawHash { get; private set; } = string.Empty;
    public string DetectedLanguageCode { get; private set; } = string.Empty;
    public string DetectedFormat { get; private set; } = string.Empty;
    public ResourceRawRecordStatus ExtractionStatus { get; private set; }
    public string? ExtractionWarningsJson { get; private set; }

    private ResourceRawRecord() { }

    public ResourceRawRecord(
        Guid resourceImportRunId,
        string rawHash,
        string detectedLanguageCode,
        string detectedFormat,
        string? externalRecordId = null,
        string? rawJson = null,
        string? rawText = null,
        string? extractionWarningsJson = null)
    {
        if (resourceImportRunId == Guid.Empty)
            throw new ArgumentException("ResourceImportRunId must not be empty.", nameof(resourceImportRunId));
        if (string.IsNullOrWhiteSpace(rawHash))
            throw new ArgumentException("RawHash is required.", nameof(rawHash));
        if (string.IsNullOrWhiteSpace(detectedFormat))
            throw new ArgumentException("DetectedFormat is required.", nameof(detectedFormat));

        ResourceImportRunId = resourceImportRunId;
        RawHash = rawHash.Trim();
        DetectedLanguageCode = string.IsNullOrWhiteSpace(detectedLanguageCode)
            ? "unknown"
            : detectedLanguageCode.Trim().ToLowerInvariant();
        DetectedFormat = detectedFormat.Trim();
        ExternalRecordId = externalRecordId?.Trim();
        RawJson = rawJson;
        RawText = rawText?.Trim();
        ExtractionWarningsJson = extractionWarningsJson;
        ExtractionStatus = ResourceRawRecordStatus.Imported;
    }

    public void MarkParsed()
    {
        ExtractionStatus = ResourceRawRecordStatus.Parsed;
    }

    public void MarkRejected(string warningsJson)
    {
        if (string.IsNullOrWhiteSpace(warningsJson))
            throw new ArgumentException("WarningsJson is required to reject a raw record.", nameof(warningsJson));

        ExtractionStatus = ResourceRawRecordStatus.Rejected;
        ExtractionWarningsJson = warningsJson;
    }
}
