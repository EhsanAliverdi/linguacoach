using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Provenance/license record for an external CEFR reference dataset (e.g. CEFR-J / Open
/// Language Profiles, UniversalCEFR). Every content-bearing CEFR reference row
/// (<see cref="CefrDescriptor"/>, <see cref="ResourceBankItem"/>) carries a
/// SourceId FK to one of these rows, so license/provenance is traceable per-row, not just
/// per-import.
///
/// This entity exists to hold provenance metadata. It does not itself import or store any
/// external dataset content — <see cref="IsImportApproved"/> stays false until a licensing
/// review has explicitly cleared the source for import, and <see cref="ImportedAtUtc"/> stays
/// null until data has actually been imported.
///
/// Phase E1 extension: this registry is also the source of truth for the English resource
/// import staging pipeline (<see cref="ResourceImportRun"/>/<see cref="ResourceRawRecord"/>/
/// <see cref="ResourceCandidate"/>). Every source must be English-only — <see cref="LanguageCode"/>
/// must be exactly "en"; there is no supported non-English source shape in this codebase.
/// </summary>
public sealed class CefrResourceSource : BaseEntity
{
    public const string RequiredLanguageCode = "en";

    public string Name { get; private set; } = string.Empty;
    public string LicenseType { get; private set; } = string.Empty;
    public string? SourceUrl { get; private set; }
    public string? UsageRestrictionNotes { get; private set; }

    /// <summary>True only once a licensing review has explicitly cleared this source for
    /// import. False (the default) means content-bearing rows must not reference this source
    /// yet — reviewers should treat any non-approved source as import-blocked.</summary>
    public bool IsImportApproved { get; private set; }

    public DateTimeOffset? ImportedAtUtc { get; private set; }

    /// <summary>Always "en" — enforced by the constructor/<see cref="Update"/>. This entity has
    /// no supported representation for non-English sources.</summary>
    public string LanguageCode { get; private set; } = RequiredLanguageCode;

    /// <summary>Whether content staged from this source may ever be rendered directly to
    /// students (vs. reference/internal-only use).</summary>
    public bool AllowsStudentDisplay { get; private set; }

    /// <summary>Whether the source's license permits commercial (not just research/personal)
    /// use — required before any content from it may reach a paying student.</summary>
    public bool AllowsCommercialUse { get; private set; }

    /// <summary>Attribution text required by the source's license, if any.</summary>
    public string? AttributionText { get; private set; }

    /// <summary>Version/edition label of the source dataset, e.g. "2023-06". Snapshotted onto
    /// each <see cref="ResourceImportRun"/> at import time.</summary>
    public string? SourceVersion { get; private set; }

    /// <summary>Direct download/archive URL for the dataset, distinct from <see cref="SourceUrl"/>
    /// (which is more of a homepage/reference link).</summary>
    public string? DownloadUrl { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    private CefrResourceSource() { }

    public CefrResourceSource(
        string name,
        string licenseType,
        string? sourceUrl = null,
        string? usageRestrictionNotes = null,
        string languageCode = RequiredLanguageCode,
        bool allowsStudentDisplay = false,
        bool allowsCommercialUse = false,
        string? attributionText = null,
        string? sourceVersion = null,
        string? downloadUrl = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(licenseType))
            throw new ArgumentException("LicenseType is required.", nameof(licenseType));
        ValidateLanguageCode(languageCode);

        Name = name.Trim();
        LicenseType = licenseType.Trim();
        SourceUrl = sourceUrl?.Trim();
        UsageRestrictionNotes = usageRestrictionNotes?.Trim();
        IsImportApproved = false;
        LanguageCode = languageCode.Trim().ToLowerInvariant();
        AllowsStudentDisplay = allowsStudentDisplay;
        AllowsCommercialUse = allowsCommercialUse;
        AttributionText = attributionText?.Trim();
        SourceVersion = sourceVersion?.Trim();
        DownloadUrl = downloadUrl?.Trim();
    }

    /// <summary>Edits descriptive/license metadata only. Deliberately does not touch
    /// <see cref="IsImportApproved"/> — that stays governed solely by
    /// <see cref="ApproveForImport"/>/<see cref="RevokeApproval"/> so an unrelated metadata
    /// edit can never silently re-approve (or leave approved) a source.</summary>
    public void Update(
        string name,
        string licenseType,
        string? sourceUrl,
        string? usageRestrictionNotes,
        string languageCode,
        bool allowsStudentDisplay,
        bool allowsCommercialUse,
        string? attributionText,
        string? sourceVersion,
        string? downloadUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(licenseType))
            throw new ArgumentException("LicenseType is required.", nameof(licenseType));
        ValidateLanguageCode(languageCode);

        Name = name.Trim();
        LicenseType = licenseType.Trim();
        SourceUrl = sourceUrl?.Trim();
        UsageRestrictionNotes = usageRestrictionNotes?.Trim();
        LanguageCode = languageCode.Trim().ToLowerInvariant();
        AllowsStudentDisplay = allowsStudentDisplay;
        AllowsCommercialUse = allowsCommercialUse;
        AttributionText = attributionText?.Trim();
        SourceVersion = sourceVersion?.Trim();
        DownloadUrl = downloadUrl?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Marks this source as cleared for import following an explicit licensing review.</summary>
    public void ApproveForImport(string? notes = null)
    {
        IsImportApproved = true;
        if (notes is not null)
            UsageRestrictionNotes = notes.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Revokes a prior import approval (e.g. license terms changed).</summary>
    public void RevokeApproval(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required to revoke import approval.", nameof(reason));

        IsImportApproved = false;
        UsageRestrictionNotes = reason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Records that data from this source has been imported. Requires prior approval.</summary>
    public void RecordImport(DateTimeOffset importedAtUtc)
    {
        if (!IsImportApproved)
            throw new InvalidOperationException(
                $"Cannot record an import for source '{Name}': it has not been approved for import.");

        ImportedAtUtc = importedAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void ValidateLanguageCode(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)
            || !string.Equals(languageCode.Trim(), RequiredLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"LanguageCode must be '{RequiredLanguageCode}' — this registry has no supported non-English source shape.",
                nameof(languageCode));
        }
    }
}
