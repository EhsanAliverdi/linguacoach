namespace LinguaCoach.Domain.Enums;

/// <summary>Phase 4 — per-asset processing state within its package, independent of the
/// package's own overall <see cref="ImportPackageStatus"/> (one bad asset must not block the rest
/// of the package — see the "isolate failed items" requirement).</summary>
public enum ImportAssetProcessingState
{
    Pending = 0,
    Inspected = 1,
    Rejected = 2, // failed a security/format/size check — see ValidationErrorsJson
    Linked = 3,   // paired with a candidate/other asset (e.g. audio<->transcript)
    Processed = 4,
    Failed = 5
}
