using LinguaCoach.Application.Admin.ReviewQueue;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Placement;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Admin.ReviewQueue;

public sealed class AdminReviewQueueQueryHandler : IAdminReviewQueueQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminReviewQueueQueryHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<AdminReviewQueueResult> HandleAsync(ListAdminReviewQueueQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var reviewStatus = !string.IsNullOrWhiteSpace(query.ReviewStatus)
            && Enum.TryParse<AdminReviewStatus>(query.ReviewStatus, ignoreCase: true, out var parsed)
                ? parsed
                : (AdminReviewStatus?)null;

        var includeTemplates = query.EntityType is null
            || string.Equals(query.EntityType, ReviewQueueEntityType.ActivityTemplate, StringComparison.OrdinalIgnoreCase);
        var includeItems = query.EntityType is null
            || string.Equals(query.EntityType, ReviewQueueEntityType.PlacementItem, StringComparison.OrdinalIgnoreCase);

        var templateItems = new List<AdminReviewQueueItemDto>();
        if (includeTemplates)
        {
            var templatesQuery = _db.ActivityTemplates.AsQueryable();
            if (reviewStatus.HasValue) templatesQuery = templatesQuery.Where(t => t.ReviewStatus == reviewStatus.Value);

            var templates = await templatesQuery.ToListAsync(ct);
            templateItems.AddRange(templates.Select(t => new AdminReviewQueueItemDto(
                ReviewQueueEntityType.ActivityTemplate, t.Id, t.Key, t.Skill, t.CefrLevel,
                t.ReviewStatus.ToString(), t.CreatedAt)));
        }

        var placementItems = new List<AdminReviewQueueItemDto>();
        if (includeItems)
        {
            var itemsQuery = _db.PlacementItemDefinitions.AsQueryable();
            if (reviewStatus.HasValue) itemsQuery = itemsQuery.Where(i => i.ReviewStatus == reviewStatus.Value);

            var items = await itemsQuery.ToListAsync(ct);
            placementItems.AddRange(items.Select(i => new AdminReviewQueueItemDto(
                ReviewQueueEntityType.PlacementItem, i.Id, PlacementItemSchemaLabel.ExtractLabel(i.FormIoSchemaJson),
                i.Skill, i.CefrLevel, i.ReviewStatus.ToString(), i.CreatedAt)));
        }

        var combined = templateItems.Concat(placementItems)
            .OrderBy(d => d.CreatedAt)
            .ToList();

        var totalCount = combined.Count;

        // Always-unfiltered pending count across both entity types, for the KPI strip —
        // independent of this request's own ReviewStatus filter (mirrors the
        // OverallTotalCount/PublishedCount convention used by the other admin list endpoints).
        var pendingTemplates = await _db.ActivityTemplates.CountAsync(t => t.ReviewStatus == AdminReviewStatus.PendingReview, ct);
        var pendingItems = await _db.PlacementItemDefinitions.CountAsync(i => i.ReviewStatus == AdminReviewStatus.PendingReview, ct);
        var pendingCount = pendingTemplates + pendingItems;

        var pageItems = combined.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new AdminReviewQueueResult(pageItems, totalCount, pendingCount);
    }
}
