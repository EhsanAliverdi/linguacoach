using LinguaCoach.Application.FormIo;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

/// <summary>Admin authoring service for the Form.io onboarding template (StudentFlowTemplate,
/// FlowKind.Onboarding). Every draft save and publish is re-validated server-side via
/// IFormIoSchemaValidationService — the admin builder's client-side output is never trusted.</summary>
public sealed class AdminOnboardingTemplateService :
    IAdminListOnboardingTemplatesQuery,
    IAdminGetOnboardingTemplateQuery,
    IAdminCreateOnboardingTemplateHandler,
    IAdminSaveOnboardingTemplateDraftHandler,
    IAdminPublishOnboardingTemplateHandler,
    IAdminArchiveOnboardingTemplateHandler,
    IAdminGetActiveOnboardingTemplateQuery
{
    private readonly LinguaCoachDbContext _db;
    private readonly IFormIoSchemaValidationService _validator;
    private readonly IFormIoQuizSchemaSplitter _splitter;

    public AdminOnboardingTemplateService(LinguaCoachDbContext db, IFormIoSchemaValidationService validator, IFormIoQuizSchemaSplitter splitter)
    {
        _db = db;
        _validator = validator;
        _splitter = splitter;
    }

    public async Task<IReadOnlyList<StudentFlowTemplateSummaryDto>> HandleAsync(ListOnboardingTemplatesQuery query, CancellationToken ct = default)
    {
        var templates = await _db.StudentFlowTemplates
            .Include(t => t.Versions)
            .Where(t => t.FlowKind == StudentFlowKind.Onboarding)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return templates.Select(ToSummary).ToList();
    }

    public async Task<StudentFlowTemplateDetailDto?> HandleAsync(GetOnboardingTemplateQuery query, CancellationToken ct = default)
    {
        var template = await _db.StudentFlowTemplates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == query.TemplateId && t.FlowKind == StudentFlowKind.Onboarding, ct);

        return template is null ? null : ToDetail(template);
    }

    public async Task<StudentFlowTemplateDetailDto?> HandleAsync(GetActiveOnboardingTemplateQuery query, CancellationToken ct = default)
    {
        var template = await _db.StudentFlowTemplates
            .Include(t => t.Versions)
            .Where(t => t.FlowKind == StudentFlowKind.Onboarding && t.Status == StudentFlowTemplateStatus.Published && t.ActiveVersionId != null)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return template is null ? null : ToDetail(template);
    }

    public async Task<StudentFlowTemplateDetailDto> HandleAsync(CreateOnboardingTemplateCommand command, CancellationToken ct = default)
    {
        var template = new StudentFlowTemplate(StudentFlowKind.Onboarding, command.Name, command.Description);
        var version = new StudentFlowTemplateVersion(template.Id, 1, "{\"components\":[]}", command.AdminId);
        template.AddVersion(version);

        _db.StudentFlowTemplates.Add(template);
        _db.StudentFlowTemplateVersions.Add(version);
        await _db.SaveChangesAsync(ct);

        return ToDetail(template);
    }

    public async Task<StudentFlowTemplateVersionDto> HandleAsync(SaveOnboardingTemplateDraftCommand command, CancellationToken ct = default)
    {
        string formIoSchemaJson, scoringRulesJson;
        if (command.AuthoringSchemaJson is not null)
        {
            var split = _splitter.Split(command.AuthoringSchemaJson);
            formIoSchemaJson = split.StudentSchemaJson;
            scoringRulesJson = split.ScoringRulesJson;
        }
        else
        {
            formIoSchemaJson = command.FormIoSchemaJson;
            scoringRulesJson = command.ScoringRulesJson ?? "";
        }

        var schemaResult = _validator.ValidateSchema(formIoSchemaJson);
        if (!schemaResult.IsValid)
            throw new OnboardingV2ValidationException(schemaResult.Error ?? "Invalid Form.io schema.");

        var template = await _db.StudentFlowTemplates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == command.TemplateId && t.FlowKind == StudentFlowKind.Onboarding, ct)
            ?? throw new OnboardingV2ValidationException("Template not found.");

        var draft = template.Versions.Where(v => v.Status == StudentFlowTemplateStatus.Draft)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        if (draft is null)
        {
            var nextVersion = template.Versions.Count == 0 ? 1 : template.Versions.Max(v => v.VersionNumber) + 1;
            draft = new StudentFlowTemplateVersion(template.Id, nextVersion, formIoSchemaJson, command.AdminId, scoringRulesJson, ParseRendererKind(command.RendererKind));
            template.AddVersion(draft);
            _db.StudentFlowTemplateVersions.Add(draft);
        }
        else
        {
            draft.UpdateDraft(formIoSchemaJson, scoringRulesJson, ParseRendererKind(command.RendererKind));
        }

        if (command.AuthoringSchemaJson is not null)
            draft.SetAuthoringSchema(command.AuthoringSchemaJson);

        await _db.SaveChangesAsync(ct);
        return ToVersionDto(draft);
    }

    public async Task<StudentFlowTemplateVersionDto> HandleAsync(PublishOnboardingTemplateCommand command, CancellationToken ct = default)
    {
        var template = await _db.StudentFlowTemplates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == command.TemplateId && t.FlowKind == StudentFlowKind.Onboarding, ct)
            ?? throw new OnboardingV2ValidationException("Template not found.");

        var draft = template.Versions.Where(v => v.Status == StudentFlowTemplateStatus.Draft)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault()
            ?? throw new OnboardingV2ValidationException("No draft version to publish.");

        var schemaResult = _validator.ValidateSchema(draft.FormIoSchemaJson);
        if (!schemaResult.IsValid)
            throw new OnboardingV2ValidationException(schemaResult.Error ?? "Invalid Form.io schema.");

        // Hardening: StudentOnboardingFlowService.ApplyToProfileAsync maps submitted data onto
        // StudentProfile purely by component-key string match — there is no other enforcement
        // that a key it depends on actually exists in the schema. A template missing a required
        // key would silently produce students with that profile field always null, forever, with
        // no error surfaced anywhere. Block publish (not draft save, so admins can iterate freely)
        // if any required key is missing.
        var presentKeys = FormIoQuizAnnotationCodec.CollectComponentKeys(draft.FormIoSchemaJson);
        var missingRequired = OnboardingProfileFieldMapping.RequiredKeys.Where(k => !presentKeys.Contains(k)).ToList();
        if (missingRequired.Count > 0)
            throw new OnboardingV2ValidationException(
                $"Cannot publish: the schema is missing required field(s): {string.Join(", ", missingRequired)}. " +
                "See the Field mapping panel in the builder for what each key must contain.");

        // Archive any previously-published version of this same template.
        foreach (var v in template.Versions.Where(v => v.Status == StudentFlowTemplateStatus.Published))
            v.Archive();

        draft.Publish();
        template.SetActiveVersion(draft.Id);

        await _db.SaveChangesAsync(ct);
        return ToVersionDto(draft);
    }

    public async Task HandleAsync(ArchiveOnboardingTemplateCommand command, CancellationToken ct = default)
    {
        var template = await _db.StudentFlowTemplates
            .FirstOrDefaultAsync(t => t.Id == command.TemplateId && t.FlowKind == StudentFlowKind.Onboarding, ct)
            ?? throw new OnboardingV2ValidationException("Template not found.");

        template.Archive();
        await _db.SaveChangesAsync(ct);
    }

    private static StudentFlowTemplateSummaryDto ToSummary(StudentFlowTemplate t) => new(
        t.Id, t.Name, t.Description, t.Status.ToString(), t.ActiveVersionId, t.Versions.Count, t.UpdatedAt);

    private static StudentFlowTemplateDetailDto ToDetail(StudentFlowTemplate t) => new(
        t.Id, t.Name, t.Description, t.Status.ToString(), t.ActiveVersionId,
        t.Versions.OrderByDescending(v => v.VersionNumber).Select(ToVersionDto).ToList());

    private static StudentFlowTemplateVersionDto ToVersionDto(StudentFlowTemplateVersion v) => new(
        v.Id, v.TemplateId, v.VersionNumber, v.FormIoSchemaJson, v.ScoringRulesJson, v.RendererKind.ToString(), v.Status.ToString(), v.PublishedAt, v.UpdatedAt,
        v.AuthoringSchemaJson);

    private static FormRendererKind ParseRendererKind(string rendererKind) =>
        Enum.TryParse<FormRendererKind>(rendererKind, ignoreCase: true, out var parsed) ? parsed : FormRendererKind.FormIo;
}
