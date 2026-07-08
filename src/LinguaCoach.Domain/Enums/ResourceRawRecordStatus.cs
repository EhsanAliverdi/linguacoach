namespace LinguaCoach.Domain.Enums;

/// <summary>
/// Extraction status of a single <see cref="Entities.ResourceRawRecord"/> within an import run.
/// </summary>
public enum ResourceRawRecordStatus
{
    /// <summary>Row was read from the file but not yet gate-checked.</summary>
    Imported = 0,

    /// <summary>Row passed all E1 gates and produced a <see cref="Entities.ResourceCandidate"/>.</summary>
    Parsed = 1,

    /// <summary>Row failed a gate (language, duplicate, or missing recognizable content field) —
    /// no candidate was created for it.</summary>
    Rejected = 2
}
