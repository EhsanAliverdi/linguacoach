namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Phase 4 (Part F — metadata precedence and provenance) — where a field's current value came
/// from. Precedence, highest to lowest: <see cref="AdministratorCorrected"/> /
/// <see cref="AdministratorProvided"/> &gt; <see cref="SourceMetadata"/> &gt;
/// <see cref="DeterministicallyExtracted"/> &gt; <see cref="AIInferred"/> / <see cref="AITranscribed"/> /
/// <see cref="AIGenerated"/> &gt; <see cref="Unknown"/>. AI must never silently overwrite a value
/// whose origin is at or above <see cref="SourceMetadata"/> — see
/// <c>ResourceCandidateFieldProvenance.CanAiOverwrite</c>.
/// </summary>
public enum MetadataOrigin
{
    Unknown = 0,
    AIInferred = 1,
    AITranscribed = 2,
    AIGenerated = 3,
    DeterministicallyExtracted = 4,
    SourceMetadata = 5,
    AdministratorProvided = 6,
    AdministratorCorrected = 7
}
