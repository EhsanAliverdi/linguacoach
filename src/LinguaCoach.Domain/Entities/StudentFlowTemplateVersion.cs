using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>One versioned snapshot of a StudentFlowTemplate's Form.io schema.
/// FormIoSchemaJson is student-safe (never contains correct answers/scoring). ScoringRulesJson
/// is backend-only, keyed by Form.io component `key`, and must never be returned to students.</summary>
public sealed class StudentFlowTemplateVersion : BaseEntity
{
    public Guid TemplateId { get; private set; }
    public StudentFlowTemplate? Template { get; private set; }
    public int VersionNumber { get; private set; }
    public string FormIoSchemaJson { get; private set; } = "{}";
    public string? ScoringRulesJson { get; private set; }

    /// <summary>Admin-only: the Form.io schema as authored in the builder, including inline
    /// per-component "quiz" annotations (enabled + correct answer) — never returned to students.
    /// The server-side <c>IFormIoQuizSchemaSplitter</c> is the sole authority that derives the
    /// student-safe <see cref="FormIoSchemaJson"/> and backend-only <see cref="ScoringRulesJson"/>
    /// from this field. Null for versions authored before the Quiz tab existed.</summary>
    public string? AuthoringSchemaJson { get; private set; }

    public FormRendererKind RendererKind { get; private set; } = FormRendererKind.FormIo;
    public StudentFlowTemplateStatus Status { get; private set; }
    public Guid CreatedByAdminId { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private StudentFlowTemplateVersion() { }

    public StudentFlowTemplateVersion(
        Guid templateId,
        int versionNumber,
        string formIoSchemaJson,
        Guid createdByAdminId,
        string? scoringRulesJson = null,
        FormRendererKind rendererKind = FormRendererKind.FormIo)
    {
        if (templateId == Guid.Empty) throw new ArgumentException("TemplateId is required.", nameof(templateId));
        if (versionNumber <= 0) throw new ArgumentException("VersionNumber must be positive.", nameof(versionNumber));
        if (string.IsNullOrWhiteSpace(formIoSchemaJson)) throw new ArgumentException("FormIoSchemaJson is required.", nameof(formIoSchemaJson));

        TemplateId = templateId;
        VersionNumber = versionNumber;
        FormIoSchemaJson = formIoSchemaJson;
        ScoringRulesJson = scoringRulesJson;
        RendererKind = rendererKind;
        CreatedByAdminId = createdByAdminId;
        Status = StudentFlowTemplateStatus.Draft;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDraft(string formIoSchemaJson, string? scoringRulesJson, FormRendererKind rendererKind = FormRendererKind.FormIo)
    {
        if (Status != StudentFlowTemplateStatus.Draft)
            throw new InvalidOperationException("Only draft versions can be edited.");
        if (string.IsNullOrWhiteSpace(formIoSchemaJson)) throw new ArgumentException("FormIoSchemaJson is required.", nameof(formIoSchemaJson));

        FormIoSchemaJson = formIoSchemaJson;
        ScoringRulesJson = scoringRulesJson;
        RendererKind = rendererKind;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetAuthoringSchema(string? authoringSchemaJson)
    {
        AuthoringSchemaJson = authoringSchemaJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Publish()
    {
        Status = StudentFlowTemplateStatus.Published;
        PublishedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Archive()
    {
        Status = StudentFlowTemplateStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
