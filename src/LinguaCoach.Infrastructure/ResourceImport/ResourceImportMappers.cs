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

    // Stateless — safe to instantiate directly in this static mapper rather than threading a
    // serializer instance through every ToDto call site (see IResourceCandidateContentSerializer's
    // doc comment: it holds no state, only field-alias tables).
    private static readonly IResourceCandidateContentSerializer ContentSerializer = new ResourceCandidateContentSerializer();

    public static AdminResourceCandidateDto ToDto(
        ResourceCandidate candidate, Guid importRunId, Guid sourceId)
    {
        string? typedContentJson = null;
        IReadOnlyList<CandidateFieldError>? contentValidationErrors = null;

        if (ContentSerializer.SupportsTypedSchema(candidate.CandidateType))
        {
            var parsed = ContentSerializer.Parse(candidate.CandidateType, candidate.NormalizedJson, candidate.CanonicalText);
            if (parsed.Success && parsed.Content is not null)
            {
                typedContentJson = ContentSerializer.Serialize(parsed.Content);
                var validation = ContentSerializer.Validate(candidate.CandidateType, parsed.Content);
                contentValidationErrors = validation.IsValid ? Array.Empty<CandidateFieldError>() : validation.Errors;
            }
            else
            {
                contentValidationErrors = parsed.Errors;
            }
        }

        return ToDtoCore(candidate, importRunId, sourceId, typedContentJson, contentValidationErrors);
    }

    private static AdminResourceCandidateDto ToDtoCore(
        ResourceCandidate candidate, Guid importRunId, Guid sourceId,
        string? typedContentJson, IReadOnlyList<CandidateFieldError>? contentValidationErrors) => new(
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
        candidate.GrammarTagsJson,
        candidate.VocabularyTagsJson,
        candidate.PronunciationTagsJson,
        candidate.ActivitySuitabilityTagsJson,
        candidate.SafetyTagsJson,
        candidate.LicenseTagsJson,
        candidate.QualityScore,
        candidate.ContentFingerprint,
        candidate.AiAnalysisJson,
        candidate.ValidationStatus.ToString(),
        candidate.ReviewStatus.ToString(),
        candidate.RejectReason,
        candidate.AdminNotes,
        candidate.CreatedAt,
        candidate.UpdatedAtUtc,
        candidate.IsPublished,
        candidate.PublishedAtUtc,
        candidate.PublishedEntityType,
        candidate.PublishedEntityId,
        candidate.PublishedByUserId,
        ResourceCandidatePublishGateHelper.CanAttemptPublish(candidate),
        ResourceCandidatePublishGateHelper.DescribeHardBlock(candidate),
        typedContentJson,
        contentValidationErrors
    );
}
