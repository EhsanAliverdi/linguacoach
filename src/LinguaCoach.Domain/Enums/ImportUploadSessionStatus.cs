namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Phase 4.7 (2026-07-17 reliable large uploads) — lifecycle of a chunked-upload session for an
/// Import Package archive. A session exists only while the archive's bytes are being assembled;
/// once <see cref="Completed"/> it is linked to the <c>ImportPackage</c> row it produced and is
/// never mutated again (idempotent completion returns the same package).
/// </summary>
public enum ImportUploadSessionStatus
{
    Created = 0,
    InProgress = 1,
    Completed = 2,
    Aborted = 3,
}
