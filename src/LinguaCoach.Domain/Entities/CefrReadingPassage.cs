using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase E7 — a full-length, original/internal (or explicitly license-approved) English reading
/// passage. Distinct from <see cref="CefrReadingReference"/>, which intentionally holds only a
/// short excerpt/citation ("reading difficulty guidance, not a content library" — see that
/// entity's own doc comment) and is capped at
/// <see cref="Infrastructure.ResourceImport.ResourceCandidatePublishService.MaxReadingExcerptLength"/>
/// characters at publish time. A <c>ReadingPassage</c> candidate whose staged text exceeds that
/// threshold publishes here instead — see
/// <see cref="Infrastructure.ResourceImport.ResourceCandidatePublishService"/>'s ReadingPassage
/// handling for the exact routing rule. Never holds copyrighted third-party text — this bank is
/// for original/internal content or content whose source is explicitly license-approved for full
/// republication (tracked via <see cref="SourceId"/>/<see cref="AttributionText"/>).
/// </summary>
public sealed class CefrReadingPassage : BaseEntity
{
    public Guid SourceId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string PassageText { get; private set; } = string.Empty;
    public string? Summary { get; private set; }
    public string CefrLevel { get; private set; } = string.Empty;
    public int? DifficultyBand { get; private set; }
    public string PrimarySkill { get; private set; } = "Reading";
    public string? Subskill { get; private set; }
    public string? TopicTagsJson { get; private set; }
    public string? ContextTagsJson { get; private set; }
    public string? FocusTagsJson { get; private set; }
    public int WordCount { get; private set; }
    public int EstimatedReadingMinutes { get; private set; }

    /// <summary>Denormalized snapshot of the source's attribution text at publish time. Unlike
    /// CefrVocabularyEntry/CefrGrammarProfileEntry/CefrReadingReference (which rely solely on a
    /// join to CefrResourceSource for attribution), full passages carry their own copy — a future
    /// batch could plausibly mix passages needing per-passage attribution nuance that a single
    /// source-level field can't capture as precisely. Still joined to Source for the rest of the
    /// license/provenance picture.</summary>
    public string? AttributionText { get; private set; }

    /// <summary>Deterministic content fingerprint copied from the originating ResourceCandidate at
    /// publish time (not recomputed) — available for a future within-bank duplicate check; no such
    /// check is implemented yet (candidate-level exact-fingerprint dedup already runs at Phase E2
    /// validation time, before this row ever gets published).</summary>
    public string? ContentFingerprint { get; private set; }

    public double? QualityScore { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    private const double AssumedWordsPerMinute = 200;

    private CefrReadingPassage() { }

    public CefrReadingPassage(
        Guid sourceId,
        string title,
        string passageText,
        string cefrLevel,
        string? summary = null,
        int? difficultyBand = null,
        string primarySkill = "Reading",
        string? subskill = null,
        string? topicTagsJson = null,
        string? contextTagsJson = null,
        string? focusTagsJson = null,
        string? attributionText = null,
        string? contentFingerprint = null,
        double? qualityScore = null)
    {
        if (sourceId == Guid.Empty)
            throw new ArgumentException("SourceId is required.", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(passageText))
            throw new ArgumentException("PassageText is required.", nameof(passageText));
        if (!CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"Invalid CEFR level '{cefrLevel}'.", nameof(cefrLevel));
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5.");
        if (qualityScore is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(qualityScore), "QualityScore must be between 0 and 1.");

        SourceId = sourceId;
        Title = title.Trim();
        PassageText = passageText.Trim();
        Summary = summary?.Trim();
        CefrLevel = cefrLevel.ToUpperInvariant();
        DifficultyBand = difficultyBand;
        PrimarySkill = string.IsNullOrWhiteSpace(primarySkill) ? "Reading" : primarySkill.Trim();
        Subskill = subskill?.Trim();
        TopicTagsJson = topicTagsJson;
        ContextTagsJson = contextTagsJson;
        FocusTagsJson = focusTagsJson;
        AttributionText = attributionText?.Trim();
        ContentFingerprint = contentFingerprint?.Trim();
        QualityScore = qualityScore;

        WordCount = PassageText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        EstimatedReadingMinutes = Math.Max(1, (int)Math.Round(WordCount / AssumedWordsPerMinute, MidpointRounding.AwayFromZero));
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
