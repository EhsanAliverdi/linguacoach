using LinguaCoach.Application.Admin;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class AdminTemplateHandler : IAdminTemplateHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly INotificationTemplateRenderer _renderer;
    private readonly ILogger<AdminTemplateHandler> _logger;

    public AdminTemplateHandler(
        LinguaCoachDbContext db,
        INotificationTemplateRenderer renderer,
        ILogger<AdminTemplateHandler> logger)
    {
        _db = db;
        _renderer = renderer;
        _logger = logger;
    }

    public async Task<PagedResponse<AdminTemplateItem>> ListTemplatesAsync(
        AdminTemplateListQuery query, CancellationToken ct = default)
    {
        var q = _db.NotificationTemplates.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Channel) &&
            Enum.TryParse<NotificationChannel>(query.Channel, ignoreCase: true, out var ch))
            q = q.Where(t => t.Channel == ch);

        if (!string.IsNullOrWhiteSpace(query.Category) &&
            Enum.TryParse<NotificationCategory>(query.Category, ignoreCase: true, out var cat))
            q = q.Where(t => t.Category == cat);

        if (query.IsActive.HasValue)
            q = q.Where(t => t.IsActive == query.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.ToLowerInvariant();
            q = q.Where(t =>
                t.TemplateKey.ToLower().Contains(s) ||
                t.Name.ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderBy(t => t.TemplateKey).ThenBy(t => t.Channel)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResponse<AdminTemplateItem>(
            Items: items.Select(ToItem).ToList(),
            TotalCount: total,
            Page: query.Page,
            PageSize: query.PageSize,
            TotalPages: (int)Math.Ceiling((double)total / query.PageSize));
    }

    public async Task<AdminTemplateItem?> GetTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _db.NotificationTemplates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return t is null ? null : ToItem(t);
    }

    public async Task<AdminTemplateItem> CreateTemplateAsync(
        AdminCreateTemplateCommand command, Guid adminUserId, CancellationToken ct = default)
    {
        if (!Enum.TryParse<NotificationChannel>(command.Channel, ignoreCase: true, out var channel))
            throw new ArgumentException($"Unknown channel: {command.Channel}", nameof(command));
        if (!Enum.TryParse<NotificationCategory>(command.Category, ignoreCase: true, out var category))
            throw new ArgumentException($"Unknown category: {command.Category}", nameof(command));
        if (!Enum.TryParse<NotificationSeverity>(command.Severity, ignoreCase: true, out var severity))
            throw new ArgumentException($"Unknown severity: {command.Severity}", nameof(command));

        // Reject duplicate active key+channel
        var duplicate = await _db.NotificationTemplates.AnyAsync(
            t => t.TemplateKey == command.TemplateKey && t.Channel == channel && t.IsActive, ct);
        if (duplicate)
            throw new InvalidOperationException(
                $"An active template already exists for key '{command.TemplateKey}' and channel '{command.Channel}'.");

        var template = NotificationTemplate.Create(
            templateKey: command.TemplateKey,
            channel: channel,
            name: command.Name,
            body: command.Body,
            category: category,
            severity: severity,
            subject: command.Subject,
            title: command.Title,
            description: command.Description,
            supportedVariablesJson: command.SupportedVariablesJson);

        _db.NotificationTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {AdminId} created notification template {Key}/{Channel} id={Id}.",
            adminUserId, template.TemplateKey, template.Channel, template.Id);

        return ToItem(template);
    }

    public async Task<AdminTemplateItem> UpdateTemplateAsync(
        Guid id, AdminUpdateTemplateCommand command, Guid adminUserId, CancellationToken ct = default)
    {
        var template = await _db.NotificationTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException($"Template {id} not found.");

        if (!Enum.TryParse<NotificationCategory>(command.Category, ignoreCase: true, out var category))
            throw new ArgumentException($"Unknown category: {command.Category}", nameof(command));
        if (!Enum.TryParse<NotificationSeverity>(command.Severity, ignoreCase: true, out var severity))
            throw new ArgumentException($"Unknown severity: {command.Severity}", nameof(command));

        template.Update(
            name: command.Name,
            body: command.Body,
            subject: command.Subject,
            title: command.Title,
            category: category,
            severity: severity,
            description: command.Description,
            supportedVariablesJson: command.SupportedVariablesJson);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {AdminId} updated template {Id} v{Version}.",
            adminUserId, id, template.Version);

        return ToItem(template);
    }

    public async Task DeactivateTemplateAsync(Guid id, Guid adminUserId, CancellationToken ct = default)
    {
        var template = await _db.NotificationTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException($"Template {id} not found.");

        template.Deactivate();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {AdminId} deactivated template {Id}.", adminUserId, id);
    }

    public async Task<AdminTemplatePreviewResult> PreviewTemplateAsync(
        Guid id, AdminTemplatePreviewRequest request, CancellationToken ct = default)
    {
        var template = await _db.NotificationTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException($"Template {id} not found.");

        var result = _renderer.Render(
            subject: template.Subject,
            title: template.Title,
            body: template.Body,
            variables: request.Variables);

        return new AdminTemplatePreviewResult(
            Succeeded: result.Succeeded,
            RenderedSubject: result.RenderedSubject,
            RenderedTitle: result.RenderedTitle,
            RenderedBody: result.RenderedBody,
            MissingVariables: result.MissingVariables);
    }

    private static AdminTemplateItem ToItem(NotificationTemplate t) =>
        new(t.Id, t.TemplateKey, t.Channel.ToString(), t.Name,
            t.Subject, t.Title, t.Body,
            t.Category.ToString(), t.Severity.ToString(),
            t.IsActive, t.Version,
            t.SupportedVariablesJson, t.Description,
            t.CreatedAtUtc, t.UpdatedAtUtc);
}
