using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase I0 — the single physical Resource Bank table, replacing the four typed tables
/// (CefrVocabularyEntry/CefrGrammarProfileEntry/CefrReadingReference/CefrReadingPassage). Common,
/// DB-filterable fields (CefrLevel/Subskill/DifficultyBand/ContextTagsJson/FocusTagsJson) stay real
/// columns so selectors like TodayBankResourceSelector's relaxation ladder keep working; the
/// genuinely type-specific payload (word/grammar point/reading excerpt/passage text, etc.) is
/// packed into <see cref="ContentJson"/> and deserialized per-<see cref="Type"/> at the
/// application layer.
/// </summary>
public sealed class ResourceBankItem : BaseEntity
{
    public PublishedResourceType Type { get; private set; }
    public string CefrLevel { get; private set; } = string.Empty;
    public string? Subskill { get; private set; }
    public int? DifficultyBand { get; private set; }
    public string? ContextTagsJson { get; private set; }
    public string? FocusTagsJson { get; private set; }
    public Guid SourceId { get; private set; }
    public string? ContentFingerprint { get; private set; }
    public string ContentJson { get; private set; } = "{}";
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>Phase K3 — admin-facing soft-delete. Archived items are excluded from the default
    /// Resource Bank list/browse views but the row and every link into it (LessonResourceLink,
    /// ExerciseResourceLink) stay intact — archiving never breaks a Lesson/Exercise/Module that
    /// already references this resource, it only hides the row from new authoring flows.</summary>
    public bool IsArchived { get; private set; }

    private ResourceBankItem() { }

    public ResourceBankItem(
        PublishedResourceType type,
        Guid sourceId,
        string cefrLevel,
        string contentJson,
        string? subskill = null,
        int? difficultyBand = null,
        string? contextTagsJson = null,
        string? focusTagsJson = null,
        string? contentFingerprint = null)
    {
        if (sourceId == Guid.Empty)
            throw new ArgumentException("SourceId is required.", nameof(sourceId));
        if (!CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"Invalid CEFR level '{cefrLevel}'.", nameof(cefrLevel));
        if (string.IsNullOrWhiteSpace(contentJson))
            throw new ArgumentException("ContentJson is required.", nameof(contentJson));
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5.");

        Type = type;
        SourceId = sourceId;
        CefrLevel = cefrLevel.ToUpperInvariant();
        ContentJson = contentJson;
        Subskill = subskill?.Trim();
        DifficultyBand = difficultyBand;
        ContextTagsJson = contextTagsJson;
        FocusTagsJson = focusTagsJson;
        ContentFingerprint = contentFingerprint?.Trim();
    }

    /// <summary>Phase I0 backfill only — constructs a row preserving the original typed table's Id,
    /// so LessonResourceLink/ExerciseResourceLink's existing ResourceId values keep resolving
    /// with no link-table migration needed.</summary>
    public static ResourceBankItem Reconstitute(
        Guid id,
        DateTime createdAt,
        PublishedResourceType type,
        Guid sourceId,
        string cefrLevel,
        string contentJson,
        string? subskill,
        int? difficultyBand,
        string? contextTagsJson,
        string? focusTagsJson,
        string? contentFingerprint,
        DateTime? updatedAt)
    {
        var item = new ResourceBankItem(
            type, sourceId, cefrLevel, contentJson, subskill, difficultyBand,
            contextTagsJson, focusTagsJson, contentFingerprint);
        item.Id = id;
        item.CreatedAt = createdAt;
        item.UpdatedAt = updatedAt;
        return item;
    }

    public void Archive()
    {
        IsArchived = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unarchive()
    {
        IsArchived = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
