namespace LinguaCoach.Application.Onboarding;

// ── Shared Form.io schema validation (also used by placement item authoring) ───────

public sealed record FormIoValidationResult(bool IsValid, string? Error)
{
    public static FormIoValidationResult Ok() => new(true, null);
    public static FormIoValidationResult Fail(string error) => new(false, error);
}

public interface IFormIoSchemaValidationService
{
    /// <summary>Validates a student-safe Form.io schema JSON: approved component types only,
    /// no script/eval-style properties, no external data sources, and no answer/scoring-shaped
    /// keys leaking into the public schema.</summary>
    FormIoValidationResult ValidateSchema(string formIoSchemaJson);
}

// ── Admin: onboarding template management ──────────────────────────────────────────

public sealed record StudentFlowTemplateSummaryDto(
    Guid TemplateId,
    string Name,
    string? Description,
    string Status,
    Guid? ActiveVersionId,
    int VersionCount,
    DateTimeOffset UpdatedAt);

public sealed record StudentFlowTemplateVersionDto(
    Guid VersionId,
    Guid TemplateId,
    int VersionNumber,
    string FormIoSchemaJson,
    string? ScoringRulesJson,
    string RendererKind,
    string Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset UpdatedAt,
    /// <summary>Admin-only: the Form.io schema as authored (with inline "quiz" annotations),
    /// null for versions authored before the Quiz tab existed. Never sent to students.</summary>
    string? AuthoringSchemaJson = null);

public sealed record StudentFlowTemplateDetailDto(
    Guid TemplateId,
    string Name,
    string? Description,
    string Status,
    Guid? ActiveVersionId,
    IReadOnlyList<StudentFlowTemplateVersionDto> Versions);

public sealed record ListOnboardingTemplatesQuery();

public interface IAdminListOnboardingTemplatesQuery
{
    Task<IReadOnlyList<StudentFlowTemplateSummaryDto>> HandleAsync(ListOnboardingTemplatesQuery query, CancellationToken ct = default);
}

public sealed record GetOnboardingTemplateQuery(Guid TemplateId);

public interface IAdminGetOnboardingTemplateQuery
{
    Task<StudentFlowTemplateDetailDto?> HandleAsync(GetOnboardingTemplateQuery query, CancellationToken ct = default);
}

public sealed record CreateOnboardingTemplateCommand(string Name, string? Description, Guid AdminId);

public interface IAdminCreateOnboardingTemplateHandler
{
    Task<StudentFlowTemplateDetailDto> HandleAsync(CreateOnboardingTemplateCommand command, CancellationToken ct = default);
}

public sealed record SaveOnboardingTemplateDraftCommand(
    Guid TemplateId, string FormIoSchemaJson, string? ScoringRulesJson, Guid AdminId, string RendererKind = "FormIo",
    /// <summary>See <see cref="LinguaCoach.Application.Placement.AddPlacementItemCommand.AuthoringSchemaJson"/> —
    /// same Quiz-tab authoring/server-side-split contract, shared with placement.</summary>
    string? AuthoringSchemaJson = null);

public interface IAdminSaveOnboardingTemplateDraftHandler
{
    Task<StudentFlowTemplateVersionDto> HandleAsync(SaveOnboardingTemplateDraftCommand command, CancellationToken ct = default);
}

public sealed record PublishOnboardingTemplateCommand(Guid TemplateId);

public interface IAdminPublishOnboardingTemplateHandler
{
    Task<StudentFlowTemplateVersionDto> HandleAsync(PublishOnboardingTemplateCommand command, CancellationToken ct = default);
}

public sealed record ArchiveOnboardingTemplateCommand(Guid TemplateId);

public interface IAdminArchiveOnboardingTemplateHandler
{
    Task HandleAsync(ArchiveOnboardingTemplateCommand command, CancellationToken ct = default);
}

public sealed record GetActiveOnboardingTemplateQuery();

public interface IAdminGetActiveOnboardingTemplateQuery
{
    Task<StudentFlowTemplateDetailDto?> HandleAsync(GetActiveOnboardingTemplateQuery query, CancellationToken ct = default);
}

// ── Student: onboarding flow ─────────────────────────────────────────────────────

public sealed record StudentOnboardingActiveDto(
    Guid TemplateVersionId,
    string FormIoSchemaJson,
    string RendererKind,
    string? SubmissionJson,
    bool IsComplete);

public sealed record GetStudentOnboardingActiveQuery(Guid UserId);

public interface IStudentOnboardingActiveQuery
{
    Task<StudentOnboardingActiveDto> HandleAsync(GetStudentOnboardingActiveQuery query, CancellationToken ct = default);
}

public sealed record SaveOnboardingDraftCommand(Guid UserId, string SubmissionJson);

public interface IStudentOnboardingSaveDraftHandler
{
    Task HandleAsync(SaveOnboardingDraftCommand command, CancellationToken ct = default);
}

public sealed record SubmitOnboardingCommand(Guid UserId, string SubmissionJson);

public sealed record SubmitOnboardingResult(bool Success, string? PreliminaryCefrLevel);

public interface IStudentOnboardingSubmitHandler
{
    Task<SubmitOnboardingResult> HandleAsync(SubmitOnboardingCommand command, CancellationToken ct = default);
}

// ── Validation error (kept — same exception type used by both onboarding and admin flows) ──

public sealed class OnboardingV2ValidationException : Exception
{
    public OnboardingV2ValidationException(string message) : base(message) { }
}
