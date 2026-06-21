using System.Security.Claims;
using LinguaCoach.Application.Notifications;
using LinguaCoach.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinguaCoach.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationQueryService _query;

    public NotificationsController(INotificationQueryService query) => _query = query;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] string? category = null,
        [FromQuery] string? severity = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        NotificationCategory? parsedCategory = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!Enum.TryParse<NotificationCategory>(category, ignoreCase: true, out var cat))
                return BadRequest(new { error = $"Invalid category '{category}'." });
            parsedCategory = cat;
        }

        NotificationSeverity? parsedSeverity = null;
        if (!string.IsNullOrWhiteSpace(severity))
        {
            if (!Enum.TryParse<NotificationSeverity>(severity, ignoreCase: true, out var sev))
                return BadRequest(new { error = $"Invalid severity '{severity}'." });
            parsedSeverity = sev;
        }

        var clampedPageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _query.ListAsync(
            new NotificationListQuery(userId, Math.Max(1, page), clampedPageSize, unreadOnly, parsedCategory, parsedSeverity),
            ct);

        return Ok(new
        {
            items = result.Items.Select(ToResponse),
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize,
            totalPages = result.TotalPages
        });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var count = await _query.GetUnreadCountAsync(userId, ct);
        return Ok(new { unreadCount = count });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await _query.MarkReadAsync(id, userId, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await _query.MarkAllReadAsync(userId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await _query.ArchiveAsync(id, userId, ct);
        return NoContent();
    }

    private static object ToResponse(NotificationDto n) => new
    {
        id = n.Id,
        title = n.Title,
        body = n.Body,
        category = n.Category.ToString(),
        severity = n.Severity.ToString(),
        channel = n.Channel.ToString(),
        status = n.Status.ToString(),
        createdAtUtc = n.CreatedAtUtc,
        readAtUtc = n.ReadAtUtc,
        expiresAtUtc = n.ExpiresAtUtc,
        deepLinkUrl = n.DeepLinkUrl,
        metadataJson = n.MetadataJson
    };

    private Guid GetCurrentUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}
