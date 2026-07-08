using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A staged candidate produced from a <see cref="ResourceRawRecord"/> that passed all Phase E1
/// import gates (English-only, within-run duplicate check, recognizable-content check).
/// AI analysis (Phase E2) and deterministic validation (Phase E2) populate the classification
/// fields and <see cref="ValidationStatus"/>; <see cref="ReviewStatus"/> is the separate admin
/// approval step (<see cref="Approve"/>/<see cref="Reject"/>); Phase E4 (<see cref="MarkPublished"/>)
/// is the final step that copies an approved, validated candidate into a Cefr* bank table.
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

    /// <summary>
    /// Phase E1: a plain rejection reason string. Phase E2 broadens this field's meaning (no
    /// migration — it was already a free-text nullable column) to hold the most recent
    /// deterministic validation run's result summary, as a small JSON object
    /// <c>{"errors":[...],"warnings":[...]}</c>, regardless of whether the candidate passed.
    /// This avoids standing up a separate validation-log entity for E2 — see
    /// <see cref="ApplyValidation"/>.
    /// </summary>
    public string? RejectReason { get; private set; }
    public string? AdminNotes { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    // ── Phase E4 publish state ──────────────────────────────────────────────────
    /// <summary>True once this candidate has been published into a Cefr* bank table. Publishing
    /// is a one-way, idempotent action per candidate — a published candidate is never
    /// re-published (see <see cref="MarkPublished"/>) and cannot be rejected without an unpublish
    /// step, which this phase does not implement.</summary>
    public bool IsPublished { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }

    /// <summary>e.g. "CefrVocabularyEntry" — the target bank entity's simple type name.</summary>
    public string? PublishedEntityType { get; private set; }
    public Guid? PublishedEntityId { get; private set; }

    /// <summary>Best-effort — null when the acting admin's user id was not available to the
    /// publish handler, mirroring <see cref="ResourceImportRun.ImportedByUserId"/>'s convention.</summary>
    public Guid? PublishedByUserId { get; private set; }

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

    /// <summary>
    /// Phase E2 — stores the AI's advisory classification output. The AI never decides
    /// <see cref="ValidationStatus"/> directly; that stays the sole responsibility of
    /// <see cref="ApplyValidation"/>, which runs deterministically over whatever field values are
    /// current (which may include what this method just wrote). Safe to call repeatedly — each
    /// call overwrites the prior analysis, it never appends/duplicates.
    /// </summary>
    public void ApplyAnalysis(
        string aiAnalysisJson,
        string? cefrLevel,
        double? cefrConfidence,
        string? primarySkill,
        string? subskill,
        int? difficultyBand,
        string? contextTagsJson,
        string? focusTagsJson,
        string? grammarTagsJson,
        string? vocabularyTagsJson,
        string? pronunciationTagsJson,
        string? activitySuitabilityTagsJson,
        string? safetyTagsJson,
        double? qualityScore,
        string? searchText)
    {
        if (string.IsNullOrWhiteSpace(aiAnalysisJson))
            throw new ArgumentException("AiAnalysisJson is required.", nameof(aiAnalysisJson));
        if (cefrLevel is not null && !CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"CefrLevel '{cefrLevel}' is not a recognized CEFR level.", nameof(cefrLevel));
        if (cefrConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(cefrConfidence), "CefrConfidence must be between 0 and 1.");
        if (qualityScore is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(qualityScore), "QualityScore must be between 0 and 1.");
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5 (matches CurriculumObjective.DifficultyBand's scale).");

        AiAnalysisJson = aiAnalysisJson;
        CefrLevel = cefrLevel;
        CefrConfidence = cefrConfidence;
        PrimarySkill = primarySkill;
        Subskill = subskill;
        DifficultyBand = difficultyBand;
        ContextTagsJson = contextTagsJson ?? "[]";
        FocusTagsJson = focusTagsJson ?? "[]";
        GrammarTagsJson = grammarTagsJson;
        VocabularyTagsJson = vocabularyTagsJson;
        PronunciationTagsJson = pronunciationTagsJson;
        ActivitySuitabilityTagsJson = activitySuitabilityTagsJson;
        SafetyTagsJson = safetyTagsJson;
        QualityScore = qualityScore;
        if (!string.IsNullOrWhiteSpace(searchText))
            SearchText = searchText.Trim().ToLowerInvariant();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Phase E2 — records the outcome of a deterministic rule-validation pass (see
    /// IResourceCandidateValidationService). <paramref name="validationSummaryJson"/> is a small
    /// JSON object <c>{"errors":[...],"warnings":[...]}</c> describing why, stored in
    /// <see cref="RejectReason"/> (reused rather than adding a new column/entity — see that
    /// field's doc comment). Passing validation promotes <see cref="ReviewStatus"/> from
    /// <see cref="AdminReviewStatus.NotRequired"/> to <see cref="AdminReviewStatus.PendingReview"/>
    /// — only a fully-passed candidate is a real candidate for an eventual admin publish decision
    /// (Phase E4); Failed/NeedsReview candidates are not yet ready for that queue and are left at
    /// NotRequired until they pass a later re-validation.
    /// </summary>
    public void ApplyValidation(ResourceCandidateValidationStatus status, string? validationSummaryJson)
    {
        ValidationStatus = status;
        RejectReason = string.IsNullOrWhiteSpace(validationSummaryJson) ? null : validationSummaryJson;

        if (status == ResourceCandidateValidationStatus.Passed
            && ReviewStatus == AdminReviewStatus.NotRequired)
        {
            ReviewStatus = AdminReviewStatus.PendingReview;
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Phase E4 — admin approval step, distinct from <see cref="ValidationStatus"/> (the
    /// deterministic gate). <paramref name="notes"/> is optional and, when provided, overwrites
    /// <see cref="AdminNotes"/> — mirrors the "approve with an optional comment" shape used by
    /// <see cref="ActivityTemplate.Approve"/>'s caller convention, but (unlike that entity) this
    /// one actually persists the note since <see cref="ResourceCandidate"/> already has a free-text
    /// notes column to put it in.
    /// </summary>
    public void Approve(string? notes = null)
    {
        ReviewStatus = AdminReviewStatus.Approved;
        if (notes is not null)
            AdminNotes = notes.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Phase E4 — admin rejection. Reason is required and is stored in <see cref="AdminNotes"/>
    /// (never in <see cref="RejectReason"/> — that column is Phase E2's deterministic validation
    /// summary JSON and must not be clobbered by an unrelated admin action). A published candidate
    /// cannot be rejected: this phase has no unpublish step, so rejecting a candidate whose bank
    /// row already exists would leave a contradictory state (a "rejected" row backing a live
    /// published bank entity) — blocked outright rather than silently allowed.
    /// </summary>
    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required to reject a resource candidate.", nameof(reason));
        if (IsPublished)
            throw new InvalidOperationException(
                "Cannot reject a resource candidate that has already been published — no unpublish step exists in this phase.");

        ReviewStatus = AdminReviewStatus.Rejected;
        AdminNotes = reason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Phase E4 — records that this candidate was published into a Cefr* bank table. Callers
    /// (see IResourceCandidatePublishService) must treat a candidate whose <see cref="IsPublished"/>
    /// is already true as an idempotent no-op and must NOT call this again / create a second bank
    /// row — this method itself does not enforce that (it always overwrites), so the guard is the
    /// publish service's responsibility, checked before any bank-table mutation happens.
    /// </summary>
    public void MarkPublished(
        string publishedEntityType, Guid publishedEntityId, DateTimeOffset publishedAtUtc, Guid? publishedByUserId)
    {
        if (string.IsNullOrWhiteSpace(publishedEntityType))
            throw new ArgumentException("PublishedEntityType is required.", nameof(publishedEntityType));
        if (publishedEntityId == Guid.Empty)
            throw new ArgumentException("PublishedEntityId must not be empty.", nameof(publishedEntityId));

        IsPublished = true;
        PublishedEntityType = publishedEntityType.Trim();
        PublishedEntityId = publishedEntityId;
        PublishedAtUtc = publishedAtUtc;
        PublishedByUserId = publishedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
