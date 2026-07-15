namespace LinguaCoach.Domain.Enums;

/// <summary>Phase 4 — lifecycle of an <see cref="Entities.ImportProfile"/>. This entity is the
/// package's Import Execution Plan: the AI-proposed, admin-approved mapping/extraction ruleset
/// plus its volume/time/cost estimate and risk list, generated from a bounded package sample.
/// No material AI/STT/TTS/background processing may begin while a plan is outside
/// <see cref="Approved"/> or <see cref="Executing"/> — see <c>ImportPackage</c>'s
/// <c>ApprovedImportProfileId</c> gate.</summary>
public enum ImportProfileStatus
{
    Draft = 0,
    AwaitingApproval = 1,
    Approved = 2,
    Rejected = 3,
    /// <summary>A material deviation or a revised cost estimate exceeded the approved ceiling —
    /// execution is paused and a re-approval is required before it may resume.</summary>
    PausedForCostApproval = 4,
    Executing = 5,
    Completed = 6,
    Failed = 7,
    Cancelled = 8,
    /// <summary>A newer version was created for the same package (a material assumption
    /// changed) — this version can never execute again.</summary>
    Superseded = 9
}
