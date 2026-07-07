using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Admin-configurable placement assessment item, replacing the previously hardcoded
/// in-code item bank. Mirrors the OnboardingStepDefinition admin-CRUD pattern.
///
/// Form.io-native authoring (post-migration): every item is authored directly via the
/// Form.io builder (<see cref="FormIoSchemaJson"/>) with a backend-only scoring artifact
/// (<see cref="ScoringRulesJson"/>) keyed by Form.io component key. The legacy
/// QuestionContent-based authoring path (CorrectAnswer/ReadingPassage/ListeningAudioScript/
/// ContentJson) has been retired, as have the ItemType/Prompt admin-authored fields — the
/// Form.io schema is now the only source of what the student sees.
/// </summary>
public sealed class PlacementItemDefinition : BaseEntity
{
    public string Skill { get; private set; } = string.Empty;
    public string CefrLevel { get; private set; } = string.Empty;

    public int ItemOrder { get; private set; }
    public bool IsEnabled { get; private set; }

    /// <summary>Optional finer-grained classification beneath Skill. Null means unclassified.
    /// Must be a value from CurriculumSubskillConstants belonging to Skill when set.</summary>
    public string? Subskill { get; private set; }

    /// <summary>Student-safe Form.io schema for this item — never contains a correct answer or
    /// scoring data. Required for every item under the Form.io-native authoring model.</summary>
    public string? FormIoSchemaJson { get; private set; }

    /// <summary>Backend-only: correct answer(s)/rubric for this item, keyed by Form.io component
    /// key. Never returned to students.</summary>
    public string? ScoringRulesJson { get; private set; }

    /// <summary>Incremented every time ScoringRulesJson changes — copied onto issued
    /// PlacementAssessmentItem rows as a snapshot reference so a later scoring-rule edit never
    /// silently reinterprets a historical answer.</summary>
    public int ScoringRulesVersion { get; private set; }

    /// <summary>Which rendering engine FormIoSchemaJson is authored for. Only FormIo exists
    /// today; kept alongside the schema so a future alternate renderer doesn't need a further
    /// migration to distinguish items.</summary>
    public FormRendererKind RendererKind { get; private set; } = FormRendererKind.FormIo;

    /// <summary>Admin-only: the Form.io schema as authored in the builder, including inline
    /// per-component "quiz" annotations (enabled + correct answer) — never returned to students.
    /// The server-side <c>IFormIoQuizSchemaSplitter</c> is the sole authority that derives the
    /// student-safe <see cref="FormIoSchemaJson"/> and backend-only <see cref="ScoringRulesJson"/>
    /// from this field; the Angular client never constructs the split itself. Null for items
    /// authored before the Quiz tab existed — they keep scoring via their existing
    /// ScoringRulesJson until an admin re-saves through the new UI.</summary>
    public string? AuthoringSchemaJson { get; private set; }

    // --- Calibration (Phase 7 — AI Bank-First Teaching Architecture) ---

    /// <summary>Difficulty band 1 (easiest) to 5 (hardest) within CefrLevel — same scale as
    /// CurriculumObjective.DifficultyBand.</summary>
    public int DifficultyBand { get; private set; } = 1;

    /// <summary>How strongly this item discriminates between students who know the skill and
    /// those who don't (classical test theory point-biserial correlation, roughly -1..1). Null
    /// until enough attempts have been collected to compute it — populated later from attempt
    /// statistics, not required for an item to be usable.</summary>
    public double? DiscriminationIndex { get; private set; }

    /// <summary>Number of scored attempts DiscriminationIndex was computed from. Null alongside
    /// DiscriminationIndex until calibration data exists.</summary>
    public int? CalibrationSampleSize { get; private set; }

    /// <summary>Multiplier applied when this item's result feeds into a skill's confidence
    /// score (PlacementAssessmentService) — lets a more reliable item count for more than a
    /// newly-added, unproven one. Default 1.0 (equal weight).</summary>
    public double EvidenceWeight { get; private set; } = 1.0;

    /// <summary>Admin review state for this bank item — separate from IsEnabled (which is a
    /// simple on/off switch). NotRequired by default for hand-authored items.</summary>
    public AdminReviewStatus ReviewStatus { get; private set; } = AdminReviewStatus.NotRequired;

    /// <summary>Version number for this item's authoring content. Starts at 1; incremented only
    /// by a future "create next version" flow (not yet exposed — see docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md).</summary>
    public int ItemVersion { get; private set; } = 1;

    /// <summary>Id of the PlacementItemDefinition row this version was derived from, if any.</summary>
    public Guid? PreviousVersionId { get; private set; }

    public void SetFormIoAuthoring(string? formIoSchemaJson, string? scoringRulesJson, FormRendererKind rendererKind = FormRendererKind.FormIo)
    {
        FormIoSchemaJson = formIoSchemaJson;
        if (!string.Equals(ScoringRulesJson, scoringRulesJson, StringComparison.Ordinal))
            ScoringRulesVersion++;
        ScoringRulesJson = scoringRulesJson;
        RendererKind = rendererKind;
    }

    public void SetAuthoringSchema(string? authoringSchemaJson)
    {
        AuthoringSchemaJson = authoringSchemaJson;
    }

    private PlacementItemDefinition() { }

    public PlacementItemDefinition(
        string skill,
        string cefrLevel,
        int itemOrder,
        bool isEnabled = true,
        string? subskill = null,
        int difficultyBand = 1,
        double evidenceWeight = 1.0,
        int itemVersion = 1,
        Guid? previousVersionId = null)
    {
        if (string.IsNullOrWhiteSpace(skill)) throw new ArgumentException("Skill is required.", nameof(skill));
        if (string.IsNullOrWhiteSpace(cefrLevel)) throw new ArgumentException("CefrLevel is required.", nameof(cefrLevel));
        if (!CurriculumSubskillConstants.IsValidForSkill(skill, subskill))
            throw new ArgumentException($"Subskill '{subskill}' does not belong to skill '{skill}'.", nameof(subskill));
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5.");
        if (evidenceWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(evidenceWeight), "EvidenceWeight must be >= 0.");

        Skill = skill.Trim();
        CefrLevel = cefrLevel.Trim();
        ItemOrder = itemOrder;
        IsEnabled = isEnabled;
        Subskill = subskill?.Trim().ToLowerInvariant();
        DifficultyBand = difficultyBand;
        EvidenceWeight = evidenceWeight;
        ItemVersion = itemVersion;
        PreviousVersionId = previousVersionId;
    }

    public void Update(
        string skill,
        string cefrLevel,
        int itemOrder,
        bool isEnabled,
        int? difficultyBand = null,
        double? evidenceWeight = null)
    {
        if (string.IsNullOrWhiteSpace(skill)) throw new ArgumentException("Skill is required.", nameof(skill));
        if (string.IsNullOrWhiteSpace(cefrLevel)) throw new ArgumentException("CefrLevel is required.", nameof(cefrLevel));
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5.");
        if (evidenceWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(evidenceWeight), "EvidenceWeight must be >= 0.");

        Skill = skill.Trim();
        CefrLevel = cefrLevel.Trim();
        ItemOrder = itemOrder;
        IsEnabled = isEnabled;
        DifficultyBand = difficultyBand ?? DifficultyBand;
        EvidenceWeight = evidenceWeight ?? EvidenceWeight;
    }

    /// <summary>Sets or clears the subskill independently of Update, so an admin edit that
    /// doesn't touch subskill never silently resets it.</summary>
    public void SetSubskill(string? subskill)
    {
        if (!CurriculumSubskillConstants.IsValidForSkill(Skill, subskill))
            throw new ArgumentException($"Subskill '{subskill}' does not belong to skill '{Skill}'.", nameof(subskill));

        Subskill = subskill?.Trim().ToLowerInvariant();
    }

    /// <summary>Records calibration statistics computed from real attempt data. Both null means
    /// "not yet calibrated" — this is expected for new items and is not an error state.</summary>
    public void SetCalibrationStats(double? discriminationIndex, int? calibrationSampleSize)
    {
        if (calibrationSampleSize is < 0)
            throw new ArgumentOutOfRangeException(nameof(calibrationSampleSize), "CalibrationSampleSize must be >= 0.");

        DiscriminationIndex = discriminationIndex;
        CalibrationSampleSize = calibrationSampleSize;
    }

    public void Approve() => ReviewStatus = AdminReviewStatus.Approved;

    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required to reject a placement item.", nameof(reason));

        ReviewStatus = AdminReviewStatus.Rejected;
        IsEnabled = false;
    }

    public void ResetToPendingReview() => ReviewStatus = AdminReviewStatus.PendingReview;
}
