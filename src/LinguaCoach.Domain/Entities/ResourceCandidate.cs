using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A staged candidate produced from a <see cref="ResourceRawRecord"/> that passed all Phase E1
/// import gates (English-only, within-run duplicate check, recognizable-content check). This is
/// staging only — no row here has been published to any Cefr* bank table, no AI analysis has
/// run (<see cref="AiAnalysisJson"/> is always null in E1), and no CEFR level/quality
/// classification has happened yet. Publishing is Phase E4.
/// </summary>
public sealed class ResourceCandidate : BaseEntity
{
    public Guid ResourceRawRecordId { get; private set; }
    public ResourceCandidateType CandidateType { get; private set; }
    public string CanonicalText { get; private set; } = string.Empty;
    public string NormalizedJson { get; private set; } = string.Empty;
    public string LanguageCode { get; private set; } = string.Empty;

    // Placeholder classification fields — always null/default in E1, populated by Phase E2.
    public string? CefrLevel { get; private set; }
    public double? CefrConfidence { get; private set; }
    public string? PrimarySkill { get; private set; }
    public string? Subskill { get; private set; }
    public int? DifficultyBand { get; private set; }

    public string? ContextTagsJson { get; private set; } = "[]";
    public string? FocusTagsJson { get; private set; } = "[]";
    public string? GrammarTagsJson { get; private set; }
    public string? VocabularyTagsJson { get; private set; }
    public string? PronunciationTagsJson { get; private set; }
    public string? ActivitySuitabilityTagsJson { get; private set; }
    public string? SafetyTagsJson { get; private set; }
    public string? LicenseTagsJson { get; private set; }
    public double? QualityScore { get; private set; }

    public string SearchText { get; private set; } = string.Empty;
    public string ContentFingerprint { get; private set; } = string.Empty;

    /// <summary>Always null in Phase E1 — reserved for Phase E2 AI analysis output.</summary>
    public string? AiAnalysisJson { get; private set; }

    public ResourceCandidateValidationStatus ValidationStatus { get; private set; }
    public AdminReviewStatus ReviewStatus { get; private set; }
    public string? RejectReason { get; private set; }
    public string? AdminNotes { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    private ResourceCandidate() { }

    public ResourceCandidate(
        Guid resourceRawRecordId,
        ResourceCandidateType candidateType,
        string canonicalText,
        string normalizedJson,
        string languageCode,
        string searchText,
        string contentFingerprint,
        ResourceCandidateValidationStatus validationStatus,
        string? contextTagsJson = "[]",
        string? focusTagsJson = "[]")
    {
        if (resourceRawRecordId == Guid.Empty)
            throw new ArgumentException("ResourceRawRecordId must not be empty.", nameof(resourceRawRecordId));
        if (string.IsNullOrWhiteSpace(canonicalText))
            throw new ArgumentException("CanonicalText is required.", nameof(canonicalText));
        if (string.IsNullOrWhiteSpace(normalizedJson))
            throw new ArgumentException("NormalizedJson is required.", nameof(normalizedJson));
        if (string.IsNullOrWhiteSpace(contentFingerprint))
            throw new ArgumentException("ContentFingerprint is required.", nameof(contentFingerprint));

        ResourceRawRecordId = resourceRawRecordId;
        CandidateType = candidateType;
        CanonicalText = canonicalText.Trim();
        NormalizedJson = normalizedJson;
        LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "unknown" : languageCode.Trim().ToLowerInvariant();
        SearchText = (searchText ?? string.Empty).Trim().ToLowerInvariant();
        ContentFingerprint = contentFingerprint.Trim();
        ValidationStatus = validationStatus;
        ReviewStatus = AdminReviewStatus.NotRequired;
        ContextTagsJson = contextTagsJson ?? "[]";
        FocusTagsJson = focusTagsJson ?? "[]";
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Admin-only free-text note. The only field an admin may edit on a candidate in
    /// Phase E1 — there is no approve/reject/publish workflow yet (Phase E4).</summary>
    public void SetAdminNotes(string? adminNotes)
    {
        AdminNotes = adminNotes?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
