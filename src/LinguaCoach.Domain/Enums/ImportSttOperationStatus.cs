namespace LinguaCoach.Domain.Enums;

/// <summary>Phase 4.4 — lifecycle of one durable STT (speech-to-text) billable operation. See
/// <see cref="Entities.ImportSttOperation"/>.</summary>
public enum ImportSttOperationStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
}
