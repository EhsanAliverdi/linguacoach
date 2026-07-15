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
    public ResourceCandidateReviewStatus ReviewStatus { get; private set; }

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

    // ── Phase J5c — uploaded audio (ListeningPassage candidates only) ──────────────
    /// <summary>The IFileStorageService object key for this candidate's uploaded audio file.
    /// Null until an admin uploads one via <see cref="AttachAudio"/> — staging a
    /// ListeningPassage candidate from an imported row never sets this by itself.</summary>
    public string? AudioStorageKey { get; private set; }
    public string? AudioContentType { get; private set; }

    // ── Phase 4 (2026-07-15 large-scale AI import packages) — transcript/audio provenance and
    // general field-level provenance for candidates staged from an ImportPackage. ─────────────
    /// <summary>Null when this candidate has no transcript (e.g. audio not yet transcribed) or
    /// wasn't staged from a package. "AITranscribed" (see <see cref="MetadataOrigin"/>) vs.
    /// "SourceMetadata"/"AdministratorProvided" distinguishes a generated transcript from a
    /// supplied one — never conflated.</summary>
    public MetadataOrigin? TranscriptOrigin { get; private set; }
    public double? TranscriptConfidence { get; private set; }
    public string? SttProviderName { get; private set; }
    public string? SttModelName { get; private set; }

    /// <summary>Per-field provenance for this candidate's structured content, keyed by field name
    /// (e.g. "cefrLevel", "title") — see <c>ResourceCandidateFieldProvenance</c> for the
    /// deserialized shape (origin, confidence, provider/model, timestamp). Null for candidates
    /// staged before Phase 4 or via the simple deterministic pipeline, where every populated field
    /// is implicitly DeterministicallyExtracted/AIInferred per the existing Phase E2 convention.</summary>
    public string? MetadataProvenanceJson { get; private set; }

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
        ReviewStatus = ResourceCandidateReviewStatus.NotRequired;
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
    /// — a Passed OR warning-only NeedsReview candidate is a real candidate for an eventual admin
    /// publish decision (Phase E4): NeedsReview means advisory warnings only (duplicate content,
    /// low-confidence CEFR, missing attribution, etc.) — never a hard block, that's what Failed is
    /// for — so it enters the same review queue Passed does, letting an admin approve-and-override
    /// it. Failed candidates are not ready for that queue and are left at NotRequired until they
    /// pass a later re-validation.
    /// </summary>
    public void ApplyValidation(ResourceCandidateValidationStatus status, string? validationSummaryJson)
    {
        ValidationStatus = status;
        RejectReason = string.IsNullOrWhiteSpace(validationSummaryJson) ? null : validationSummaryJson;

        if (status is ResourceCandidateValidationStatus.Passed or ResourceCandidateValidationStatus.NeedsReview
            && ReviewStatus == ResourceCandidateReviewStatus.NotRequired)
        {
            ReviewStatus = ResourceCandidateReviewStatus.PendingReview;
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
        ReviewStatus = ResourceCandidateReviewStatus.Approved;
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

        ReviewStatus = ResourceCandidateReviewStatus.Rejected;
        AdminNotes = reason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Phase 3 (2026-07-15 import candidate review workflow) — "I am intentionally ignoring this
    /// candidate," distinct from never having been reviewed (<see cref="ResourceCandidateReviewStatus.PendingReview"/>).
    /// Unlike <see cref="Reject"/>, a reason is optional — skipping is a lighter-weight decision
    /// than an explicit rejection. Blocked once published for the same reason <see cref="Reject"/>
    /// is: no unpublish step exists, so a published candidate's review decision is final.
    /// </summary>
    public void Skip(string? reason = null)
    {
        if (IsPublished)
            throw new InvalidOperationException(
                "Cannot skip a resource candidate that has already been published — no unpublish step exists in this phase.");

        ReviewStatus = ResourceCandidateReviewStatus.Skipped;
        if (!string.IsNullOrWhiteSpace(reason))
            AdminNotes = reason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Phase 3 (2026-07-15 import candidate review workflow) — lets an admin edit a candidate's
    /// staged content before approval. Every parameter is optional/independent: pass only the
    /// fields the review UI actually changed, matching <see cref="ApplyAnalysis"/>'s "always
    /// overwrites, never merges partial state silently" discipline for the ones that ARE passed —
    /// a null here always means "leave this field as-is," never "clear it" (use an explicit empty
    /// string/array for that). <paramref name="normalizedJson"/> carries every type-specific field
    /// (word/definition/title/body/examples/etc — see <c>ResourceCandidateFieldHelper</c>'s
    /// field-name lookups) since a <see cref="ResourceCandidate"/> has no per-type typed columns;
    /// editing content means replacing this JSON blob. Blocked once
    /// published — published content is immutable, edit through the Resource Bank instead (mirrors
    /// <see cref="Reject"/>/<see cref="AttachAudio"/>'s same guard). Does not itself re-run
    /// validation — the caller (<c>IAdminResourceCandidateContentUpdateHandler</c>) is responsible
    /// for calling <see cref="ApplyValidation"/> afterward so <see cref="ValidationStatus"/> and
    /// <see cref="RejectReason"/> reflect the edited content, not stale pre-edit gates.
    /// </summary>
    public void UpdateContent(
        string? canonicalText = null,
        string? normalizedJson = null,
        string? cefrLevel = null,
        string? primarySkill = null,
        string? subskill = null,
        int? difficultyBand = null,
        string? contextTagsJson = null,
        string? focusTagsJson = null)
    {
        if (IsPublished)
            throw new InvalidOperationException(
                "Cannot edit a resource candidate that has already been published — edit through the Resource Bank instead.");
        if (cefrLevel is not null && !CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"CefrLevel '{cefrLevel}' is not a recognized CEFR level.", nameof(cefrLevel));
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5.");

        if (!string.IsNullOrWhiteSpace(canonicalText))
            CanonicalText = canonicalText.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedJson))
            NormalizedJson = normalizedJson;
        if (cefrLevel is not null)
            CefrLevel = cefrLevel;
        if (primarySkill is not null)
            PrimarySkill = primarySkill;
        if (subskill is not null)
            Subskill = subskill;
        if (difficultyBand is not null)
            DifficultyBand = difficultyBand;
        if (contextTagsJson is not null)
            ContextTagsJson = contextTagsJson;
        if (focusTagsJson is not null)
            FocusTagsJson = focusTagsJson;

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

    /// <summary>
    /// Phase J5c — attaches (or replaces) this candidate's uploaded audio file reference. The
    /// actual bytes are written to storage by the caller (see IResourceCandidateAudioService)
    /// before this is called — this method only records the resulting key. Blocked once the
    /// candidate is published: published content is immutable, mirroring <see cref="Reject"/>'s
    /// same guard, so an admin can't silently swap the audio backing a live bank row.
    /// </summary>
    public void AttachAudio(string storageKey, string contentType)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("StorageKey is required.", nameof(storageKey));
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("ContentType is required.", nameof(contentType));
        if (IsPublished)
            throw new InvalidOperationException(
                "Cannot change the audio file on a resource candidate that has already been published.");

        AudioStorageKey = storageKey.Trim();
        AudioContentType = contentType.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Phase 4 (Part G — audio without transcript) — records an AI-generated transcript from
    /// speech-to-text, distinct in origin from a transcript supplied directly with the source
    /// (which should instead be written via <see cref="UpdateContent"/>'s <c>normalizedJson</c>
    /// with <see cref="MetadataOrigin.SourceMetadata"/> recorded in
    /// <see cref="MetadataProvenanceJson"/>). Never overwrites a transcript an administrator has
    /// already corrected — see <see cref="ApplyFieldProvenance"/>'s precedence rule.
    /// </summary>
    public void SetGeneratedTranscript(string transcriptText, double? confidence, string providerName, string modelName)
    {
        if (string.IsNullOrWhiteSpace(transcriptText))
            throw new ArgumentException("TranscriptText is required.", nameof(transcriptText));
        if (IsPublished)
            throw new InvalidOperationException("Cannot change the transcript on a resource candidate that has already been published.");

        TranscriptOrigin = MetadataOrigin.AITranscribed;
        TranscriptConfidence = confidence;
        SttProviderName = providerName;
        SttModelName = modelName;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Marks the transcript as supplied directly by the source/admin rather than
    /// generated — set alongside writing the transcript text itself into NormalizedJson.</summary>
    public void SetSuppliedTranscript(MetadataOrigin origin)
    {
        if (origin is MetadataOrigin.AITranscribed or MetadataOrigin.AIGenerated or MetadataOrigin.AIInferred)
            throw new ArgumentException("Use SetGeneratedTranscript for an AI-produced transcript.", nameof(origin));

        TranscriptOrigin = origin;
        TranscriptConfidence = null;
        SttProviderName = null;
        SttModelName = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Phase 4 (Part F — metadata precedence and provenance). Records/updates one field's
    /// provenance entry, enforcing that an <see cref="MetadataOrigin.AdministratorCorrected"/> or
    /// <see cref="MetadataOrigin.AdministratorProvided"/> value is never silently downgraded by a
    /// later AI-origin write for the same field — the caller must check
    /// <see cref="CanFieldBeAiOverwritten"/> before applying an AI suggestion's value to the field
    /// itself; this method only maintains the provenance ledger.
    /// </summary>
    public void ApplyFieldProvenance(string fieldName, MetadataOrigin origin, double? confidence, string? providerName, string? modelName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("FieldName is required.", nameof(fieldName));

        var provenance = string.IsNullOrWhiteSpace(MetadataProvenanceJson)
            ? new Dictionary<string, ResourceCandidateFieldProvenanceEntry>()
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ResourceCandidateFieldProvenanceEntry>>(MetadataProvenanceJson!)
                ?? new Dictionary<string, ResourceCandidateFieldProvenanceEntry>();

        if (provenance.TryGetValue(fieldName, out var existing)
            && existing.Origin is MetadataOrigin.AdministratorCorrected or MetadataOrigin.AdministratorProvided
            && origin is not (MetadataOrigin.AdministratorCorrected or MetadataOrigin.AdministratorProvided))
        {
            // An admin-set value's provenance entry is never overwritten by a lower-precedence
            // origin — the caller should have already skipped writing the field value itself.
            return;
        }

        provenance[fieldName] = new ResourceCandidateFieldProvenanceEntry(origin, confidence, providerName, modelName, DateTimeOffset.UtcNow);
        MetadataProvenanceJson = System.Text.Json.JsonSerializer.Serialize(provenance);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>True unless <paramref name="fieldName"/>'s current provenance is
    /// AdministratorProvided/AdministratorCorrected — the Part F precedence gate an AI enrichment
    /// step must check before overwriting a field value.</summary>
    public bool CanFieldBeAiOverwritten(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(MetadataProvenanceJson)) return true;

        var provenance = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, ResourceCandidateFieldProvenanceEntry>>(MetadataProvenanceJson!);
        if (provenance is null || !provenance.TryGetValue(fieldName, out var entry)) return true;

        return entry.Origin is not (MetadataOrigin.AdministratorCorrected or MetadataOrigin.AdministratorProvided);
    }
}

/// <summary>Phase 4 — one field's provenance ledger entry. See
/// <see cref="ResourceCandidate.ApplyFieldProvenance"/>.</summary>
public sealed record ResourceCandidateFieldProvenanceEntry(
    MetadataOrigin Origin,
    double? Confidence,
    string? ProviderName,
    string? ModelName,
    DateTimeOffset RecordedAtUtc);
