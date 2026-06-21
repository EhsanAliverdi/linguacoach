namespace LinguaCoach.Application.Admin;

// ── Query inputs ──────────────────────────────────────────────────────────────

public sealed record AdminTemplateListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Channel = null,
    string? Category = null,
    bool? IsActive = null,
    string? Search = null);

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record AdminTemplateItem(
    Guid Id,
    string TemplateKey,
    string Channel,
    string Name,
    string? Subject,
    string? Title,
    string Body,
    string Category,
    string Severity,
    bool IsActive,
    int Version,
    string? SupportedVariablesJson,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

// ── Commands ──────────────────────────────────────────────────────────────────

public sealed record AdminCreateTemplateCommand(
    string TemplateKey,
    string Channel,
    string Name,
    string Body,
    string Category,
    string Severity,
    string? Subject,
    string? Title,
    string? Description,
    string? SupportedVariablesJson);

public sealed record AdminUpdateTemplateCommand(
    string Name,
    string Body,
    string Category,
    string Severity,
    string? Subject,
    string? Title,
    string? Description,
    string? SupportedVariablesJson);

// ── Preview ───────────────────────────────────────────────────────────────────

public sealed record AdminTemplatePreviewRequest(
    IReadOnlyDictionary<string, string> Variables);

public sealed record AdminTemplatePreviewResult(
    bool Succeeded,
    string? RenderedSubject,
    string? RenderedTitle,
    string RenderedBody,
    IReadOnlyList<string> MissingVariables);

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IAdminTemplateHandler
{
    Task<PagedResponse<AdminTemplateItem>> ListTemplatesAsync(
        AdminTemplateListQuery query, CancellationToken ct = default);

    Task<AdminTemplateItem?> GetTemplateAsync(Guid id, CancellationToken ct = default);

    Task<AdminTemplateItem> CreateTemplateAsync(
        AdminCreateTemplateCommand command, Guid adminUserId, CancellationToken ct = default);

    Task<AdminTemplateItem> UpdateTemplateAsync(
        Guid id, AdminUpdateTemplateCommand command, Guid adminUserId, CancellationToken ct = default);

    Task DeactivateTemplateAsync(Guid id, Guid adminUserId, CancellationToken ct = default);

    Task<AdminTemplatePreviewResult> PreviewTemplateAsync(
        Guid id, AdminTemplatePreviewRequest request, CancellationToken ct = default);
}
