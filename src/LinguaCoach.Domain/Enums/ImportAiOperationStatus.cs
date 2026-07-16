namespace LinguaCoach.Domain.Enums;

/// <summary>Mirrors <see cref="ImportSttOperationStatus"/> for AI candidate-enrichment operations
/// (Phase 4.4D) — see <see cref="Entities.ImportAiEnrichmentOperation"/>.</summary>
public enum ImportAiOperationStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2
}
