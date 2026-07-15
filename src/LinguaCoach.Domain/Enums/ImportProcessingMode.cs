namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Phase 4 — how an <see cref="Entities.ImportPackage"/> is processed, decided deterministically
/// by <c>IImportProcessingModeDecisionService</c> from the package manifest against configured
/// limits. Shown to the administrator with its reason — never silently chosen.
/// </summary>
public enum ImportProcessingMode
{
    /// <summary>Well-structured source, known/stable mapping, no AI segmentation needed
    /// (e.g. a CSV with a recognized column schema) — the existing Phase E1/K1 deterministic
    /// parser handles it directly, matching pre-Phase-4 behavior exactly.</summary>
    Direct = 0,

    /// <summary>Small enough package, unstructured or multimodal content — AI analyzes every
    /// eligible file directly, no sample/profile step required.</summary>
    FullAiAssisted = 1,

    /// <summary>Package exceeds configured size/count/token thresholds — AI only analyzes a
    /// representative sample to build an Import Profile, which is then applied deterministically
    /// to the full package (AI only re-engaged for enrichment/ambiguous-record exceptions).</summary>
    SampleDriven = 2
}
