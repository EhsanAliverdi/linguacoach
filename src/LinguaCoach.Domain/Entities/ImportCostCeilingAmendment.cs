using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase 4.4B — one immutable, append-only audit record of an administrator raising a paused
/// plan's approved cost ceiling. Created exactly once per successful amendment (never mutated or
/// deleted afterward) — this is the audit trail the "cost ceiling may only be increased through an
/// explicit audited admin action" canonical rule requires. <see cref="PreviousCeiling"/> is
/// preserved here even though <see cref="ImportProfile.ApprovedCostCeiling"/> is overwritten by the
/// resume, so history is never lost.
/// </summary>
public sealed class ImportCostCeilingAmendment : BaseEntity
{
    public Guid ImportPackageId { get; private set; }
    public Guid ImportProfileId { get; private set; }
    public decimal PreviousCeiling { get; private set; }
    public decimal NewCeiling { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string Reason { get; private set; } = string.Empty;
    public Guid? AdministratorUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private ImportCostCeilingAmendment() { }

    public ImportCostCeilingAmendment(
        Guid importPackageId, Guid importProfileId, decimal previousCeiling, decimal newCeiling,
        string currency, string reason, Guid? administratorUserId, DateTimeOffset createdAtUtc)
    {
        if (importPackageId == Guid.Empty)
            throw new ArgumentException("ImportPackageId must not be empty.", nameof(importPackageId));
        if (importProfileId == Guid.Empty)
            throw new ArgumentException("ImportProfileId must not be empty.", nameof(importProfileId));
        if (previousCeiling < 0)
            throw new ArgumentOutOfRangeException(nameof(previousCeiling));
        if (newCeiling <= previousCeiling)
            throw new ArgumentOutOfRangeException(nameof(newCeiling), "A ceiling amendment must raise the ceiling above its previous value.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.", nameof(currency));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required for a cost ceiling amendment.", nameof(reason));

        ImportPackageId = importPackageId;
        ImportProfileId = importProfileId;
        PreviousCeiling = previousCeiling;
        NewCeiling = newCeiling;
        Currency = currency.Trim();
        Reason = reason.Trim();
        AdministratorUserId = administratorUserId;
        CreatedAtUtc = createdAtUtc;
    }
}
