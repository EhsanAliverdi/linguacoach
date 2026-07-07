using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Provenance/license record for an external CEFR reference dataset (e.g. CEFR-J / Open
/// Language Profiles, UniversalCEFR). Every content-bearing CEFR reference row
/// (<see cref="CefrDescriptor"/>, <see cref="CefrVocabularyEntry"/>,
/// <see cref="CefrGrammarProfileEntry"/>, <see cref="CefrReadingReference"/>) carries a
/// SourceId FK to one of these rows, so license/provenance is traceable per-row, not just
/// per-import.
///
/// This entity exists to hold provenance metadata. It does not itself import or store any
/// external dataset content — <see cref="IsImportApproved"/> stays false until a licensing
/// review has explicitly cleared the source for import, and <see cref="ImportedAtUtc"/> stays
/// null until data has actually been imported.
/// </summary>
public sealed class CefrResourceSource : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string LicenseType { get; private set; } = string.Empty;
    public string? SourceUrl { get; private set; }
    public string? UsageRestrictionNotes { get; private set; }

    /// <summary>True only once a licensing review has explicitly cleared this source for
    /// import. False (the default) means content-bearing rows must not reference this source
    /// yet — reviewers should treat any non-approved source as import-blocked.</summary>
    public bool IsImportApproved { get; private set; }

    public DateTimeOffset? ImportedAtUtc { get; private set; }

    private CefrResourceSource() { }

    public CefrResourceSource(
        string name,
        string licenseType,
        string? sourceUrl = null,
        string? usageRestrictionNotes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(licenseType))
            throw new ArgumentException("LicenseType is required.", nameof(licenseType));

        Name = name.Trim();
        LicenseType = licenseType.Trim();
        SourceUrl = sourceUrl?.Trim();
        UsageRestrictionNotes = usageRestrictionNotes?.Trim();
        IsImportApproved = false;
    }

    /// <summary>Marks this source as cleared for import following an explicit licensing review.</summary>
    public void ApproveForImport(string? notes = null)
    {
        IsImportApproved = true;
        if (notes is not null)
            UsageRestrictionNotes = notes.Trim();
    }

    /// <summary>Revokes a prior import approval (e.g. license terms changed).</summary>
    public void RevokeApproval(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required to revoke import approval.", nameof(reason));

        IsImportApproved = false;
        UsageRestrictionNotes = reason.Trim();
    }

    /// <summary>Records that data from this source has been imported. Requires prior approval.</summary>
    public void RecordImport(DateTimeOffset importedAtUtc)
    {
        if (!IsImportApproved)
            throw new InvalidOperationException(
                $"Cannot record an import for source '{Name}': it has not been approved for import.");

        ImportedAtUtc = importedAtUtc;
    }
}
