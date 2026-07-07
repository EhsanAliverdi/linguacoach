using LinguaCoach.Domain.Common;
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
        bool isEnabled = true)
    {
        if (string.IsNullOrWhiteSpace(skill)) throw new ArgumentException("Skill is required.", nameof(skill));
        if (string.IsNullOrWhiteSpace(cefrLevel)) throw new ArgumentException("CefrLevel is required.", nameof(cefrLevel));

        Skill = skill.Trim();
        CefrLevel = cefrLevel.Trim();
        ItemOrder = itemOrder;
        IsEnabled = isEnabled;
    }

    public void Update(
        string skill,
        string cefrLevel,
        int itemOrder,
        bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(skill)) throw new ArgumentException("Skill is required.", nameof(skill));
        if (string.IsNullOrWhiteSpace(cefrLevel)) throw new ArgumentException("CefrLevel is required.", nameof(cefrLevel));

        Skill = skill.Trim();
        CefrLevel = cefrLevel.Trim();
        ItemOrder = itemOrder;
        IsEnabled = isEnabled;
    }
}
