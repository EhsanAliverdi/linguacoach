using LinguaCoach.Application.ResourceImport;
using LinguaCoach.Domain.Entities;

namespace LinguaCoach.Infrastructure.ResourceImport;

internal static class ResourceImportMappers
{
    public static AdminResourceSourceDto ToDto(CefrResourceSource source) => new(
        source.Id,
        source.Name,
        source.LicenseType,
        source.SourceUrl,
        source.UsageRestrictionNotes,
        source.IsImportApproved,
        source.ImportedAtUtc,
        source.LanguageCode,
        source.AllowsStudentDisplay,
        source.AllowsCommercialUse,
        source.AttributionText,
        source.SourceVersion,
        source.DownloadUrl,
        source.CreatedAt,
        source.UpdatedAtUtc
    );

    public static AdminResourceImportRunDto ToDto(ResourceImportRun run, string sourceName) => new(
        run.Id,
        run.CefrResourceSourceId,
        sourceName,
        run.StartedAtUtc,
        run.CompletedAtUtc,
        run.Status.ToString(),
        run.ImportedByUserId,
        run.ImportMode.ToString(),
        run.FileName,
        run.FileHash,
        run.SourceVersion,
        run.ParserVersion,
        run.AiModelUsed,
        run.TotalRecordCount,
        run.SucceededCount,
        run.RejectedCount,
        run.WarningCount,
        run.ErrorSummary,
        run.Notes
    );

    public static AdminResourceRawRecordDto ToDto(ResourceRawRecord record) => new(
        record.Id,
        record.ResourceImportRunId,
        record.ExternalRecordId,
        record.RawJson,
        record.RawText,
        record.RawHash,
        record.DetectedLanguageCode,
        record.DetectedFormat,
        record.ExtractionStatus.ToString(),
        record.ExtractionWarningsJson,
        record.CreatedAt
    );

    public static AdminResourceCandidateDto ToDto(
        ResourceCandidate candidate, Guid importRunId, Guid sourceId) => new(
        candidate.Id,
        candidate.ResourceRawRecordId,
        importRunId,
        sourceId,
        candidate.CandidateType.ToString(),
        candidate.CanonicalText,
        candidate.NormalizedJson,
        candidate.LanguageCode,
        candidate.CefrLevel,
        candidate.CefrConfidence,
        candidate.PrimarySkill,
        candidate.Subskill,
        candidate.DifficultyBand,
        candidate.ContextTagsJson,
        candidate.FocusTagsJson,
        candidate.QualityScore,
        candidate.ContentFingerprint,
        candidate.ValidationStatus.ToString(),
        candidate.ReviewStatus.ToString(),
        candidate.RejectReason,
        candidate.AdminNotes,
        candidate.CreatedAt,
        candidate.UpdatedAtUtc
    );
}
