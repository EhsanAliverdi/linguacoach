namespace LinguaCoach.Domain.Enums;

/// <summary>Phase 4.4E — persisted status of an <see cref="Entities.ImportAsset"/>'s real
/// audio-duration measurement (replaces the previous flat five-minute assumption).</summary>
public enum ImportAudioDurationMeasurementStatus
{
    /// <summary>Never measured — either not audio, or audio not yet processed.</summary>
    NotMeasured = 0,
    Measured = 1,
    Failed = 2
}
